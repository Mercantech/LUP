using System.Diagnostics;
using System.Net;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.KnowledgeChecks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Pages.KnowledgeChecks;

public partial class LivePlayer
{
    [Parameter] public string? Code { get; set; }

    private readonly HashSet<int> _selected = [];
    private CancellationTokenSource _scope = new();
    private Task? _updates;
    private LiveQuizState? _state;
    private string? _lastRoute, _roomCode, _playerToken, _entryError;
    private string _typedCode = "", _nickname = "", _writtenAnswer = "";
    private bool _initialized, _restoring, _busy, _refreshing, _connectionError;
    private bool _focusNickname, _focusQuestion;
    private long _receivedAt;
    private int _mutations;
    private ElementReference _nicknameField, _questionHeading;

    private int SecondsLeft => _state is { DeadlineUtc: { } deadline } state
        ? Math.Clamp((int)Math.Ceiling((deadline - state.ServerNowUtc - Stopwatch.GetElapsedTime(_receivedAt)).TotalSeconds),
            0, state.QuestionSeconds)
        : 0;

    private bool CanAnswer => !_busy && SecondsLeft > 0
        && _state is { Phase: LiveQuiz.Question, Personal.HasAnswered: false };

    protected override async Task OnParametersSetAsync()
    {
        if (_initialized && _lastRoute == Code) return;
        _initialized = true;
        _lastRoute = Code;
        _scope.Cancel();
        _scope.Dispose();
        _scope = new();
        var ct = _scope.Token;

        _state = null;
        _playerToken = null;
        _roomCode = null;
        _typedCode = _nickname = _writtenAnswer = "";
        _entryError = null;
        _selected.Clear();
        _restoring = _busy = _refreshing = _connectionError = _focusNickname = _focusQuestion = false;
        _updates = null;

        if (!LiveQuiz.IsCode(Code))
        {
            if (Code is not null) _entryError = "PIN-koden skal bestå af seks cifre.";
            return;
        }

        _roomCode = Code;
        _restoring = true;
        try
        {
            var saved = await JS.InvokeAsync<string?>("lupLive.getPlayerToken", ct, _roomCode);
            if (ct.IsCancellationRequested || string.IsNullOrWhiteSpace(saved)) return;
            _playerToken = saved;
            await RefreshAsync(ct);
            if (!ct.IsCancellationRequested && _playerToken is not null) StartUpdates(ct);
        }
        catch (JSException)
        {
            // Private browsing can disable browsing-session storage.
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                _restoring = false;
                _focusNickname = _playerToken is null;
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusNickname && !_restoring && _state is null && _playerToken is null && _roomCode is not null)
        {
            _focusNickname = false;
            await FocusAsync(_nicknameField);
        }

        if (_focusQuestion && _state is { Phase: LiveQuiz.Question, Personal.HasAnswered: false })
        {
            _focusQuestion = false;
            await FocusAsync(_questionHeading);
        }
    }

    private static async Task FocusAsync(ElementReference field)
    {
        try { await field.FocusAsync(preventScroll: true); }
        catch (JSException) { }
    }

    private void OpenRoom()
    {
        var code = _typedCode.Trim();
        if (!LiveQuiz.IsCode(code))
        {
            _entryError = "Indtast de seks cifre fra tavlen.";
            return;
        }
        Navigation.NavigateTo($"/spil/{code}");
    }

    private async Task JoinAsync()
    {
        if (_busy || _roomCode is not { } code) return;
        var nickname = _nickname.Trim();
        if (nickname.Length == 0)
        {
            _entryError = "Vælg et kaldenavn.";
            return;
        }

        var ct = _scope.Token;
        _busy = true;
        _entryError = null;
        try
        {
            var result = await Client.JoinAsync(code, new(nickname), ct);
            if (ct.IsCancellationRequested) return;
            if (result.State.Personal is null)
            {
                _entryError = "Din plads i spillet kunne ikke åbnes. Prøv igen.";
                return;
            }
            _playerToken = result.PlayerToken;
            Apply(result.State);
            StartUpdates(ct);
            try { await JS.InvokeVoidAsync("lupLive.setPlayerToken", ct, code, result.PlayerToken); }
            catch (JSException) { }
        }
        catch (LiveQuizException error)
        {
            if (!ct.IsCancellationRequested) _entryError = error.Message;
        }
        catch (Exception error) when (IsTransportError(error))
        {
            if (!ct.IsCancellationRequested) _entryError = "Forbindelsen til spillet blev afbrudt. Prøv igen.";
        }
        finally
        {
            if (!ct.IsCancellationRequested) _busy = false;
        }
    }

    private Task ChooseAsync(int index, bool multiple)
    {
        if (!CanAnswer) return Task.CompletedTask;
        if (!multiple) return SendAnswerAsync([index], null);
        if (!_selected.Add(index)) _selected.Remove(index);
        return Task.CompletedTask;
    }

    private Task SubmitSelectionAsync()
    {
        if (!CanAnswer) return Task.CompletedTask;
        if (_state?.Question?.Kind == "short")
        {
            var text = _writtenAnswer.Trim();
            return text.Length == 0 ? Task.CompletedTask : SendAnswerAsync([], text);
        }
        return _selected.Count == 0 ? Task.CompletedTask : SendAnswerAsync(_selected.Order().ToArray(), null);
    }

