using System.Globalization;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Calendar;
using Mercantec.Tech.Web.Learning;
using Mercantec.Tech.Web.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Components;

public partial class PersonalCalendar
{
    [Parameter] public bool Teacher { get; set; }

    private const int MaxDuration = 31 * 24 * 60;
    private const int HourHeight = 64;
    private const int MinimumBlockHeight = 58;
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");

    private readonly CancellationTokenSource _life = new();
    private CancellationTokenSource? _holdRequest, _outlookRequest;
    private (string? Subject, bool CanRead, bool Teacher, bool Preview, Guid? AsUser)? _context;
    private DateOnly _anchor = Today;
    private HoldCalendarSource[] _holds = [];
    private HoldCalendarEventView[] _activities = [];
    private CalendarQuestDeadline[] _deadlines = [];
    private CalendarQuestChoice[] _choices = [];
    private TeacherCalendarEvent[] _outlookEvents = [];
    private string _holdFilter = "";
    private string? _loadError, _outlookError, _mutationError, _notice, _overlap;
    private bool _loading, _outlookLoading, _busy, _disposed, _focusEditor, _stale;
    private bool _showOutlook = true;
    private CalendarDraft? _draft, _baseline, _overlapDraft;
    private HoldCalendarEventView? _dragged;
    private CalendarDraft? _deleting;
    private ElementReference _titleInput;

    private bool CanRead => Auth.IsInitialized && Auth.IsAuthenticated &&
        (Teacher ? Redirect.ShowsTeacherChrome : !Redirect.ShowsTeacherChrome);
    private bool CanEdit => Teacher && CanRead && !ViewAs.IsActive;
    private bool CanShowOutlook => CanEdit;
    private bool CanCreate => CanEdit && _holds.Any(hold => hold.CanManage);
    private IEnumerable<HoldCalendarSource> ManageableHolds => _holds.Where(hold => hold.CanManage);
    private bool CanManage(Guid holdId) => CanEdit && _holds.Any(hold => hold.HoldId == holdId && hold.CanManage);
    private bool IsDirty => _draft is not null && _draft != _baseline;
    private bool ConfirmOverlap => _overlap is not null && _draft == _overlapDraft;
    private static DateOnly Today => LocalDay(DateTimeOffset.UtcNow);
    private static DateOnly LocalDay(DateTimeOffset value) => DateOnly.FromDateTime(HoldTime.Local(value));
    private static DateOnly Monday(DateOnly date) => date.AddDays(-((int)date.DayOfWeek + 6) % 7);
    private DateOnly PeriodStart => Teacher ? Monday(_anchor) : _anchor;
    private int PeriodLength => Teacher ? 7 : 1;
    private string PeriodLabel => Teacher
        ? $"{PeriodStart.ToString("d. MMM", Danish)} – {PeriodStart.AddDays(6).ToString("d. MMM yyyy", Danish)}"
        : _anchor.ToString("dddd d. MMMM yyyy", Danish);

    protected override void OnInitialized()
    {
        Auth.AuthStateChanged += ContextChanged;
        ViewAs.Changed += ContextChanged;
    }

