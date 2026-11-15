using System.Globalization;
using System.Net;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Pages;

public partial class DailyWord
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private readonly CancellationTokenSource _lifetime = new();
    private DailyGameDay? _day;
    private DailyGameRound? _round;
    private ElementReference _entry;
    private ElementReference _resultHeading;
    private (string Action, DailyGameMove Move)? _pending;
    private string? _subject;
    private string? _error;
    private string? _feedback;
    private string _input = "";
    private int _generation;
    private bool _loginRequired;
    private bool _busy;
    private bool _focusEntry;
    private bool _disposed;

    private bool CanPlay => Auth.IsAuthenticated && !_loginRequired;
    private DailyGameDay? Puzzle => CanPlay ? _round?.Day ?? _day : _day;

    protected override async Task OnInitializedAsync()
    {
        _subject = Auth.Subject;
        Auth.AuthStateChanged += OnAccountChanged;
        await LoadAsync();
        if (!_disposed) _ = RefreshAtMidnightAsync(_lifetime.Token);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focusEntry) return;
        _focusEntry = false;
        if (!CanPlay || _round is null) return;
        if (_round.Status == DailyGameStatus.Playing)
            await _entry.FocusAsync(preventScroll: true);
        else
            await _resultHeading.FocusAsync(preventScroll: true);
    }

    private async Task LoadAsync()
    {
        if (_disposed) return;
        var generation = ++_generation;
        var subject = Auth.Subject;
        var ct = _lifetime.Token;
        _error = null;
        try
        {
            if (CanPlay)
            {
                var round = await Games.GetRoundAsync(DailyGameIds.Word, ct);
                if (IsCurrent(generation, subject)) SetRound(round);
            }
            else
            {
                var day = await Games.GetDayAsync(DailyGameIds.Word, ct);
                if (IsCurrent(generation, subject)) _day = day;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            if (!IsCurrent(generation, subject)) return;
            if (ex is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized } && CanPlay)
            {
                ResetPlay();
                _loginRequired = true;
                await LoadAsync();
            }
            else _error = "Dagens ord kunne ikke hentes. Prøv igen.";
        }
    }

    private async Task RefreshAtMidnightAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var untilNextRound = Puzzle?.NextRoundAt - DateTimeOffset.UtcNow;
                var delay = untilNextRound is { TotalMilliseconds: > 0 and < 60_000 }
                    ? untilNextRound.Value + TimeSpan.FromMilliseconds(150)
                    : TimeSpan.FromMinutes(1);
                await Task.Delay(delay, ct);
                await InvokeAsync(async () =>
                {
                    if (_disposed || _busy || Puzzle is null || Puzzle.NextRoundAt > DateTimeOffset.UtcNow) return;
                    await LoadAsync();
                    if (!_disposed) StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private Task SubmitAsync() => string.IsNullOrWhiteSpace(_input)
        ? Task.CompletedTask
        : MoveAsync("guess", _input.Trim());

    private Task RevealAsync() => MoveAsync("hint");

    private async Task MoveAsync(string action, string? word = null)
    {
        if (_disposed || !CanPlay || _busy || Auth.Subject is not { } subject
            || _round is not { Status: DailyGameStatus.Playing } round
            || (action == "hint" && round.Hints.Count >= 2)) return;

        var generation = _generation;
        var ct = _lifetime.Token;
        var move = _pending is { } pending && pending.Action == action && pending.Move.Word == word
            && pending.Move.Date == round.Day.Date && pending.Move.Version == round.Version
            ? pending.Move
            : new DailyGameMove(round.Day.Date, round.Version, Guid.NewGuid(), word);
        _pending = (action, move);
        _busy = true;
        _error = null;
        _feedback = null;
        try
        {
            var result = await Games.MoveAsync(DailyGameIds.Word, action, move, subject, ct);
            if (!IsCurrent(generation, subject)) return;
            _pending = null;
            SetRound(result);
            _error = result.Error switch
            {
                null => null,
                "day_changed" => "En ny dag er begyndt. Her er dagens ledetråd.",
                "round_changed" => "Runden er opdateret fra en anden fane eller enhed.",
                "invalid_word" => "Brug 2–32 tegn. Du kan bruge bogstaver, mellemrum og bindestreger.",
                "hints_exhausted" => "Du har åbnet begge ekstra ledetråde.",
                _ => "Handlingen kunne ikke gennemføres. Prøv igen."
            };
            if (result.Error is not null) return;

            if (action == "guess")
            {
                _input = "";
                _feedback = result.Status switch
                {
                    DailyGameStatus.Won => "Rigtigt gættet!",
                    DailyGameStatus.Lost => "Runden er slut. Du kan nu se dagens ord.",
                    _ => "Dit gæt var forkert. Prøv igen eller åbn en ekstra ledetråd."
                };
                _focusEntry = true;
            }
            else if (result.Hints.Count > round.Hints.Count)
            {
                _feedback = $"Ekstra ledetråd {result.Hints.Count}. {result.Hints[^1]}";
                _focusEntry = result.Hints.Count == 2;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            if (!IsCurrent(generation, subject)) return;
            if (ex is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized })
            {
                ResetPlay();
                _loginRequired = true;
                await LoadAsync();
            }
            else _error = "Handlingen kunne ikke bekræftes. Prøv igen.";
        }
        finally
        {
            if (IsCurrent(generation, subject)) _busy = false;
        }
    }

    private void SetRound(DailyGameRound round)
    {
        if (_day?.Date != round.Day.Date)
        {
            _input = "";
            _feedback = null;
            _pending = null;
        }
        // Keep the same public board while an account change is loading.
        _day = round.Day.WordPattern is { } pattern
            ? round.Day with { WordPattern = new string(pattern.Select(character => char.IsLetter(character) ? '_' : character).ToArray()) }
            : round.Day;
        _round = round;
    }

    private bool IsCurrent(int generation, string? subject) =>
        !_disposed && generation == _generation && subject == Auth.Subject;

    private void OnAccountChanged()
    {
        if (_disposed || _subject == Auth.Subject) return;
        _subject = Auth.Subject;
        _ = InvokeAsync(async () =>
        {
            if (_disposed) return;
            ResetPlay();
            _loginRequired = false;
            StateHasChanged();
            await LoadAsync();
            if (!_disposed) StateHasChanged();
        });
    }

    private void ResetPlay()
    {
        _pending = null;
        _round = null;
        _input = "";
        _feedback = null;
        _busy = false;
    }

    private static string DescribePattern(string pattern)
    {
        var letters = pattern.Where(character => character == '_' || char.IsLetter(character)).ToArray();
        var revealed = letters.Select((character, index) => (character, index))
            .Where(tile => char.IsLetter(tile.character))
            .Select(tile => $"Bogstav {tile.index + 1} er {char.ToUpperInvariant(tile.character)}")
            .ToArray();
        var description = $"{letters.Length} bogstaver.";
        if (pattern.IndexOfAny([' ', '-']) >= 0)
            description += " Feltgrupper med " + string.Join(" og ", pattern.Split([' ', '-']).Select(word => word.Length)) + " bogstaver.";
        return description + (revealed.Length == 0 ? " Ingen bogstaver er afsløret." : " " + string.Join(". ", revealed) + ".");
    }

    private static string DaysLabel(int days) => days == 1 ? "1 dag" : $"{days} dage";

    public void Dispose()
    {
        _disposed = true;
        Auth.AuthStateChanged -= OnAccountChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
