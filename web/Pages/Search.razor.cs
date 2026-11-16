using System.Net.Http.Json;
using System.Text.Json;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Public;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Pages;

public partial class Search
{
    private const int MaxQueryLength = 200;
    private const int PageSize = 20;
    private static readonly (string Id, string Label)[] Categories =
    [
        (GlobalSearchCategories.All, "Alt"),
        (GlobalSearchCategories.People, "Personer"),
        (GlobalSearchCategories.Assignments, "Opgaver"),
        (GlobalSearchCategories.Badges, "Badges"),
        (GlobalSearchCategories.Projects, "Projekter"),
        (GlobalSearchCategories.Courses, "Forløb, fag og emner"),
        (GlobalSearchCategories.Materials, "Materialer"),
        (GlobalSearchCategories.Notes, "Notesbøger"),
        (GlobalSearchCategories.Holds, "Hold og grupper"),
        (GlobalSearchCategories.KnowledgeChecks, "Videnstjek"),
        (GlobalSearchCategories.Games, "Spil"),
        (GlobalSearchCategories.Characters, "Karakterer"),
        (GlobalSearchCategories.Events, "Kalender"),
        (GlobalSearchCategories.Polls, "Afstemninger"),
        (GlobalSearchCategories.Files, "Filer"),
        (GlobalSearchCategories.Electives, "Valgfag"),
        (GlobalSearchCategories.Pages, "Sider"),
    ];

    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] private AuthService Auth { get; set; } = default!;
    [Inject] private ViewAsStudentContext ViewAs { get; set; } = default!;
    [SupplyParameterFromQuery(Name = "q")] public string? Query { get; set; }
    [SupplyParameterFromQuery(Name = "category")] public string? Category { get; set; }

    private readonly List<GlobalSearchHit> _items = [];
    private CancellationTokenSource? _debounce;
    private CancellationTokenSource? _request;
    private string _draftQuery = "";
    private string _draftCategory = GlobalSearchCategories.All;
    private int _total;
    private bool _loading = true;
    private bool _failed;
    private bool _disposed;
    private (bool Ready, bool SignedIn, string? Subject, bool Preview) _lastScope;

    private (bool Ready, bool SignedIn, string? Subject, bool Preview) Scope
        => (Auth.IsInitialized, Auth.IsAuthenticated, Auth.Subject, ViewAs.IsActive);

    protected override void OnInitialized()
    {
        Auth.AuthStateChanged += ScopeChanged;
        ViewAs.Changed += ScopeChanged;
    }

    protected override Task OnParametersSetAsync()
    {
        _debounce?.Cancel();
        _draftQuery = CleanQuery(Query);
        _draftCategory = NormalizeCategory(Category);
        return SearchAsync();
    }

    private void ScopeChanged() => _ = InvokeAsync(async () =>
    {
        if (_disposed || _lastScope == Scope) return;
        var update = SearchAsync();
        StateHasChanged();
        await update;
        if (!_disposed) StateHasChanged();
    });

    private async Task QueueSearchAsync()
    {
        _debounce?.Cancel();
        using var debounce = new CancellationTokenSource();
        _debounce = debounce;
        try
        {
            await Task.Delay(250, debounce.Token);
            await ApplyDraftAsync(replace: true);
        }
        catch (OperationCanceledException) when (debounce.IsCancellationRequested) { }
        finally
        {
            if (_debounce == debounce) _debounce = null;
        }
    }

    private Task SubmitAsync()
    {
        _debounce?.Cancel();
        return ApplyDraftAsync(replace: false);
    }

    private Task ApplyDraftAsync(bool replace)
    {
        var query = CleanQuery(_draftQuery);
        var category = NormalizeCategory(_draftCategory);
        var target = Nav.GetUriWithQueryParameters(Nav.ToAbsoluteUri("/search").AbsoluteUri, new Dictionary<string, object?>
        {
            ["q"] = query.Length == 0 ? null : query,
            ["category"] = category == GlobalSearchCategories.All ? null : category,
        });
        if (target == Nav.Uri)
            return _failed && !replace ? RetryAsync() : Task.CompletedTask;

        Nav.NavigateTo(target, replace: replace);
        return Task.CompletedTask;
    }

    private Task LoadMoreAsync() => _loading ? Task.CompletedTask : SearchAsync(_items.Count);
    private Task RetryAsync() => SearchAsync(_items.Count);

    private async Task SearchAsync(int offset = 0)
    {
        _request?.Cancel();
        _request = null;
        var scope = _lastScope = Scope;
        _failed = false;
        _loading = true;
        if (offset == 0)
        {
            _items.Clear();
            _total = 0;
        }
        if (!scope.Ready) return;

        var query = CleanQuery(Query);
        if (query.Length == 0)
        {
            _loading = false;
            return;
        }

        var category = NormalizeCategory(Category);
        using var request = new CancellationTokenSource();
        _request = request;
        try
        {
            GlobalSearchResult result;
            if (scope.SignedIn && !scope.Preview)
            {
                var path = $"search?q={Uri.EscapeDataString(query)}&category={category}&offset={offset}&limit={PageSize}";
                result = await Http.GetFromJsonAsync<GlobalSearchResult>(path, request.Token)
                    ?? throw new JsonException("Search response was empty.");
            }
            else
            {
                var matches = PublicGlobalSearch.Rank(PublicGlobalSearch.Search(query, category), query).ToArray();
                result = new(matches.Skip(offset).Take(PageSize).ToArray(), matches.Length);
            }

            if (request.IsCancellationRequested || scope != Scope) return;
            _items.AddRange(result.Items);
            _total = result.Total;
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception error) when (error is HttpRequestException or JsonException or OperationCanceledException)
        {
            if (!request.IsCancellationRequested && scope == Scope) _failed = true;
        }
        finally
        {
            if (_request == request)
            {
                _request = null;
                _loading = false;
            }
        }
    }

    private static string CleanQuery(string? value)
    {
        var query = value?.Trim() ?? "";
        return query.Length <= MaxQueryLength ? query : query[..MaxQueryLength];
    }

    private static string NormalizeCategory(string? value)
        => GlobalSearchCategories.IsValid(value) ? value! : GlobalSearchCategories.All;

    public void Dispose()
    {
        _disposed = true;
        _debounce?.Cancel();
        _request?.Cancel();
        Auth.AuthStateChanged -= ScopeChanged;
        ViewAs.Changed -= ScopeChanged;
    }
}
