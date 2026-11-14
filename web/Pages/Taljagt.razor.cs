using System.Globalization;
using System.Net;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Pages;

public partial class Taljagt
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private static readonly (string Token, string Symbol, string Label)[] Operators =
    [
        ("+", "+", "Plus"),
        ("-", "−", "Minus"),
        ("*", "×", "Gange"),
        ("/", "÷", "Division"),
        ("(", "(", "Start parentes"),
        (")", ")", "Slut parentes")
    ];

    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<string> _tokens = [];
    private DailyGameDay? _day;
    private DailyGameRound? _round;
    private DailyGameMove? _pending;
    private string? _subject;
    private string? _notice;
    private int _generation;
    private int? _draftValue;
    private ElementReference _resultHeading;
    private bool _focusResult;
    private bool _loginRequired;
    private bool _submitting;
    private bool _disposed;

    private bool CanPlay => Auth.IsAuthenticated && !_loginRequired;
    private int? DraftValue => _draftValue;
    private bool CanSubmit => CanPlay && !_submitting && DraftValue is not null;
    private bool NeedsNumber => _tokens.Count == 0 || _tokens[^1] is "(" or "+" or "-" or "*" or "/";
    private bool HasOpenParenthesis => _tokens.Count(token => token == "(") > _tokens.Count(token => token == ")");

    private string? DraftFeedback
    {
        get
        {
            if (_draftValue is not { } value || _day?.Target is not { } target)
                return _tokens.Count >= 3 && !NeedsNumber && !HasOpenParenthesis
                    ? "Regnestykket skal bruge mindst to tal og give hele, ikke-negative tal undervejs."
                    : null;

            var distance = Math.Abs(value - target);
            return distance == 0 ? "Du har ramt måltallet." : $"{distance.ToString(Danish)} fra måltallet.";
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _subject = Auth.Subject;
        Auth.AuthStateChanged += OnAuthChanged;
        await LoadAsync();
        if (!_disposed) _ = WatchDayAsync(_lifetime.Token);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focusResult) return;

        _focusResult = false;
        if (_round is { Status: not DailyGameStatus.Playing })
            await _resultHeading.FocusAsync(preventScroll: true);
    }

    private async Task LoadAsync()
    {
        var generation = ++_generation;
        var subject = Auth.Subject;
        var ct = _lifetime.Token;
        _notice = null;
        try
        {
            if (CanPlay)
            {
                var round = await Games.GetRoundAsync(DailyGameIds.Numbers, ct);
                if (IsCurrent(generation, subject)) SetRound(round);
            }
            else
            {
                var day = await Games.GetDayAsync(DailyGameIds.Numbers, ct);
                if (IsCurrent(generation, subject)) SetDay(day);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            if (!IsCurrent(generation, subject)) return;
            if (CanPlay && ex is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized })
            {
                RequireLogin();
                await LoadAsync();
            }
            else
                _notice = "Dagens Taljagt kunne ikke hentes. Prøv igen.";
        }
    }

    private async Task WatchDayAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var untilChange = _day?.NextRoundAt - DateTimeOffset.UtcNow;
                var delay = untilChange is { TotalSeconds: > 0 and < 60 }
                    ? untilChange.Value + TimeSpan.FromMilliseconds(250)
                    : TimeSpan.FromMinutes(1);
                await Task.Delay(delay, ct);
                await InvokeAsync(async () =>
                {
                    if (_disposed || _submitting || _day is null || _day.NextRoundAt > DateTimeOffset.UtcNow)
                        return;
                    await LoadAsync();
                    if (!_disposed) StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private void OnAuthChanged()
    {
        if (_disposed || _subject == Auth.Subject) return;
        _subject = Auth.Subject;
        _ = InvokeAsync(async () =>
        {
            if (_disposed) return;
            _round = null;
            _submitting = false;
            _loginRequired = false;
            Clear();
            StateHasChanged();
            await LoadAsync();
            if (!_disposed) StateHasChanged();
        });
    }

    private async Task SubmitAsync()
    {
        if (!CanSubmit || Auth.Subject is not { } subject || _round is not { Status: DailyGameStatus.Playing } round)
            return;

        var generation = _generation;
        var ct = _lifetime.Token;
        _pending ??= new DailyGameMove(round.Day.Date, round.Version, Guid.NewGuid(), Tokens: _tokens.ToArray());
        _submitting = true;
        _notice = null;

        try
        {
            var result = await Games.MoveAsync(DailyGameIds.Numbers, "submit", _pending, subject, ct);
            if (!IsCurrent(generation, subject)) return;

            _pending = null;
            SetRound(result);
            _focusResult = result.Status != DailyGameStatus.Playing;
            _notice = result.Error switch
            {
                "invalid_expression" => "Regnestykket kunne ikke godkendes. Se spillereglerne, og ret dit regnestykke.",
                "round_changed" => "Runden blev opdateret fra en anden fane eller enhed.",
                "day_changed" => "En ny dag er begyndt. Her er dagens Taljagt.",
                null => null,
                _ => "Resultatet kunne ikke gemmes. Prøv igen."
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            if (!IsCurrent(generation, subject)) return;
            if (ex is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized })
                RequireLogin();
            else
                _notice = "Afleveringen kunne ikke bekræftes. Prøv igen med samme regnestykke.";
        }
        finally
        {
            if (IsCurrent(generation, subject)) _submitting = false;
        }
    }

    private void RequireLogin()
    {
        _loginRequired = true;
        _round = null;
        Clear();
    }

    private bool IsCurrent(int generation, string? subject) =>
        !_disposed && generation == _generation && subject == Auth.Subject;

    private void SetDay(DailyGameDay day)
    {
        if (_day?.Date != day.Date) Clear();
        _day = day;
    }

    private void SetRound(DailyGameRound round)
    {
        SetDay(round.Day);
        _round = round;
        if (round.Status != DailyGameStatus.Playing) Clear();
    }

    private bool CanAddNumber(string token) =>
        !_submitting && _tokens.Count < DailyNumberExpression.MaxTokens && NeedsNumber && !_tokens.Contains(token);

    private bool CanAddOperator(string token) =>
        !_submitting && _tokens.Count < DailyNumberExpression.MaxTokens && (token switch
        {
            "(" => NeedsNumber,
            ")" => !NeedsNumber && HasOpenParenthesis,
            _ => !NeedsNumber
        });

    private void AddToken(string token)
    {
        if (!(DailyNumberExpression.IsTileToken(token) ? CanAddNumber(token) : CanAddOperator(token))) return;
        _tokens.Add(token);
        DraftChanged();
    }

    private void Undo()
    {
        if (_submitting || _tokens.Count == 0) return;
        _tokens.RemoveAt(_tokens.Count - 1);
        DraftChanged();
    }

    private void Clear()
    {
        _tokens.Clear();
        DraftChanged();
    }

    private void DraftChanged()
    {
        _pending = null;
        _notice = null;
        _draftValue = _day?.Tiles is { } tiles && DailyNumberExpression.TryEvaluate(tiles, _tokens, out var value, out _)
            ? value : null;
    }

    private string DisplayExpression(IReadOnlyList<int> tiles) => ReadableExpression(
        string.Join(" ", _tokens.Select(token => DailyNumberExpression.IsTileToken(token)
            ? tiles[token[0] - '0'].ToString(Danish) : token)));

    private static string ReadableExpression(string expression) =>
        expression.Replace('*', '×').Replace('/', '÷').Replace('-', '−');

    private static string DaysLabel(int days) => days == 1 ? "1 dag" : $"{days} dage";

    public void Dispose()
    {
        _disposed = true;
        Auth.AuthStateChanged -= OnAuthChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
