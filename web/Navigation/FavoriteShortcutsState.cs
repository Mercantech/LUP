using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;

namespace Mercantec.Tech.Web.Navigation;

public sealed class FavoriteShortcutsState : IDisposable
{
    private const string Path = "me/favorite-shortcuts";
    private readonly HttpClient _http;
    private readonly AuthService _auth;
    private readonly AuthRedirect _redirect;
    private readonly ViewAsStudentContext _viewAs;
    private AccountFavorites? _account;

    public FavoriteShortcutsState(HttpClient http, AuthService auth, AuthRedirect redirect, ViewAsStudentContext viewAs)
    {
        _http = http;
        _auth = auth;
        _redirect = redirect;
        _viewAs = viewAs;
        _auth.AuthStateChanged += AccountChanged;
        _viewAs.Changed += AccountChanged;
        AccountChanged();
    }

    public event Action? Changed;
    public IReadOnlyList<FavoriteShortcut> Items => _account?.Items ?? [];
    public bool IsLoaded => _account?.IsLoaded == true;
    public bool IsBusy => _account?.IsBusy == true;
    public string? Error => _account?.Error;

    public bool Contains(string href) => Items.Any(item => FavoriteShortcutNavigation.Matches(item.Href, href));

    public Task EnsureAsync()
    {
        if (_account is not { IsLoaded: false, IsBusy: false } account) return Task.CompletedTask;
        return RunAsync(account, ct => ReadAsync(account, ct), "Dine favoritter kunne ikke hentes. Prøv igen.");
    }

    public Task AddAsync(FavoriteShortcut shortcut) =>
        Contains(shortcut.Href) ? Task.CompletedTask : SaveAsync([.. Items, shortcut]);

    public Task RemoveAsync(string href) =>
        SaveAsync(Items.Where(item => !FavoriteShortcutNavigation.Matches(item.Href, href)).ToArray());

    public Task MoveAsync(string href, int offset)
    {
        var items = Items.ToArray();
        var index = Array.FindIndex(items, item => FavoriteShortcutNavigation.Matches(item.Href, href));
        var target = index + offset;
        if (index < 0 || target < 0 || target >= items.Length || offset is not (-1 or 1)) return Task.CompletedTask;
        (items[index], items[target]) = (items[target], items[index]);
        return SaveAsync(items);
    }

    private Task SaveAsync(IReadOnlyList<FavoriteShortcut> items)
    {
        if (_account is not { IsLoaded: true, IsBusy: false } account) return Task.CompletedTask;
        if (items.Count > FavoriteShortcutRules.MaxCount)
        {
            account.Error = $"Du kan have op til {FavoriteShortcutRules.MaxCount} favoritter. Fjern en favorit, før du tilføjer en ny.";
            Changed?.Invoke();
            return Task.CompletedTask;
        }

        return RunAsync(account, async ct =>
        {
            using var request = CreateRequest(HttpMethod.Put, account);
            request.Content = JsonContent.Create(new FavoriteShortcutsDto(account.Revision, items));
            using var response = await _http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                account.IsLoaded = false;
                await ReadAsync(account, ct);
                account.Error = "Dine favoritter er ændret i en anden fane eller på en anden enhed. Listen er opdateret. Prøv igen.";
                return;
            }

            await ApplyAsync(account, response, ct);
        }, "Ændringen kunne ikke gemmes. Prøv igen.");
    }

    private async Task ReadAsync(AccountFavorites account, CancellationToken ct)
    {
        using var request = CreateRequest(HttpMethod.Get, account);
        using var response = await _http.SendAsync(request, ct);
        await ApplyAsync(account, response, ct);
    }

    private async Task ApplyAsync(AccountFavorites account, HttpResponseMessage response, CancellationToken ct)
    {
        response.EnsureSuccessStatusCode();
        var snapshot = await response.Content.ReadFromJsonAsync<FavoriteShortcutsDto>(ct)
            ?? throw new JsonException("Favoritter mangler i svaret.");
        ct.ThrowIfCancellationRequested();
        account.Revision = snapshot.Revision;
        account.Items = snapshot.Revision is null
            ? FavoriteShortcutNavigation.Defaults(account.IsTeacher)
            : snapshot.Items;
        account.IsLoaded = true;
    }

    private async Task RunAsync(AccountFavorites account, Func<CancellationToken, Task> action, string failure)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(account.Lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        account.IsBusy = true;
        account.Error = null;
        Changed?.Invoke();
        try
        {
            await action(timeout.Token);
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or OperationCanceledException)
        {
            account.Error = failure;
        }
        finally
        {
            account.IsBusy = false;
            if (ReferenceEquals(account, _account)) Changed?.Invoke();
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, AccountFavorites account)
    {
        var request = new HttpRequestMessage(method, Path);
        request.Headers.Add(FavoriteShortcutRules.SubjectHeader, account.Subject.ToString("D"));
        return request;
    }

    private void AccountChanged()
    {
        var subject = _auth.IsAuthenticated && !_viewAs.IsActive && Guid.TryParse(_auth.Subject, out var id) && id != Guid.Empty
            ? id : (Guid?)null;
        var teacher = _redirect.ShowsTeacherChrome;
        if (_account?.Subject == subject && (_account is null || _account.IsTeacher == teacher)) return;

        ResetAccount(subject is Guid own ? new AccountFavorites(own, teacher) : null);
        Changed?.Invoke();
        _ = EnsureAsync();
    }

    private void ResetAccount(AccountFavorites? next)
    {
        var previous = _account;
        _account = next;
        previous?.Lifetime.Cancel();
        previous?.Lifetime.Dispose();
    }

    public void Dispose()
    {
        _auth.AuthStateChanged -= AccountChanged;
        _viewAs.Changed -= AccountChanged;
        ResetAccount(null);
    }

    private sealed class AccountFavorites(Guid subject, bool isTeacher)
    {
        public Guid Subject { get; } = subject;
        public bool IsTeacher { get; } = isTeacher;
        public CancellationTokenSource Lifetime { get; } = new();
        public Guid? Revision { get; set; }
        public IReadOnlyList<FavoriteShortcut> Items { get; set; } = [];
        public bool IsLoaded { get; set; }
        public bool IsBusy { get; set; }
        public string? Error { get; set; }
    }
}