    private async Task SendAnswerAsync(int[] options, string? text)
    {
        if (!CanAnswer || _state is not { } state || _playerToken is not { } token) return;

        var ct = _scope.Token;
        _mutations++;
        _busy = true;
        _entryError = null;
        try
        {
            var updated = await Client.AnswerAsync(state.Code, token, new(state.QuestionIndex, options, text), ct);
            if (!ct.IsCancellationRequested) Apply(updated);
        }
        catch (LiveQuizException error)
        {
            if (!ct.IsCancellationRequested)
            {
                if (IsInvalidParticipant(error)) await ExpireTokenAsync(state.Code, error.Message, ct);
                else _entryError = error.Message;
            }
        }
        catch (Exception error) when (IsTransportError(error))
        {
            if (!ct.IsCancellationRequested) _entryError = "Svaret kunne ikke bekræftes. Prøv igen, hvis tiden tillader det.";
        }
        finally
        {
            if (!ct.IsCancellationRequested) _busy = false;
        }
    }

    private void StartUpdates(CancellationToken ct)
    {
        if (_playerToken is { } token)
            _updates ??= Task.WhenAll(PollAsync(token, ct), DrawTimerAsync(token, ct));
    }

    private async Task PollAsync(string token, CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(ct) && _playerToken == token && _state?.Phase != LiveQuiz.Finished)
            {
                await InvokeAsync(async () =>
                {
                    if (_playerToken == token) await RefreshAsync(ct);
                    if (!ct.IsCancellationRequested) StateHasChanged();
                });
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private async Task DrawTimerAsync(string token, CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
            var displayedSeconds = -1;
            while (await timer.WaitForNextTickAsync(ct) && _playerToken == token && _state?.Phase != LiveQuiz.Finished)
            {
                if (_state?.Phase != LiveQuiz.Question || displayedSeconds == SecondsLeft) continue;
                displayedSeconds = SecondsLeft;
                if (!ct.IsCancellationRequested) await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private async Task RetryRestoreAsync()
    {
        if (_refreshing) return;
        var ct = _scope.Token;
        _restoring = true;
        try { await RefreshAsync(ct); }
        finally { if (!ct.IsCancellationRequested) _restoring = false; }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested || _busy || _refreshing || _roomCode is not { } code
            || _playerToken is not { } token) return;
        _refreshing = true;
        var revision = _mutations;

        try
        {
            var updated = await Client.GetAsync(code, token, ct);
            if (ct.IsCancellationRequested || revision != _mutations) return;
            if (updated.Personal is null)
            {
                await ExpireTokenAsync(code, "Din plads i spillet er ikke længere tilgængelig.", ct);
                return;
            }
            Apply(updated);
        }
        catch (LiveQuizException error)
        {
            if (ct.IsCancellationRequested || revision != _mutations) return;
            if (IsInvalidParticipant(error)) await ExpireTokenAsync(code, error.Message, ct);
            else SetConnectionError(error.Message);
        }
        catch (Exception error) when (IsTransportError(error))
        {
            if (!ct.IsCancellationRequested && revision == _mutations)
                SetConnectionError("Spillet kunne ikke nås. Prøver igen…");
        }
        finally
        {
            if (!ct.IsCancellationRequested) _refreshing = false;
        }
    }

    private void SetConnectionError(string message)
    {
        _connectionError = true;
        if (_state is null) _entryError = message;
    }

    private async Task ExpireTokenAsync(string code, string message, CancellationToken ct)
    {
        _playerToken = null;
        _state = null;
        _entryError = message;
        _connectionError = false;
        _focusNickname = true;
        // A participant can join again after the previous polling loop has stopped.
        _updates = null;
        try { await JS.InvokeVoidAsync("lupLive.clearPlayerToken", ct, code); }
        catch (JSException) { }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private void Apply(LiveQuizState state)
    {
        if (state.QuestionIndex != _state?.QuestionIndex)
        {
            _selected.Clear();
            _writtenAnswer = "";
        }
        if (state.Phase != _state?.Phase || state.Personal?.HasAnswered == true)
            _entryError = null;
        if (state is { Phase: LiveQuiz.Question, Personal.HasAnswered: false } && _state?.Phase != LiveQuiz.Question)
            _focusQuestion = true;
        _receivedAt = Stopwatch.GetTimestamp();
        _state = state;
        _connectionError = false;
    }

    private static bool IsInvalidParticipant(LiveQuizException error) =>
        error.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound;

    private static bool IsTransportError(Exception error) =>
        error is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException;

    private static string SelectedText(LiveQuizQuestion question, LiveQuizPersonal personal) =>
        personal.Text ?? OptionText(question, personal.SelectedOptions);

    private static string CorrectText(LiveQuizQuestion? question, LiveQuizReveal reveal) =>
        reveal.AcceptedAnswers.Length > 0
            ? string.Join(" / ", reveal.AcceptedAnswers)
            : question is null ? "" : OptionText(question, reveal.CorrectOptions);

    private static string OptionText(LiveQuizQuestion question, IEnumerable<int> indexes) =>
        string.Join(" / ", indexes.Where(i => i >= 0 && i < question.Options.Length).Select(i => question.Options[i]));

    private static string PhaseAnnouncement(LiveQuizState state) => state.Phase switch
    {
        LiveQuiz.Reveal when state.Personal?.Correct == true => "Svaret er korrekt.",
        LiveQuiz.Reveal when state.Personal?.HasAnswered == true => "Svaret er forkert.",
        LiveQuiz.Reveal => "Tiden er gået.",
        LiveQuiz.Leaderboard => "Din placering er klar.",
        LiveQuiz.Finished => "Spillet er slut.",
        _ => ""
    };

    private static string RankText(LiveQuizPersonal personal) => personal.Rank > 0 ? $"{personal.Rank}." : "–";

    public async ValueTask DisposeAsync()
    {
        await _scope.CancelAsync();
        if (_updates is { } updates) await updates;
        _scope.Dispose();
    }
}
