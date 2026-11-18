using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Mercantec.Tech.Api.Avatars;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Contracts.HomeGame;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.HomeGame;

public sealed class HomeRunCoordinator(IServiceScopeFactory scopes, HomeRunnerClient runner,
    ILogger<HomeRunCoordinator> logger, IOptions<HomeGameOptions>? options = null) : BackgroundService
{
    private static readonly TimeSpan DisconnectedAfter = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan PauseLimit = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RunLimit = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan WorkerHeartbeat = TimeSpan.FromSeconds(55);
    private const int MaxLogBytes = 64 * 1024;
    private const int MaxConcurrentRuns = 24;
    private readonly DateTimeOffset processStartedAt = DateTimeOffset.UtcNow;
    private readonly ConcurrentDictionary<Guid, LiveRun> active = new();
    private readonly SemaphoreSlim capacity = new(MaxConcurrentRuns, MaxConcurrentRuns);
    private readonly SemaphoreSlim workers = new(Math.Clamp(options?.Value.WorkerSlots ?? 8, 1, MaxConcurrentRuns),
        MaxConcurrentRuns);

    internal async Task<HomeRunStatus?> Start(Guid subject, HomeStartRunRequest request, HomeLayout layout,
        HomeProgramDraft program, IReadOnlyList<HomeScenario> scenarios, HomeItem controller, bool teacherSandbox,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!capacity.Wait(0)) return null;
        var keys = layout.Items.ToDictionary(item => item.Alias, RunnerCapability, StringComparer.Ordinal);
        var run = new LiveRun(subject, request, layout, program, scenarios, controller.Id, keys, teacherSandbox);
        if (!active.TryAdd(subject, run))
        {
            capacity.Release();
            return null;
        }

        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var json = HomeGameJson.Write(run.Stored());
            var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO home_game_runs ("Id", "UserSub", "Revision", "StartedAtUtc", "Active", "RunJson")
                SELECT {run.Id}, {subject}, {layout.Revision}, {run.Started}, true, {json}
                FROM home_game_profiles WHERE "UserSub" = {subject} AND "Revision" = {layout.Revision}
                ON CONFLICT DO NOTHING
                """, CancellationToken.None);
            if (affected != 1)
            {
                Remove(run);
                return null;
            }
        }
        catch
        {
            Remove(run);
            throw;
        }

        var status = run.Status();
        ScheduleCompile(run);
        return status;
    }

    internal async Task<HomeRunStatus?> Step(Guid subject, Guid id, HomeStepRunRequest request)
    {
        if (!TryGet(subject, id, out var run)) return null;
        await run.Gate.WaitAsync();
        try
        {
            await PublishPending(run);
            if (run.State != "running" || request.AfterSequence != run.Sequence) return run.Status();
            run.LastContact = DateTimeOffset.UtcNow;
            if (!await IsCurrent(run)) return await Cancel(run, "cancelled");
            if (!run.Simulation.ControllerFinished && run.LastContact - run.LastWorkerContact > WorkerHeartbeat)
                await KeepWorkerAlive(run);
            for (var i = 0; i < Math.Clamp(request.MaxTicks, 1, 20) && run.State == "running"; i++)
            {
                if (run.Lifetime.IsCancellationRequested || run.PausePending) break;
                var frame = run.Simulation.BeginTick();
                if (frame.ControllerDue)
                {
                    if (!await Activate(run, frame)) break;
                }
                run.Simulation.AdvanceTick();
                if (run.Simulation.Finished)
                    await FinishScenario(run);
            }
            run.Sequence++;
            await Persist(run, includeReplay: run.Terminal);
            if (run.Terminal) Remove(run);
            return run.Status();
        }
        finally
        {
            run.Gate.Release();
        }
    }

    internal async Task<HomeRunStatus?> Control(Guid subject, Guid id, HomeRunControlRequest request)
    {
        if (!TryGet(subject, id, out var run)) return null;
        if (request.Action == "stop") run.Lifetime.Cancel();
        if (request.Action == "pause") run.RequestPause();
        await run.Gate.WaitAsync();
        try
        {
            await PublishPending(run);
            if (run.Terminal) return run.Status();
            run.LastContact = DateTimeOffset.UtcNow;
            if (!await IsCurrent(run) || request.Action == "stop") return await Cancel(run, "cancelled");
            switch (request.Action)
            {
                case "pause" when run.State == "running":
                    run.State = "paused";
                    run.PausedAt = DateTimeOffset.UtcNow;
                    break;
                case "resume" when run.State == "paused":
                    run.State = "running";
                    run.PausedAt = null;
                    break;
                case "speed" when request.Speed is 1 or 4 or 10:
                    run.Speed = request.Speed.Value;
                    break;
                case "input" when !run.Assessment && run.State is "running" or "paused"
                    && run.Scenarios[run.ScenarioIndex].Free:
                    if (request.Intent is { } intent) run.Simulation.SetIntention(intent);
                    if (request.DaylightLux is int daylight) run.Simulation.SetDaylight(daylight);
                    if (request.AutoEnabled is bool auto) run.Simulation.SetAutoEnabled(auto);
                    run.Events.Add(new(run.ScenarioIndex, run.Simulation.Snapshot().Tick, [], null, false,
                        request.Intent, request.DaylightLux, request.AutoEnabled));
                    break;
                default:
                    return run.Status();
            }
            run.Sequence++;
            await Persist(run, includeReplay: false);
            return run.Status();
        }
        finally
        {
            if (request.Action == "pause") run.ReleasePause();
            run.Gate.Release();
        }
    }

    internal async Task Invalidate(Guid subject, int updatedRevision)
    {
        if (!active.TryGetValue(subject, out var run) || run.Revision >= updatedRevision) return;
        await Control(subject, run.Id, new("stop"));
    }

    internal async Task<HomeBuildGuard> GuardBuild(Guid subject, int revision, CancellationToken ct)
    {
        if (!active.TryGetValue(subject, out var run) || run.Revision != revision)
            return new(true, null);
        await run.Gate.WaitAsync(ct);
        if (run.Terminal)
        {
            run.Gate.Release();
            return new(true, null);
        }
        return new(run.State != "running", run.Simulation.Snapshot().AvatarCell, run.Gate);
    }

    internal static IReadOnlyList<HomeScenario> Scenarios(HomeTask task, HomeStartRunRequest request)
    {
        if (request.Assessment)
        {
            var seed = RandomNumberGenerator.GetInt32(1, int.MaxValue);
            return task.ScenarioIds.Select(id => HomeCatalog.WithSeed(HomeCatalog.Scenarios[id], seed)).ToArray();
        }
        if (request.ScenarioId is { } id && HomeCatalog.Scenarios.TryGetValue(id, out var scenario))
            return [scenario];
        return [HomeCatalog.FreeScenario with
        {
            Events = HomeCatalog.FreeScenario.Events.Select(change =>
                change.Tick == 0 ? change with { Intention = request.Intent } : change).ToArray()
        }];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stale = await db.HomeGameRuns.Where(row => row.Active && row.StartedAtUtc < processStartedAt)
                .ToListAsync(stoppingToken);
            foreach (var row in stale)
            {
                var stored = HomeGameJson.Read<HomeStoredRun>(row.RunJson);
                var replay = stored.Replay;
                var summary = replay.Run.Run with { Status = "expired", ErrorCode = "RUN_EXPIRED" };
                row.RunJson = HomeGameJson.Write(stored with
                {
                    Replay = replay with
                    {
                        Run = replay.Run with { Run = summary, ErrorMessage = "Forsøget udløb. Start det igen fra din gemte kode." }
                    }
                });
                row.Active = false;
            }
            await db.SaveChangesAsync(stoppingToken);
            await Task.WhenAll(stale.Select(row => runner.Stop(HomeGameJson.Read<HomeStoredRun>(row.RunJson).WorkerId)));
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var run in active.Values)
            {
                await Sweep(run, now);
            }
        }
    }

    private async Task Sweep(LiveRun run, DateTimeOffset now)
    {
        var expired = now - run.Started > RunLimit || run.PausedAt is { } since && now - since > PauseLimit;
        if (expired) run.Lifetime.Cancel();
        if (!run.Gate.Wait(0)) return;
        try
        {
            await PublishPending(run);
            if (run.Terminal) return;
            if (expired) await Cancel(run, "expired");
            else if (run.State == "running" && now - run.LastContact > DisconnectedAfter)
            {
                run.State = "paused";
                run.PausedAt = now;
                run.Sequence++;
                await Persist(run, includeReplay: false);
            }
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "Could not update Home.exe run {RunId}", run.Id);
        }
        finally { run.Gate.Release(); }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        var open = active.Values.ToArray();
        foreach (var run in open) run.Lifetime.Cancel();
        await Task.WhenAll(open.Select(run => runner.Stop(run.WorkerId)));
        await base.StopAsync(cancellationToken);
    }

    private void ScheduleCompile(LiveRun run) => _ = Compile(run);

    private async Task Compile(LiveRun run)
    {
        var reserved = false;
        try
        {
            if (!run.HasWorker)
            {
                await workers.WaitAsync(run.Lifetime.Token);
                reserved = true;
            }
            await run.Gate.WaitAsync(run.Lifetime.Token);
            try
            {
                if (reserved)
                {
                    run.HasWorker = true;
                    reserved = false;
                }
                if (run.Terminal || !await IsCurrent(run))
                {
                    await Cancel(run, "cancelled");
                    return;
                }
                run.State = "compiling";
                run.Sequence++;
                await Persist(run, includeReplay: false);
            }
            finally { run.Gate.Release(); }

            await runner.Start(run.WorkerId, run.Id, run.ControllerId, run.Program, run.Aliases, run.Lifetime.Token);
            await run.Gate.WaitAsync(run.Lifetime.Token);
            try
            {
                if (run.Terminal || !await IsCurrent(run))
                {
                    await Cancel(run, "cancelled");
                    return;
                }
                run.State = "running";
                run.Sequence++;
                run.LastContact = DateTimeOffset.UtcNow;
                run.LastWorkerContact = run.LastContact;
                await Persist(run, includeReplay: false);
            }
            finally { run.Gate.Release(); }
        }
        catch (OperationCanceledException) when (run.Lifetime.IsCancellationRequested)
        {
            // Stop owns the final status and closes the session through its server-created ID.
        }
        catch (Exception error)
        {
            await run.Gate.WaitAsync();
            try
            {
                if (!run.Terminal)
                {
                    await Fail(run, RunnerError(error, "Koden kunne ikke startes."));
                    run.Sequence++;
                    await Persist(run, includeReplay: true);
                    Remove(run);
                }
            }
            catch (Exception persistenceError)
            {
                logger.LogError(persistenceError, "Could not persist Home.exe run {RunId}", run.Id);
            }
            finally { run.Gate.Release(); }
        }
        finally
        {
            if (reserved) workers.Release();
        }
    }

    private async Task<bool> Activate(LiveRun run, HomeFrame frame)
    {
        try
        {
            var batch = await runner.Activate(run.WorkerId, run.Id, run.ControllerId,
                run.Activation, frame, run.Lifetime.Token);
            run.LastWorkerContact = DateTimeOffset.UtcNow;
            run.Lifetime.Token.ThrowIfCancellationRequested();
            var commands = batch.Commands!.Select(command => new HomeCommand(command.Alias, command.Action, command.Value))
                .ToArray();
            var error = run.Simulation.Commit(new(run.Id.ToString(), frame.Tick, run.Activation, commands,
                batch.NextDelayMs, batch.Done));
            if (error is not null)
            {
                await Fail(run, error);
                return false;
            }
            run.Activation++;
            // Replay needs only the final validated write for each installed alias.
            var effectiveCommands = commands.GroupBy(command => command.Alias, StringComparer.Ordinal)
                .Select(writes => writes.Last()).ToArray();
            run.Events.Add(new(run.ScenarioIndex, frame.Tick, effectiveCommands, batch.NextDelayMs, batch.Done));
            run.AppendLogs(batch.Logs ?? []);
            return true;
        }
        catch (OperationCanceledException) when (run.Lifetime.IsCancellationRequested) { return false; }
        catch (Exception error) when (error is HomeRunnerException or HttpRequestException or OperationCanceledException
            or IOException)
        {
            await Fail(run, RunnerError(error, "Kodearbejderen svarede ikke under kørslen."));
            return false;
        }
    }

    private async Task KeepWorkerAlive(LiveRun run)
    {
        try
        {
            await runner.KeepAlive(run.WorkerId, run.Lifetime.Token);
            run.LastWorkerContact = DateTimeOffset.UtcNow;
        }
        catch (OperationCanceledException) when (run.Lifetime.IsCancellationRequested) { }
        catch (Exception error) when (error is HomeRunnerException or HttpRequestException or OperationCanceledException)
        {
            await Fail(run, RunnerError(error, "Forbindelsen til kodearbejderen blev afbrudt."));
        }
    }

    private async Task FinishScenario(LiveRun run)
    {
        run.Outcomes.Add(run.Simulation.Outcome());
        await runner.Stop(run.WorkerId);
        if (run.Lifetime.IsCancellationRequested) return;
        if (run.ScenarioIndex + 1 < run.Scenarios.Count)
        {
            run.State = "queued";
            run.ScenarioIndex++;
            run.Simulation = new HomeSimulation(run.Layout, run.Scenarios[run.ScenarioIndex], run.Id.ToString());
            run.Activation = 1;
            run.WorkerId = Guid.NewGuid();
            ScheduleCompile(run);
            return;
        }
        run.State = !run.Assessment ? "completed" : run.Outcomes.All(outcome => outcome.Passed) ? "passed" : "failed";
    }

    private async Task<HomeRunStatus> Cancel(LiveRun run, string state)
    {
        run.Lifetime.Cancel();
        run.State = state;
        if (state == "expired")
            run.Error = new("RUN_EXPIRED", "Forsøget udløb. Start det igen fra din gemte kode.", []);
        run.Sequence++;
        await runner.Stop(run.WorkerId);
        await Persist(run, includeReplay: true);
        Remove(run);
        return run.Status();
    }

    private async Task Fail(LiveRun run, HomeRuleError error)
    {
        run.Error = error;
        run.State = "error";
        run.Simulation.Fault(error);
        run.Events.Add(new(run.ScenarioIndex, run.Simulation.Snapshot().Tick, [], null, false, Error: error));
        run.Outcomes.Add(run.Simulation.Outcome());
        await runner.Stop(run.WorkerId);
    }

    private async Task<bool> IsCurrent(LiveRun run)
    {
        using var scope = scopes.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<HomeGameStore>();
        return await store.Revision(run.Subject, CancellationToken.None) == run.Revision;
    }

    private async Task Persist(LiveRun run, bool includeReplay)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<HomeGameStore>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var query = db.HomeGameRuns.Where(row => row.Id == run.Id && row.UserSub == run.Subject && row.Active);
        if (!await query.AnyAsync())
        {
            var persisted = await store.FindRun(run.Subject, run.Id, CancellationToken.None);
            if (persisted is not null)
            {
                run.State = persisted.Run.Run.Status;
                run.GoldAwarded = persisted.Run.GoldAwarded;
            }
            run.PublishedSequence = run.Sequence;
            return;
        }

        var previousState = run.State;
        var previousAward = run.GoldAwarded;
        var previousError = run.Error;
        try
        {
            if (run.State == "passed" && !run.TeacherSandbox && run.GoldAwarded == 0)
            {
                var state = await store.Find(run.Subject, CancellationToken.None);
                if (state is null || state.Layout.Revision != run.Revision || run.Lifetime.IsCancellationRequested)
                    run.State = "cancelled";
                else
                {
                    var rewardKey = $"{HomeCatalog.Tasks[run.Task - 1].Id}:first-pass";
                    if (!state.Progress.Any(progress => progress.RewardKey == rewardKey))
                    {
                        var progress = new HomeTaskProgress(run.Task, rewardKey, HomeGameVersion.Content, run.Id, DateTimeOffset.UtcNow);
                        var updated = state with
                        {
                            Layout = state.Layout with { Revision = state.Layout.Revision + 1 },
                            Progress = [.. state.Progress, progress]
                        };
                        if (!await store.Write(run.Subject, run.Revision, updated, CancellationToken.None))
                            run.State = "cancelled";
                        else if (await AvatarWallet.TryAdjustGold(db, run.Subject, HomeCatalog.TaskRewardGold, CancellationToken.None))
                        {
                            run.GoldAwarded = HomeCatalog.TaskRewardGold;
                        }
                        else
                        {
                            // The profile row is still locked by this transaction.
                            if (!await store.Write(run.Subject, updated.Layout.Revision, state, CancellationToken.None))
                                throw new DbUpdateConcurrencyException("Home profile changed while reversing an unpayable reward.");
                            run.Error = new("GOLD_LIMIT", "Din guldsaldo kan ikke rumme belønningen.", []);
                            run.State = "error";
                        }
                    }
                }
            }

            var json = HomeGameJson.Write(run.Stored(includeReplay));
            if (await query.ExecuteUpdateAsync(update => update.SetProperty(row => row.Active, !run.Terminal)
                .SetProperty(row => row.RunJson, json)) != 1)
                throw new DbUpdateConcurrencyException("Home game run changed while its result was being saved.");
            await transaction.CommitAsync();
            run.PublishedSequence = run.Sequence;
        }
        catch
        {
            run.State = previousState;
            run.GoldAwarded = previousAward;
            run.Error = previousError;
            throw;
        }
    }

    private async Task PublishPending(LiveRun run)
    {
        if (run.PublishedSequence == run.Sequence) return;
        await Persist(run, includeReplay: run.Terminal);
        if (run.Terminal) Remove(run);
    }

    private bool TryGet(Guid subject, Guid id, out LiveRun run)
    {
        if (active.TryGetValue(subject, out var found) && found.Id == id)
        {
            run = found;
            return true;
        }
        run = null!;
        return false;
    }

    private void Remove(LiveRun run)
    {
        if (!active.TryRemove(KeyValuePair.Create(run.Subject, run))) return;
        capacity.Release();
        if (run.HasWorker) workers.Release();
    }

    private static HomeRuleError RunnerError(Exception error, string fallback) => error switch
    {
        HomeRunnerException runnerError => new(runnerError.Code, runnerError.Message, []),
        OperationCanceledException => new("RUNNER_TIMEOUT", fallback, []),
        _ => new("RUNNER_UNAVAILABLE", fallback, [])
    };

    private static string RunnerCapability(HomeItem item)
    {
        if (item.Placement is null) return "uninstalled";
        var kind = HomeCatalog.Definition(item.DefinitionId)?.Kind;
        return kind is "lamp" or "daylight" or "presence" or "switch" ? kind : "unsupported";
    }

    private sealed class LiveRun(Guid subject, HomeStartRunRequest request, HomeLayout layout,
        HomeProgramDraft program, IReadOnlyList<HomeScenario> scenarios, string controllerId,
        IReadOnlyDictionary<string, string> aliases, bool teacherSandbox)
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid Subject { get; } = subject;
        public HomeLayout Layout { get; } = layout;
        public int Revision => Layout.Revision;
        public HomeProgramDraft Program { get; } = program;
        public DateTimeOffset Started { get; } = DateTimeOffset.UtcNow;
        public int Task { get; } = request.Task;
        public bool Assessment { get; } = request.Assessment;
        public bool TeacherSandbox { get; } = teacherSandbox;
        public int Speed { get; set; } = request.Speed;
        public string InitialIntent { get; } = request.Intent;
        public IReadOnlyList<HomeScenario> Scenarios { get; } = scenarios;
        public string ControllerId { get; } = controllerId;
        public IReadOnlyDictionary<string, string> Aliases { get; } = aliases;
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public CancellationTokenSource Lifetime { get; } = new();
        public List<HomeReplayEvent> Events { get; } = [];
        public List<HomeScenarioOutcome> Outcomes { get; } = [];
        public List<string> Logs { get; } = [];
        public int LogBytes { get; private set; }
        public int ScenarioIndex { get; set; }
        private HomeSimulation? simulation;
        public HomeSimulation Simulation
        {
            get => simulation ??= new HomeSimulation(Layout, Scenarios[ScenarioIndex], Id.ToString());
            set => simulation = value;
        }
        public Guid WorkerId { get; set; } = Guid.NewGuid();
        public long Sequence { get; set; }
        public long PublishedSequence { get; set; }
        public long Activation { get; set; } = 1;
        public DateTimeOffset LastContact { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset LastWorkerContact { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? PausedAt { get; set; }
        public HomeRuleError? Error { get; set; }
        public int GoldAwarded { get; set; }
        public string State { get; set; } = "queued";
        public bool HasWorker { get; set; }
        private int pauseRequests;
        public bool PausePending => Volatile.Read(ref pauseRequests) > 0;
        public void RequestPause() => Interlocked.Increment(ref pauseRequests);
        public void ReleasePause() => Interlocked.Decrement(ref pauseRequests);
        public bool Terminal => State is "passed" or "failed" or "completed" or "error" or "cancelled" or "expired";

        public HomeRunStatus Status() => new(new(Id, Revision, Program.VersionId, Program.Language, Task,
            Assessment, State, State == "passed" ? true : State == "failed" ? false : null,
            Error?.Code, Started, Program.Hash, HomeGameVersion.Sdk, HomeGameVersion.Simulator, HomeGameVersion.Content,
            TeacherSandbox),
            Sequence, ScenarioIndex, Scenarios.Select(scenario => scenario.Id).ToArray(), Speed,
            Simulation.Snapshot(includeLightMap: true), Outcomes.ToArray(), Logs.ToArray(), Error?.Message, GoldAwarded);

        public HomeStoredRun Stored(bool includeReplay = true) =>
            new(new(Status(), Layout, Scenarios, includeReplay ? Events.ToArray() : [], InitialIntent), WorkerId);

        public void AppendLogs(IReadOnlyList<string> logs)
        {
            const string limit = "Loggrænsen er nået.";
            foreach (var log in logs)
            {
                if (LogBytes == MaxLogBytes) return;
                var size = Encoding.UTF8.GetByteCount(log) + 1;
                if (LogBytes + size > MaxLogBytes - Encoding.UTF8.GetByteCount(limit) - 1)
                {
                    Logs.Add(limit);
                    LogBytes = MaxLogBytes;
                    return;
                }
                Logs.Add(log);
                LogBytes += size;
            }
        }
    }
}

internal sealed class HomeBuildGuard(bool allowed, HomeCell? avatarCell, SemaphoreSlim? gate = null) : IDisposable
{
    private SemaphoreSlim? held = gate;
    public bool Allowed { get; } = allowed;
    public HomeCell? AvatarCell { get; } = avatarCell;
    public void Dispose() => Interlocked.Exchange(ref held, null)?.Release();
}