    protected override Task OnParametersSetAsync() => UpdateContext();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusEditor && _draft is not null)
        {
            _focusEditor = false;
            await _titleInput.FocusAsync();
        }
    }

    private void ContextChanged() => _ = InvokeAsync(async () =>
    {
        var load = UpdateContext();
        StateHasChanged();
        await load;
        if (!_disposed) StateHasChanged();
    });

    private Task UpdateContext()
    {
        var context = (Auth.Subject, CanRead, Teacher, ViewAs.IsActive, ViewAs.AuthSub);
        if (_context == context) return Task.CompletedTask;
        _context = context;
        _holdFilter = "";
        _holds = [];
        _choices = [];
        _notice = null;
        ClearEditor();
        _dragged = null;
        _busy = false;
        return LoadPeriod();
    }

    private Task Move(int direction) => ChangeAnchor(_anchor.AddDays(direction * PeriodLength));

    private Task ChangeAnchor(DateOnly day)
    {
        if (_busy) return Task.CompletedTask;
        var previousPeriod = PeriodStart;
        _anchor = day;
        if (PeriodStart == previousPeriod) return Task.CompletedTask;
        _notice = null;
        _overlapDraft = null;
        return LoadPeriod();
    }

    private Task LoadPeriod()
    {
        _holdRequest?.Cancel();
        _outlookRequest?.Cancel();
        _activities = [];
        _deadlines = [];
        _outlookEvents = [];
        _loadError = _outlookError = null;
        _loading = _outlookLoading = false;
        if (!CanRead || ViewAs.IsActive && ViewAs.AuthSub is null)
        {
            _holds = [];
            _choices = [];
            return Task.CompletedTask;
        }

        return Task.WhenAll(RetryHoldPlan(), RetryOutlook());
    }

    private async Task RetryHoldPlan()
    {
        _holdRequest?.Cancel();
        var request = _holdRequest = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
        var start = PeriodStart;
        var end = start.AddDays(Teacher ? PeriodLength : 8);
        _loading = true;
        _loadError = null;
        try
        {
            var plan = await Learning.CalendarOverview(LocalBoundary(start), LocalBoundary(end),
                ViewAs.IsActive ? ViewAs.AuthSub : null, request.Token);
            if (request.IsCancellationRequested) return;
            _holds = plan.Holds;
            _activities = plan.Events;
            _deadlines = plan.Deadlines;
            _choices = plan.QuestChoices;
            if (_holdFilter.Length > 0 && !_holds.Any(hold => hold.HoldId.ToString() == _holdFilter))
                _holdFilter = "";
        }
        catch (Exception ex) when (ex is LearningException or HttpRequestException or OperationCanceledException)
        {
            if (!request.IsCancellationRequested)
                _loadError = ex is LearningException ? ex.Message : "Undervisning og quests kunne ikke hentes.";
        }
        finally
        {
            if (_holdRequest == request)
            {
                _holdRequest = null;
                _loading = false;
                if (!_disposed) StateHasChanged();
            }
            request.Dispose();
        }
    }

    private async Task RetryOutlook()
    {
        _outlookRequest?.Cancel();
        if (!CanShowOutlook || !_showOutlook) return;

        var request = _outlookRequest = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
        var start = PeriodStart;
        _outlookLoading = true;
        _outlookError = null;
        try
        {
            var result = await Outlook.MineAsync(LocalBoundary(start), LocalBoundary(start.AddDays(PeriodLength)), request.Token);
            if (request.IsCancellationRequested) return;
            _outlookEvents = result.Events.Where(item => !item.IsCancelled).ToArray();
            if (!result.Configured)
                _outlookError = "Din Outlook-kalender er endnu ikke koblet til LUP.";
        }
        catch (Exception ex) when (ex is TeacherCalendarException or HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
        {
            if (!request.IsCancellationRequested)
                _outlookError = ex is TeacherCalendarException ? ex.Message : "Outlook kunne ikke hentes. LUP-planen vises stadig.";
        }
        finally
        {
            if (_outlookRequest == request)
            {
                _outlookRequest = null;
                _outlookLoading = false;
                if (!_disposed) StateHasChanged();
            }
            request.Dispose();
        }
    }

    private async Task ToggleOutlook(ChangeEventArgs args)
    {
        _showOutlook = args.Value is true;
        _overlapDraft = null;
        if (_showOutlook)
            await RetryOutlook();
        else
        {
            _outlookRequest?.Cancel();
            _outlookEvents = [];
            _outlookError = null;
            _outlookLoading = false;
        }
    }

    private const string DiscardMessage = "Du har ugemte ændringer i aktiviteten. Vil du kassere dem?";
    private async ValueTask<bool> CanReplaceDraft() => !IsDirty || await Js.InvokeAsync<bool>("confirm", DiscardMessage);
    private async Task BeforeNavigate(LocationChangingContext context)
    {
        if (!await CanReplaceDraft()) context.PreventNavigation();
    }

    private async Task NewActivity(DateOnly day)
    {
        if (!CanCreate || _busy || !await CanReplaceDraft()) return;
        var hold = ManageableHolds.FirstOrDefault(item => item.HoldId.ToString() == _holdFilter) ?? ManageableHolds.First();
        var draft = new CalendarDraft
        {
            HoldId = hold.HoldId,
            Start = DayInput(day, new TimeOnly(8, 0)),
            Duration = 60,
            AttendanceMode = "physical",
        };
        OpenEditor(draft, draft with { });
    }

    private async Task EditActivity(HoldCalendarEventView item, string? movedStart = null)
    {
        if (!CanManage(item.HoldId) || _busy || !await CanReplaceDraft()) return;
        var baseline = new CalendarDraft
        {
            Id = item.Id,
            HoldId = item.HoldId,
            Title = item.Title,
            Description = item.Description,
            Start = HoldTime.Input(item.StartUtc),
            Duration = Math.Clamp((int)Math.Round((item.EndUtc - item.StartUtc).TotalMinutes), 1, MaxDuration),
            Location = item.Location,
            Kind = item.Kind,
            AttendanceMode = item.AttendanceMode ?? "",
            QuestId = item.QuestId,
            Version = item.Version
        };
        OpenEditor(baseline with { Start = movedStart ?? baseline.Start }, baseline);
    }

    private void OpenEditor(CalendarDraft draft, CalendarDraft baseline)
    {
        ClearEditor();
        _notice = null;
        _draft = draft;
        _baseline = baseline;
        _focusEditor = true;
    }

    private void ClearEditor()
    {
        _draft = _baseline = _overlapDraft = _deleting = null;
        _mutationError = _overlap = null;
        _stale = _focusEditor = false;
    }

    private IEnumerable<CalendarQuestChoice> Choices(CalendarDraft draft)
    {
        var choices = _choices.Where(item => item.HoldId == draft.HoldId).ToArray();
        // An existing link remains editable if a previously published quest is archived.
        return draft.QuestId is Guid questId && choices.All(item => item.QuestId != questId)
            ? [.. choices, new CalendarQuestChoice(questId, draft.HoldId, "Tilknyttet quest")]
            : choices;
    }

    private void SelectDraftHold(ChangeEventArgs args)
    {
        if (_draft is not { } draft || !Guid.TryParse(args.Value?.ToString(), out var holdId) || !CanManage(holdId))
            return;
        draft.HoldId = holdId;
        draft.QuestId = null;
    }

    private void SelectDraftKind(ChangeEventArgs args)
    {
        if (_draft is not { } draft) return;
        draft.Kind = args.Value?.ToString() ?? "teaching";
        if (draft.Kind != "quest") draft.QuestId = null;
    }

    private void SelectDraftQuest(ChangeEventArgs args)
    {
        if (_draft is not { } draft) return;
        var quest = Guid.TryParse(args.Value?.ToString(), out var questId)
            ? Choices(draft).FirstOrDefault(item => item.QuestId == questId)
            : null;
        draft.QuestId = quest?.QuestId;
        if (quest is not null && string.IsNullOrWhiteSpace(draft.Title))
            draft.Title = quest.Title;
    }

    private void BeginDrag(HoldCalendarEventView? item)
    {
        if (!_busy && item is not null && CanManage(item.HoldId)) _dragged = item;
    }

    private void EndDrag() => _dragged = null;

    private async Task Drop(DateOnly day, int? hour)
    {
        var item = _dragged;
        EndDrag();
        if (item is null) return;
        var time = hour is int targetHour ? new TimeOnly(targetHour, 0) : TimeOnly.FromDateTime(HoldTime.Local(item.StartUtc));
        var start = DayInput(day, time);
        if (start != HoldTime.Input(item.StartUtc))
            await EditActivity(item, start);
    }

    private async Task SaveActivity()
    {
        if (_draft is not { } draft || _busy || _stale || !CanManage(draft.HoldId)) return;
        _mutationError = null;
        if (string.IsNullOrWhiteSpace(draft.Title) || draft.Kind == "quest" && draft.QuestId is null)
        {
            _mutationError = "Udfyld en titel, og vælg en quest, hvis du planlægger en quest.";
            return;
        }
        if (!HoldTime.TryUtc(draft.Start, out var start) || draft.Duration is < 1 or > MaxDuration)
        {
            _mutationError = "Vælg et gyldigt dansk tidspunkt og en varighed på mindst 1 minut og højst 31 dage. Der kan ikke startes i en tvetydig time ved skift mellem sommer- og vintertid.";
            return;
        }

        var end = start.AddMinutes(draft.Duration);
        var context = _context;
        _busy = true;
        try
        {
            if (!ConfirmOverlap)
            {
                var overlap = await CheckOverlap(draft.Id, start, end);
                if (_disposed || _context != context) return;
                if (overlap is not null)
                {
                    _overlap = overlap;
                    _overlapDraft = draft with { };
                    return;
                }
            }

            var request = new SaveHoldCalendarEvent(draft.HoldId, draft.Title.Trim(), draft.Description, start, end,
                draft.Location, draft.Kind, draft.Version, EmptyToNull(draft.AttendanceMode), draft.QuestId);
            var saved = draft.Id is Guid id
                ? await Learning.UpdateEvent(id, request, _life.Token)
                : await Learning.CreateEvent(request, _life.Token);
            if (_disposed || _context != context) return;
            ClearEditor();
            var previousPeriod = PeriodStart;
            _anchor = LocalDay(saved.StartUtc);
            if (previousPeriod == PeriodStart)
            {
                _activities = [.. _activities.Where(activity => activity.Id != saved.Id), saved];
                await RetryHoldPlan();
            }
            else
            {
                await LoadPeriod();
            }
            if (!_disposed && _context == context)
                _notice = "Aktiviteten er gemt i holdets kalender.";
        }
        catch (Exception ex) when (ex is LearningException or HttpRequestException or OperationCanceledException)
        {
            if (_disposed || _context != context) return;
            _mutationError = ex is LearningException ? ex.Message : "Aktiviteten kunne ikke gemmes. Prøv igen.";
            _stale = ex is LearningException { Code: "calendar_event_changed" };
        }
        finally
        {
            if (_context == context) _busy = false;
        }
    }

    private async Task<string?> CheckOverlap(Guid? activityId, DateTimeOffset start, DateTimeOffset end)
    {
        var holdItems = _activities;
        var outlookItems = _outlookEvents;
        var uncheckedSources = new List<string>();
        if (start < LocalBoundary(PeriodStart) || end > LocalBoundary(PeriodStart.AddDays(PeriodLength)))
        {
            using var lookup = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
            lookup.CancelAfter(TimeSpan.FromSeconds(5));

            var holdCheck = Learning.CalendarOverview(start, end, null, lookup.Token);
            var outlookCheck = _showOutlook && _outlookError is null
                ? Outlook.MineAsync(start, end, lookup.Token)
                : null;

            try
            {
                holdItems = (await holdCheck).Events;
            }
            catch (Exception ex) when (ex is LearningException or HttpRequestException or OperationCanceledException)
            {
                uncheckedSources.Add("LUP-planen");
            }

            if (outlookCheck is not null)
            {
                try
                {
                    var outlookView = await outlookCheck;
                    outlookItems = outlookView.Events.Where(item => !item.IsCancelled).ToArray();
                }
                catch (Exception ex) when (ex is TeacherCalendarException or HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
                {
                    uncheckedSources.Add("Outlook");
                }
            }
        }

        var hold = holdItems.Any(item => item.Id != activityId && Overlaps(item.StartUtc, item.EndUtc, start, end));
        var outlook = _showOutlook && outlookItems.Any(item =>
        {
            if (string.Equals(item.ShowAs, "free", StringComparison.OrdinalIgnoreCase)) return false;
            var (from, to) = OutlookRange(item);
            return Overlaps(from, to, start, end);
        });
        var conflict = (hold, outlook) switch
        {
            (true, true) => "Aktiviteten overlapper en LUP-aktivitet og en Outlook-aftale.",
            (true, false) => "Aktiviteten overlapper en LUP-aktivitet.",
            (false, true) => "Aktiviteten overlapper en Outlook-aftale.",
            _ => null,
        };
        if (uncheckedSources.Count == 0) return conflict;
        var partial = $"Kunne ikke kontrollere overlap i {string.Join(" og ", uncheckedSources)}.";
        return conflict is null ? partial : $"{conflict} {partial}";
    }
    private async Task OpenLatest(Guid id)
    {
        await RetryHoldPlan();
        if (_disposed || _draft?.Id != id) return;
        if (_activities.FirstOrDefault(activity => activity.Id == id) is { } latest)
            await EditActivity(latest);
        else if (_loadError is null)
            _mutationError = "Aktiviteten er flyttet uden for den viste uge eller slettet. Luk formularen, og vælg aktiviteten i kalenderen.";
    }

    private void AskDelete()
    {
        _deleting = _draft;
        _mutationError = null;
    }

    private void CancelDelete()
    {
        _deleting = null;
        _mutationError = null;
    }

    private async Task DeleteActivity()
    {
        if (_deleting is not { Id: Guid id } draft || _busy || !CanManage(draft.HoldId)) return;
        var context = _context;
        _busy = true;
        try
        {
            await Learning.DeleteEvent(draft.HoldId, id, draft.Version, _life.Token);
            if (_disposed || _context != context) return;
            _activities = _activities.Where(activity => activity.Id != id).ToArray();
            ClearEditor();
            _notice = "Aktiviteten er slettet.";
        }
        catch (Exception ex) when (ex is LearningException or HttpRequestException or OperationCanceledException)
        {
            if (_disposed || _context != context) return;
            _mutationError = ex is LearningException ? ex.Message : "Aktiviteten kunne ikke slettes. Prøv igen.";
            if (ex is LearningException { Code: "calendar_event_changed" })
            {
                _deleting = null;
                _stale = true;
            }
        }
        finally
        {
            if (_context == context) _busy = false;
        }
    }

    private IEnumerable<(CalendarQuestDeadline Deadline, HoldCalendarSource Hold)> UpcomingDeadlines()
    {
        var firstDay = LocalBoundary(_anchor.AddDays(1));
        var to = LocalBoundary(_anchor.AddDays(8));
        return from deadline in _deadlines
               where deadline.DeadlineUtc >= firstDay && deadline.DeadlineUtc < to
               from hold in _holds
               where hold.HoldId == deadline.HoldId && (_holdFilter.Length == 0 || hold.HoldId.ToString() == _holdFilter)
               orderby deadline.DeadlineUtc
               select (deadline, hold);
    }

    private IReadOnlyList<PlanDay> BuildDays()
    {
        var sources = _holds.Where(hold => _holdFilter.Length == 0 || hold.HoldId.ToString() == _holdFilter)
            .ToDictionary(hold => hold.HoldId);
        var activities = _activities.Where(item => sources.ContainsKey(item.HoldId)).Select(item =>
        {
            var hold = sources[item.HoldId];
            return new PlanBlock("activity", item.Id.ToString(), item.Title, item.StartUtc, item.EndUtc,
                false, hold.Name, item.Location, item.AttendanceMode, item.QuestId is Guid questId ? QuestHref(questId, hold) : null, item);
        });
        var deadlines = _deadlines.Where(item => sources.ContainsKey(item.HoldId)).Select(item =>
        {
            var hold = sources[item.HoldId];
            return new PlanBlock("deadline", item.QuestId.ToString(), item.Title, item.DeadlineUtc, item.DeadlineUtc,
                false, hold.Name, null, null, QuestHref(item.QuestId, hold), null);
        });
        var outlook = _showOutlook && CanShowOutlook ? _outlookEvents.Select(item =>
        {
            var (from, to) = OutlookRange(item);
            return new PlanBlock("outlook", item.Id, item.IsPrivate ? "Privat aftale" : item.Subject, from, to,
                item.IsAllDay, null, item.IsPrivate ? null : item.Location, null, item.IsPrivate ? null : SafeExternal(item.OnlineMeetingUrl) ?? SafeExternal(item.WebLink), null);
        }) : [];
        var blocks = activities.Concat(deadlines).Concat(outlook).OrderBy(item => item.StartUtc).ThenBy(item => item.EndUtc).ToArray();

        return Enumerable.Range(0, PeriodLength).Select(offset =>
        {
            var day = PeriodStart.AddDays(offset);
            var start = LocalBoundary(day);
            var end = LocalBoundary(day.AddDays(1));
            return new PlanDay(day, blocks.Where(block => block.Source == "deadline"
                ? block.StartUtc >= start && block.StartUtc < end
                : Overlaps(block.StartUtc, block.EndUtc, start, end)).ToArray());
        }).ToArray();
    }

    private static (int First, int Last) HourRange(IReadOnlyList<PlanDay> days)
    {
        var first = 7;
        var last = 18;
        foreach (var day in days)
        foreach (var block in day.Blocks.Where(block => !block.AllDay))
        {
            var (start, end) = MinutesOnDay(day.Date, block);
            first = Math.Min(first, start / 60);
            last = Math.Max(last, (Math.Max(start + 1, end) + 59) / 60);
        }
        return (first, Math.Min(last, 24));
    }

    private static IEnumerable<BlockPlacement> PlaceBlocks(PlanDay day, int firstHour)
    {
        var laneEnd = new List<int>();
        var group = new List<(PlanBlock Block, int Top, int Height, int Lane)>();
        foreach (var block in day.Blocks.Where(block => !block.AllDay))
        {
            var (start, end) = MinutesOnDay(day.Date, block);
            var top = (start - firstHour * 60) * HourHeight / 60;
            var height = Math.Max(MinimumBlockHeight, (end - start) * HourHeight / 60 - 2);
            if (laneEnd.Count > 0 && laneEnd.All(lane => lane <= top))
            {
                foreach (var item in group)
                    yield return new BlockPlacement(item.Block, item.Top, item.Height, item.Lane, laneEnd.Count);
                group.Clear();
                laneEnd.Clear();
            }
            var lane = laneEnd.FindIndex(occupiedUntil => occupiedUntil <= top);
            if (lane < 0)
            {
                lane = laneEnd.Count;
                laneEnd.Add(0);
            }
            laneEnd[lane] = top + height + 2;
            group.Add((block, top, height, lane));
        }
        foreach (var item in group)
            yield return new BlockPlacement(item.Block, item.Top, item.Height, item.Lane, laneEnd.Count);
    }

    private static (int Start, int End) MinutesOnDay(DateOnly day, PlanBlock block)
    {
        var start = HoldTime.Local(block.StartUtc);
        var end = HoldTime.Local(block.EndUtc);
        return (DateOnly.FromDateTime(start) < day ? 0 : start.Hour * 60 + start.Minute,
            DateOnly.FromDateTime(end) > day ? 24 * 60 : end.Hour * 60 + end.Minute);
    }

    private static (DateTimeOffset Start, DateTimeOffset End) OutlookRange(TeacherCalendarEvent item) => item.IsAllDay
        ? (LocalBoundary(DateOnly.FromDateTime(item.Start.UtcDateTime)), LocalBoundary(DateOnly.FromDateTime(item.End.UtcDateTime)))
        : (item.Start, item.End);
    private static bool Overlaps(DateTimeOffset start, DateTimeOffset end, DateTimeOffset from, DateTimeOffset to) => start < to && end > from;
    private static string QuestHref(Guid id, HoldCalendarSource hold) => WorkspaceNavigation.Quest(id, hold.ForloebId, holdId: hold.HoldId);
    private static string? SafeExternal(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri.AbsoluteUri : null;
    private static string DayInput(DateOnly day, TimeOnly time) => FormattableString.Invariant($"{day:yyyy-MM-dd}T{time:HH:mm}");
    private static DateTimeOffset LocalBoundary(DateOnly day) =>
        HoldTime.TryUtc(DayInput(day, TimeOnly.MinValue), out var utc) ? utc : throw new InvalidOperationException("Ugyldig dansk dagsgrænse.");
    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;
    private static string TimeLabel(PlanBlock block)
    {
        if (block.AllDay) return "Hele dagen";
        var start = HoldTime.Local(block.StartUtc);
        if (block.Source == "deadline") return start.ToString("HH:mm", Danish);
        var end = HoldTime.Local(block.EndUtc);
        return start.Date == end.Date ? $"{start:HH:mm}–{end:HH:mm}" : $"{start:d. MMM HH:mm}–{end:d. MMM HH:mm}";
    }

    private static string AttendanceLabel(string value) => value switch
    {
        "physical" => "Fuldt fremmøde",
        "online" => "Online fremmøde",
        "home" => "Arbejde hjemme",
        _ => value,
    };

    public void Dispose()
    {
        _disposed = true;
        Auth.AuthStateChanged -= ContextChanged;
        ViewAs.Changed -= ContextChanged;
        _life.Cancel();
        _life.Dispose();
    }

    private sealed record CalendarDraft
    {
        public Guid? Id { get; init; }
        public Guid HoldId { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Start { get; set; } = "";
        public int Duration { get; set; }
        public string Location { get; set; } = "";
        public string Kind { get; set; } = "teaching";
        public string AttendanceMode { get; set; } = "";
        public Guid? QuestId { get; set; }
        public int Version { get; init; }
    }

    private sealed record PlanDay(DateOnly Date, IReadOnlyList<PlanBlock> Blocks);
    private sealed record PlanBlock(string Source, string Id, string Title, DateTimeOffset StartUtc, DateTimeOffset EndUtc,
        bool AllDay, string? HoldName, string? Location, string? AttendanceMode, string? Href, HoldCalendarEventView? Activity)
    {
        public string Key => Source + ":" + Id;
    }
    private sealed record BlockPlacement(PlanBlock Block, int Top, int Height, int Lane, int LaneCount)
    {
        public string Css => FormattableString.Invariant($"--pc-top:{Top}px;--pc-height:{Height}px;--pc-left:{100d * Lane / LaneCount}%;--pc-width:{100d / LaneCount}%");
    }
}
