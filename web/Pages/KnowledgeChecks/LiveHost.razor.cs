using System.Globalization;
using System.Net;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.KnowledgeChecks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Pages.KnowledgeChecks;

public partial class LiveHost
{
    [Inject] private KnowledgeCheckClient Catalog { get; set; } = default!;
    [Inject] private LiveKnowledgeCheckClient Client { get; set; } = default!;
    [Inject] private AuthRedirect Redirect { get; set; } = default!;
    [Inject] private ViewAsStudentContext ViewAs { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public string? Code { get; set; }
    [SupplyParameterFromQuery(Name = "sourceId")] public string? SourceId { get; set; }
    [SupplyParameterFromQuery(Name = "fromDraft")] public bool? FromDraft { get; set; }

    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private static readonly int[] QuestionDurations = [10, 20, 30, 45, 60];
    private const string LoadError = "Spillet er udløbet, eller du er ikke vært for det.";

    private CancellationTokenSource _scope = new();
    private LiveQuizState? Game;
    private KnowledgeCheckIndex? _sources;
    private KnowledgeCheckOwned[] Drafts = [];
    private KnowledgeCheckPublicationView[] Publications = [];
    private ElementReference _stageHeading;
    private DateTimeOffset? _deadline;
    private string? _room;
    private string? _soundCue;
    private string? _error;
    private string _source = "";
    private string _announcement = "";
    private int _seconds = 20;
    private int _commandVersion;
    private bool _initialized;
    private bool _loading;
    private bool _busy;
    private bool _connectionError;
    private bool _confirmEnd;
    private bool _linkCopied;
    private bool _focusPhase;
    private bool _soundOn;
    private bool _changingSound;

    private bool CanHost => Redirect.ShowsTeacherChrome;
    private string JoinPath => $"/spil/{Game?.Code}";
    private string JoinAddress => new Uri(Nav.BaseUri).Authority + "/spil";
    private int RemainingSeconds => _deadline is { } deadline
        ? Math.Clamp((int)Math.Ceiling((deadline - DateTimeOffset.UtcNow).TotalSeconds), 0, Game?.QuestionSeconds ?? 60)
        : 0;

    protected override async Task OnParametersSetAsync()
    {
        if (!CanHost) return;

        if (_initialized && _room == Code)
        {
            if (Code is null) SelectRequestedSource();
            return;
        }

        _initialized = true;
        _room = Code;
        _scope.Cancel();
        _scope.Dispose();
        _scope = new();
        Game = null;
        _deadline = null;
        _error = null;
        _loading = false;
        _busy = false;
        _connectionError = false;
        _confirmEnd = false;
        _linkCopied = false;
        _focusPhase = false;
        _soundCue = null;
        _announcement = "";

        if (Code is null)
        {
            if (_sources is null) await LoadSourcesAsync();
            else SelectRequestedSource();
        }
        else if (LiveQuiz.IsCode(Code))
        {
            await RetryRoomAsync();
        }
        else
        {
            _error = "Spilkoden skal være på seks cifre.";
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_soundCue is { } phase)
        {
            _soundCue = null;
            try { await JS.InvokeVoidAsync("lupLive.playCue", phase); }
            catch (JSException) { }
        }
        if (!_focusPhase || _stageHeading.Context is null) return;

        _focusPhase = false;
        try { await _stageHeading.FocusAsync(preventScroll: true); }
        catch (JSException) { }
    }

    private async Task LoadSourcesAsync()
    {
        var token = _scope.Token;
        _loading = true;
        _error = null;
        try
        {
            var sources = await Catalog.GetAsync<KnowledgeCheckIndex>(cancellationToken: token);
            if (token.IsCancellationRequested) return;

            _sources = sources;
            Drafts = sources.Owned.Where(draft => draft.QuestionCount > 0)
                .OrderBy(draft => draft.Title, StringComparer.Create(Danish, true)).ToArray();
            Publications = sources.Publications.Where(publication =>
                    publication.IsActive && publication.HoldId is null && publication.IsCurrentPractice && publication.QuestionCount > 0)
                .OrderBy(publication => publication.Title, StringComparer.Create(Danish, true)).ToArray();
            SelectRequestedSource();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) when (ex is KnowledgeCheckException or HttpRequestException or JsonException)
        {
            if (!token.IsCancellationRequested) _error = "Quizzerne kunne ikke hentes. Prøv igen om lidt.";
        }
        finally
        {
            if (!token.IsCancellationRequested) _loading = false;
        }
    }

    private void SelectRequestedSource()
    {
        if (!Guid.TryParse(SourceId, out var id)) return;

        var fromDraft = FromDraft == true;
        var exists = fromDraft ? Drafts.Any(draft => draft.Id == id) : Publications.Any(publication => publication.Id == id);
        _source = exists ? SourceKey(id, fromDraft) : "";
        _error = exists ? null : "Det valgte videnstjek er ikke tilgængeligt til live spil. Vælg en anden quiz.";
    }

    private async Task CreateAsync()
    {
        if (_busy || !CanHost || _source.Length < 2 || !Guid.TryParse(_source.AsSpan(1), out var source)) return;

        var token = _scope.Token;
        _busy = true;
        _error = null;
        try
        {
            var state = await Client.CreateAsync(new(source, _source[0] == 'd', _seconds), token);
            if (!token.IsCancellationRequested) Nav.NavigateTo($"/videnstjek/live/{state.Code}/vaert");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) when (IsRoomError(ex))
        {
            if (!token.IsCancellationRequested) _error = ErrorMessage(ex);
        }
        finally
        {
            if (!token.IsCancellationRequested) _busy = false;
        }
    }

    private async Task RetryRoomAsync()
    {
        if (_loading || !CanHost || Code is not { } code || !LiveQuiz.IsCode(code)) return;

        var token = _scope.Token;
        _loading = true;
        _error = null;
        try
        {
            var state = await Client.GetAsync(code, cancellationToken: token);
            if (token.IsCancellationRequested) return;
            if (!state.IsHost)
            {
                _error = LoadError;
                return;
            }

            Show(state);
            if (state.Phase != LiveQuiz.Finished)
                _ = PollAsync(code, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) when (IsRoomError(ex))
        {
            if (!token.IsCancellationRequested) _error = IsRoomUnavailable(ex) ? LoadError : ErrorMessage(ex);
        }
        finally
        {
            if (!token.IsCancellationRequested) _loading = false;
        }
    }

    private async Task PollAsync(string code, CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                if (!CanHost || Game?.Phase == LiveQuiz.Finished) return;
                if (_busy) continue;
                var before = _commandVersion;
                try
                {
                    var state = await Client.GetAsync(code, cancellationToken: token);
                    if (token.IsCancellationRequested || _busy || before != _commandVersion) continue;
                    if (!state.IsHost)
                    {
                        Game = null;
                        _error = LoadError;
                        await InvokeAsync(StateHasChanged);
                        return;
                    }

                    _connectionError = false;
                    Show(state);
                }
                catch (Exception ex) when (IsRoomError(ex))
                {
                    if (token.IsCancellationRequested || _busy || before != _commandVersion) continue;
                    if (IsRoomUnavailable(ex))
                    {
                        Game = null;
                        _error = LoadError;
                        await InvokeAsync(StateHasChanged);
                        return;
                    }

                    _connectionError = true;
                }

                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private Task AdvanceAsync() => Game is { } game
        ? CommandAsync((code, token) => Client.AdvanceAsync(code, new(game.Phase, game.QuestionIndex), token))
        : Task.CompletedTask;

    private Task ToggleLockAsync() => Game is { } game
        ? CommandAsync((code, token) => Client.LockAsync(code, new(!game.Locked), token))
        : Task.CompletedTask;

    private Task KickAsync(Guid playerId) =>
        CommandAsync((code, token) => Client.KickAsync(code, new(playerId), token));

    private async Task EndAsync()
    {
        if (await CommandAsync((code, token) => Client.EndAsync(code, token)))
            _confirmEnd = false;
    }

    private async Task<bool> CommandAsync(Func<string, CancellationToken, Task<LiveQuizState>> send)
    {
        if (_busy || !CanHost || Game is not { } game) return false;

        var token = _scope.Token;
        _commandVersion++;
        _busy = true;
        _error = null;
        try
        {
            var next = await send(game.Code, token);
            if (token.IsCancellationRequested) return false;

            Show(next);
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return false; }
        catch (LiveQuizException ex) when (ex.Code == "live_stale_state")
        {
            // The next poll will show the authoritative phase and prevent advancing twice.
            return false;
        }
        catch (Exception ex) when (IsRoomError(ex))
        {
            if (!token.IsCancellationRequested) _error = IsRoomUnavailable(ex) ? LoadError : ErrorMessage(ex);
            return false;
        }
        finally
        {
            if (!token.IsCancellationRequested) _busy = false;
        }
    }

    private void Show(LiveQuizState state)
    {
        if (Game is { } previous && (previous.Phase != state.Phase || previous.QuestionIndex != state.QuestionIndex))
        {
            _focusPhase = true;
            if (_soundOn) _soundCue = state.Phase;
            _announcement = state.Phase switch
            {
                LiveQuiz.Question => $"Spørgsmål {state.QuestionIndex + 1} af {state.QuestionCount}",
                LiveQuiz.Reveal => "Facit vises",
                LiveQuiz.Leaderboard => "Pointtavlen vises",
                LiveQuiz.Finished => "Spillet er afsluttet",
                _ => ""
            };
        }
        Game = state;
        _deadline = state.DeadlineUtc is { } serverDeadline
            ? DateTimeOffset.UtcNow + (serverDeadline - state.ServerNowUtc)
            : null;
    }

    private async Task CopyInviteAsync()
    {
        var token = _scope.Token;
        _error = null;
        _linkCopied = false;
        var copied = false;
        try
        {
            copied = await JS.InvokeAsync<bool>("lupLive.copy", token, Nav.ToAbsoluteUri(JoinPath).AbsoluteUri);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
        catch (JSException) { }

        if (token.IsCancellationRequested) return;
        _linkCopied = copied;
        if (copied) _announcement = "Link kopieret";
        else _error = "Kunne ikke kopiere. Åbn deltagerlinket, og kopiér adressen.";
    }

    private async Task ToggleSoundAsync()
    {
        if (_changingSound) return;

        _changingSound = true;
        _error = null;
        var enable = !_soundOn;
        try
        {
            _soundOn = await JS.InvokeAsync<bool>("lupLive.enableSound", enable);
            if (enable && !_soundOn)
                _error = "Spillyd er ikke tilgængelig i denne browser.";
        }
        catch (JSException)
        {
            _error = "Spillyd er ikke tilgængelig i denne browser.";
        }
        finally
        {
            _changingSound = false;
        }
    }

    private async Task ToggleFullscreenAsync()
    {
        var token = _scope.Token;
        _error = null;
        var changed = false;
        try { changed = await JS.InvokeAsync<bool>("lupLive.fullscreen", token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
        catch (JSException) { }

        if (!changed && !token.IsCancellationRequested)
            _error = "Fuld skærm er ikke tilgængelig i denne browser.";
    }

    private static bool IsRoomError(Exception ex) => ex is LiveQuizException or HttpRequestException or JsonException;
    private static bool IsRoomUnavailable(Exception ex) => ex is LiveQuizException
        { Status: HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Gone };
    private static string ErrorMessage(Exception ex) => ex is LiveQuizException
        ? ex.Message
        : "Forbindelsen til spillet svarede ikke. Prøv igen.";
    private static string SourceKey(Guid id, bool fromDraft) => (fromDraft ? "d" : "p") + id;
    private static string PlayerLabel(int count) => count == 1 ? "1 deltager" : $"{count} deltagere";
    private static string AnswerLabel(int count) => count == 1 ? "1 svar" : $"{count} svar";

    public void Dispose()
    {
        _scope.Cancel();
        _scope.Dispose();
    }
}
