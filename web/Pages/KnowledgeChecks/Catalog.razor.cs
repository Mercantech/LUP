using System.Globalization;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Auth;
using Mercantec.Tech.Web.Characters;
using Mercantec.Tech.Web.KnowledgeChecks;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Pages.KnowledgeChecks;

public partial class Catalog
{
    [Inject] private KnowledgeCheckClient Client { get; set; } = default!;
    [Inject] private AuthRedirect Redirect { get; set; } = default!;
    [Inject] private ViewAsStudentContext ViewAs { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "vis")] public string? RequestedView { get; set; }
    [SupplyParameterFromQuery(Name = "q")] public string? RequestedSearch { get; set; }
    [SupplyParameterFromQuery(Name = "emne")] public string? RequestedTopic { get; set; }
    [SupplyParameterFromQuery(Name = "niveau")] public string? RequestedDifficulty { get; set; }
    [SupplyParameterFromQuery(Name = "karakter")] public string? RequestedCharacter { get; set; }

    private const int SearchLimit = 160;
    private static readonly CultureInfo DanishCulture = CultureInfo.GetCultureInfo("da-DK");
    private static readonly StringComparer Danish = StringComparer.Create(DanishCulture, ignoreCase: true);
    private static readonly (string Key, string Label)[] Tabs =
        [("oev", "Frie øvelser"), ("tildelinger", "Mine tildelinger"), ("historik", "Min historik"), ("administrer", "Administrér")];
    private static readonly string[] DefaultTopics = ["GF2", "Backend", "Frontend", "Flutter"];
    private static readonly string[] Difficulties = ["Begynder", "Mellem", "Avanceret"];

    private readonly Dictionary<Guid, Guid> _startRequests = [];
    private readonly Dictionary<Guid, string> _startErrors = [];
    private KnowledgeCheckIndex? _index;
    private string? _error;
    private string? _pilotError;
    private string? _importError;
    private string? _importStatus;
    private ElementReference _searchInput;
    private Guid? _startingPublication;
    private bool _creatingPilot;
    private bool _importingLegacy;
    private bool _focusSearch;
    private bool _disposed;

    private bool CanManage => !ViewAs.IsActive && Redirect.CurrentAppRole() == AppRole.Underviser;
    private string View => RequestedView switch
    {
        "administrer" when CanManage => "administrer",
        "historik" or "tildelinger" => RequestedView,
        _ => "oev"
    };
    private string Search => RequestedSearch is { Length: > SearchLimit } text ? text[..SearchLimit] : RequestedSearch ?? "";
    private string Topic => RequestedTopic ?? "";
    private string Difficulty => Difficulties.FirstOrDefault(item => Danish.Equals(item, RequestedDifficulty)) ?? "";
    private string Character => CharacterCatalog.Find(RequestedCharacter)?.Slug ?? "";
    private bool HasFilters => Search.Length > 0 || Topic.Length > 0 || Difficulty.Length > 0 || Character.Length > 0;

    private IEnumerable<KnowledgeCheckPublicationView> Assignments =>
        _index?.Publications.Where(item => item.IsActive && item.HoldId is not null && !item.CanManage) ?? [];
    private int AssignmentCount => Assignments.Count();
    private IEnumerable<KnowledgeCheckPublicationView> Practice =>
        _index?.Publications.Where(item => item.IsActive && item.HoldId is null && item.IsCurrentPractice) ?? [];
    private IEnumerable<KnowledgeCheckPublicationView> Managed =>
        _index?.Publications.Where(item => item.CanManage) ?? [];
    private IEnumerable<string> Topics => DefaultTopics.Concat(Practice.SelectMany(TopicsOf).Order(Danish)).Distinct(Danish);
    private IEnumerable<CharacterProfile> Hosts
    {
        get
        {
            var slugs = Practice.Select(item => item.CharacterHost?.Slug).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            var selected = Character;
            return CharacterCatalog.All.Where(character => slugs.Contains(character.Slug) || character.Slug == selected)
                .OrderBy(character => character.Name, Danish);
        }
    }

    private IEnumerable<KnowledgeCheckPublicationView> FilteredPractice
    {
        get
        {
            var topic = Topic;
            var difficulty = Difficulty;
            var character = Character;
            var search = Search.Trim();
            return Practice.Where(item =>
                (topic.Length == 0 || TopicsOf(item).Contains(topic, Danish))
                && (difficulty.Length == 0 || Danish.Equals(difficulty, DifficultyOf(item)))
                && (character.Length == 0 || string.Equals(character, item.CharacterHost?.Slug, StringComparison.OrdinalIgnoreCase))
                && (search.Length == 0 || MatchesSearch(item, search)))
                .OrderBy(item => item.Title, Danish)
                .ThenBy(item => item.Id);
        }
    }

    private static string[] TopicsOf(KnowledgeCheckPublicationView item) =>
        item.LegacyDiscovery?.Categories is { Length: > 0 } categories ? categories
        : string.IsNullOrWhiteSpace(item.Topic) ? [] : [item.Topic];

    private static string? DifficultyOf(KnowledgeCheckPublicationView item) =>
        item.CharacterHost?.Difficulty ?? item.LegacyDiscovery?.Difficulty;

    private static CharacterProfile? HostOf(KnowledgeCheckPublicationView item) =>
        CharacterCatalog.Find(item.CharacterHost?.Slug);

    private static bool MatchesSearch(KnowledgeCheckPublicationView item, string search)
    {
        var corpus = $"{item.Title} {item.Description} {string.Join(" ", TopicsOf(item))} {HostOf(item)?.Name}";
        return DanishCulture.CompareInfo.IndexOf(corpus, search, CompareOptions.IgnoreCase) >= 0;
    }

    private string ViewUrl(string view) => CatalogUrl(view, Search, Topic, Difficulty, Character);
    private string TopicUrl(string topic) => CatalogUrl("oev", Search, topic, Difficulty, Character);

    private string CatalogUrl(string view, string search, string topic, string difficulty, string character) =>
        FilterableUrl("/videnstjek", view, null, search, topic, difficulty, character);

    private string AttemptUrl(Guid publicationId, Guid attemptId) =>
        FilterableUrl($"/videnstjek/{publicationId}", null, attemptId, Search, Topic, Difficulty, Character);

    private string FilterableUrl(string path, string? view, Guid? attempt, string search, string topic, string difficulty, string character) =>
        Nav.GetUriWithQueryParameters(Nav.ToAbsoluteUri(path).AbsoluteUri, new Dictionary<string, object?>
        {
            ["vis"] = view,
            ["forsog"] = attempt?.ToString(),
            ["q"] = NullIfEmpty(search),
            ["emne"] = NullIfEmpty(topic),
            ["niveau"] = NullIfEmpty(difficulty),
            ["karakter"] = NullIfEmpty(character)
        });

    private static string? NullIfEmpty(string value) => value.Length > 0 ? value : null;

    private void ChangeSearch(ChangeEventArgs change) =>
        Nav.NavigateTo(CatalogUrl("oev", change.Value as string ?? "", Topic, Difficulty, Character), replace: true);

    private void ChangeDifficulty(ChangeEventArgs change) =>
        Nav.NavigateTo(CatalogUrl("oev", Search, Topic, change.Value as string ?? "", Character));

    private void ChangeCharacter(ChangeEventArgs change) =>
        Nav.NavigateTo(CatalogUrl("oev", Search, Topic, Difficulty, change.Value as string ?? ""));

    private void ClearFilters()
    {
        _focusSearch = true;
        Nav.NavigateTo(CatalogUrl("oev", "", "", "", ""));
    }

    protected override Task OnInitializedAsync() => LoadAsync();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focusSearch || HasFilters || View != "oev" || ViewAs.IsActive) return;
        _focusSearch = false;
        await _searchInput.FocusAsync();
    }

    private async Task LoadAsync()
    {
        if (ViewAs.IsActive) return;
        _error = null;
        try
        {
            _index = await Client.GetAsync<KnowledgeCheckIndex>();
        }
        catch (KnowledgeCheckException ex) { _error = ex.Message; }
        catch (HttpRequestException) { _error = "Videnstjek kunne ikke hentes. Prøv igen."; }
    }

    private async Task StartPracticeAsync(KnowledgeCheckPublicationView publication)
    {
        if (_disposed || ViewAs.IsActive || publication.HoldId is not null || _startingPublication is not null) return;

        _startingPublication = publication.Id;
        _startErrors.Remove(publication.Id);
        if (!_startRequests.TryGetValue(publication.Id, out var requestId))
        {
            requestId = Guid.NewGuid();
            _startRequests.Add(publication.Id, requestId);
        }

        try
        {
            var attempt = await Client.SendAsync<KnowledgeCheckAttemptView>(HttpMethod.Post,
                $"publications/{publication.Id}/attempts", new KnowledgeCheckStart(requestId));
            _startRequests.Remove(publication.Id);
            if (_disposed || ViewAs.IsActive) return;

            if (_index is { } index)
            {
                var cardIndex = Array.FindIndex(index.Publications, item => item.Id == publication.Id);
                if (cardIndex >= 0)
                {
                    index.Publications[cardIndex] = index.Publications[cardIndex] with
                    {
                        ActiveAttemptId = attempt.Status == "started" ? attempt.Id : null,
                        LatestAttemptId = attempt.Id
                    };
                }
            }

            if (View == "oev")
                Nav.NavigateTo(AttemptUrl(publication.Id, attempt.Id));
        }
        catch (KnowledgeCheckException ex) { _startErrors[publication.Id] = ex.Message; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            _startErrors[publication.Id] = "Quizzen kunne ikke åbnes. Prøv igen.";
        }
        finally { _startingPublication = null; }
    }

    private async Task CreatePilotAsync()
    {
        if (_creatingPilot) return;
        _pilotError = null;
        _creatingPilot = true;
        try
        {
            var draft = await Client.SendAsync<KnowledgeCheckDraftView>(HttpMethod.Post, "pilot");
            Nav.NavigateTo($"/videnstjek/rediger/{draft.Id}");
        }
        catch (KnowledgeCheckException ex) { _pilotError = ex.Message; }
        catch (HttpRequestException) { _pilotError = "Eksemplet kunne ikke oprettes. Prøv igen."; }
        finally { _creatingPilot = false; }
    }

    private async Task ImportLegacyAsync()
    {
        if (_importingLegacy) return;
        _importingLegacy = true;
        _importError = null;
        _importStatus = null;
        try
        {
            var result = await Client.SendAsync<KnowledgeCheckLegacyImport>(HttpMethod.Post, "legacy-import");
            _importStatus = result.Added == 0
                ? "Du har allerede importeret alle videnstjek fra det gamle katalog."
                : $"{result.Added} videnstjek er tilføjet som kladder."
                    + (result.Existing > 0 ? $" {result.Existing} var allerede importeret." : "");
            await LoadAsync();
        }
        catch (KnowledgeCheckException ex) { _importError = ex.Message; }
        catch (HttpRequestException) { _importError = "Importen kunne ikke gennemføres. Prøv igen."; }
        finally { _importingLegacy = false; }
    }

    private static string QuestionLabel(int count) => $"{count} spørgsmål";
    private static string QuizCountLabel(int count) => count == 1 ? "1 quiz" : $"{count} quizzer";

    private static string StatusLabel(string status) => status switch
    {
        "started" => "I gang",
        "submitted" => "Afleveret",
        "graded" => "Vurderet",
        _ => "Forsøg"
    };

    private static string AssignmentAction(KnowledgeCheckPublicationView item) => item.ActiveAttemptId is not null
        ? "Fortsæt"
        : item.LatestAttemptId is not null ? "Åbn videnstjek" : "Start";

    private static string FormatDate(DateTime date) => date.ToLocalTime().ToString("d. MMM yyyy", DanishCulture);

    public void Dispose() => _disposed = true;
}
