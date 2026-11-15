using System.Globalization;
using System.Net;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Pages;

public partial class DailyGameProgress
{
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");
    private readonly CancellationTokenSource _lifetime = new();
    private readonly List<DailyHistoryEntry> _entries = [];
    private DailyHistory? _history;
    private DailyLeaderboard? _ranking;
    private DailyRankingSettings? _settings;
    private string? _historyError;
    private string? _rankingError;
    private string? _settingsError;
    private string? _subject;
    private string? _loadedGame;
    private string _period = "day";
    private Guid? _hold;
    private int _historyVersion;
    private int _rankingVersion;
    private int _settingsVersion;
    private bool _loadingHistory;
    private bool _loadingRanking;
    private bool _saving;
    private bool _disposed;

    [SupplyParameterFromQuery(Name = "game")] public string? Game { get; set; }

    private bool CanView => Auth.IsAuthenticated && Auth.Subject is not null;
    private DailyGameProgressDefinition Selected => DailyGameProgressCatalog.Find(Game) ?? DailyGameProgressCatalog.Games[0];

    protected override void OnInitialized()
    {
        _subject = Auth.Subject;
        Auth.AuthStateChanged += OnAuthChanged;
    }

    protected override async Task OnParametersSetAsync()
    {
        var game = Selected.Id;
        if (!CanView || _loadedGame == game) return;
        _loadedGame = game;
        if (_settings is null)
            await Task.WhenAll(LoadHistoryAsync(1), LoadRankingAsync(), LoadSettingsAsync());
        else
            await Task.WhenAll(LoadHistoryAsync(1), LoadRankingAsync());
    }

    private Task LoadMoreAsync() => _loadingHistory || _history is not { HasMore: true } history
        ? Task.CompletedTask : LoadHistoryAsync(history.Page + 1);

    private Task RetryHistoryAsync() => _history is null ? LoadHistoryAsync(1) : LoadMoreAsync();

    private async Task LoadHistoryAsync(int page)
    {
        if (_disposed || !CanView) return;
        var version = ++_historyVersion;
        var subject = Auth.Subject;
        var ct = _lifetime.Token;
        _loadingHistory = true;
        _historyError = null;
        if (page == 1)
        {
            _history = null;
            _entries.Clear();
        }
        try
        {
            var history = await Progress.HistoryAsync(Selected.Id, page, ct);
            if (IsCurrent(version, _historyVersion, subject))
            {
                _history = history;
                var loadedDates = _entries.Select(entry => entry.Date).ToHashSet();
                _entries.AddRange(history.Entries.Where(entry => loadedDates.Add(entry.Date)));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (IsReadError(ex))
        {
            if (IsCurrent(version, _historyVersion, subject))
                _historyError = "Din historik kunne ikke hentes.";
        }
        finally
        {
            if (IsCurrent(version, _historyVersion, subject)) _loadingHistory = false;
        }
    }

    private async Task LoadRankingAsync()
    {
        if (_disposed || !CanView) return;
        var version = ++_rankingVersion;
        var subject = Auth.Subject;
        var ct = _lifetime.Token;
        _ranking = null;
        _rankingError = null;
        _loadingRanking = true;
        try
        {
            var ranking = await Progress.LeaderboardAsync(Selected.Id, _period, _hold, ct);
            if (IsCurrent(version, _rankingVersion, subject)) _ranking = ranking;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (IsReadError(ex))
        {
            if (IsCurrent(version, _rankingVersion, subject))
                _rankingError = _hold is not null
                    && ex is HttpRequestException { StatusCode: HttpStatusCode.Forbidden or HttpStatusCode.NotFound }
                    ? "Du har ikke adgang til dette holds rangliste. Vælg en anden rangliste."
                    : "Ranglisten kunne ikke hentes.";
        }
        finally
        {
            if (IsCurrent(version, _rankingVersion, subject)) _loadingRanking = false;
        }
    }

    private async Task LoadSettingsAsync()
    {
        if (_disposed || !CanView || _saving) return;
        var version = ++_settingsVersion;
        var subject = Auth.Subject;
        var ct = _lifetime.Token;
        _settingsError = null;
        try
        {
            var settings = await Progress.SettingsAsync(ct);
            if (!IsCurrent(version, _settingsVersion, subject)) return;
            _settings = settings;
            if (_hold is Guid hold && !settings.Holds.Any(item => item.Id == hold))
            {
                _hold = null;
                await LoadRankingAsync();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (IsReadError(ex))
        {
            if (IsCurrent(version, _settingsVersion, subject))
                _settingsError = "Dine indstillinger kunne ikke hentes.";
        }
    }

    private async Task ToggleVisibilityAsync()
    {
        if (_disposed || _saving || Auth.Subject is not { } subject || _settings is not { } settings) return;
        var version = ++_settingsVersion;
        var ct = _lifetime.Token;
        _saving = true;
        _settingsError = null;
        try
        {
            var saved = await Progress.SetVisibilityAsync(!settings.Visible, subject, ct);
            if (!IsCurrent(version, _settingsVersion, subject)) return;
            _settings = saved;
            await LoadRankingAsync();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) when (IsReadError(ex))
        {
            if (IsCurrent(version, _settingsVersion, subject))
                _settingsError = ex is HttpRequestException { StatusCode: HttpStatusCode.UnprocessableEntity }
                    ? "Vælg et visningsnavn på din profil, før du melder dig til."
                    : "Synligheden kunne ikke gemmes. Prøv igen.";
        }
        finally
        {
            if (IsCurrent(version, _settingsVersion, subject)) _saving = false;
        }
    }

    private bool IsCurrent(int version, int currentVersion, string? subject) =>
        !_disposed && version == currentVersion && subject == Auth.Subject;

    private void OnAuthChanged()
    {
        if (_disposed || _subject == Auth.Subject) return;
        _subject = Auth.Subject;
        _ = InvokeAsync(async () =>
        {
            if (_disposed) return;
            _loadedGame = null;
            _history = null;
            _entries.Clear();
            _ranking = null;
            _settings = null;
            _hold = null;
            _loadingHistory = _loadingRanking = _saving = false;
            _historyError = _rankingError = _settingsError = null;
            _historyVersion++;
            _rankingVersion++;
            _settingsVersion++;
            StateHasChanged();
            await OnParametersSetAsync();
            if (!_disposed) StateHasChanged();
        });
    }

    private static bool IsReadError(Exception ex) => ex is HttpRequestException or JsonException or TaskCanceledException;
    private static string FormatDate(DateOnly date) => date.ToString("d. MMM yyyy", Danish);
    private static string FormatPeriod(DateOnly from, DateOnly to) =>
        from == to ? FormatDate(from) : $"{FormatDate(from)} – {FormatDate(to)}";
    private static string Days(int count) => count == 1 ? "1 dag" : $"{count} dage";
    private static string Rounds(int count) => count == 1 ? "1 runde" : $"{count} runder";
    private static string Wins(int count) => count == 1 ? "1 sejr" : $"{count} sejre";

    private static string? EntryDetail(DailyHistoryEntry entry)
    {
        if (entry.Distance is int distance)
            return distance == 0 ? "Præcis på målet" : $"{distance} fra måltallet";
        if (entry.Attempts is not int attempts) return null;
        var detail = attempts == 1 ? "1 gæt" : $"{attempts} gæt";
        return entry.Hints switch { 1 => detail + " · 1 ledetråd", 2 => detail + " · 2 ledetråde", _ => detail };
    }

    public void Dispose()
    {
        _disposed = true;
        Auth.AuthStateChanged -= OnAuthChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
