using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Electives;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Pages.Electives;

public partial class Index
{
    [SupplyParameterFromQuery(Name = "vis")] public string? View { get; set; }
    [SupplyParameterFromQuery(Name = "fag")] public string? Template { get; set; }
    [SupplyParameterFromQuery(Name = "id")] public Guid? SearchItem { get; set; }

    [SupplyParameterFromQuery(Name = "deltagere")] public bool? RosterRequested { get; set; }

    private CancellationTokenSource _accountScope = new();
    private (string? Subject, bool Preview, bool Teacher)? _account;
    private ElectiveView[] _items = [];
    private string _search = "";
    private string? _error, _notice, _lastView;
    private bool _loading = true;
    private bool _canManage;
    private bool _disposed;

    private string ActiveView => View switch
    {
        "aktuelle" or "katalog" or "historik" => View,
        "mine" when !Redirect.ShowsTeacherChrome && !ViewAs.IsActive => View,
        "administrer" when _canManage => View,
        _ => "katalog"
    };

    private bool IsPackageIndex => ActiveView == "katalog" && SearchItem is null;
    private ElectivePackage? SelectedPackage => ActiveView == "katalog" && SearchItem.HasValue
        ? ElectivePackage.Catalogue.FirstOrDefault(p => p.Id == SearchItem) : null;

    private IEnumerable<ElectivePackage> AvailablePackages =>
        ElectivePackage.Catalogue.Where(p => FindTemplate(p.Id) is not null);

    private ElectiveView[] UpcomingFor(Guid templateId) =>
        _items.Where(e => e.TemplateId == templateId && IsOffering(e) && !ElectiveDisplay.IsPast(e))
            .OrderBy(e => e.StartsOn ?? DateOnly.MaxValue).ThenBy(e => e.Title).ToArray();

    private ElectiveView? SelectedTemplate => Guid.TryParse(Template, out var id) ? FindTemplate(id) : null;

    private IEnumerable<ElectiveView> VisibleItems => ActiveView switch
    {
        "katalog" => _items.Where(e => e.Status == "template").OrderBy(e => e.Title),
        "historik" => _items.Where(e => IsOffering(e) && ElectiveDisplay.IsPast(e))
            .OrderByDescending(e => e.StartsOn).ThenBy(e => e.Title),
        "mine" => _items.Where(e => e.IsEnrolled)
            .OrderBy(e => ElectiveDisplay.IsPast(e)).ThenBy(e => e.StartsOn).ThenBy(e => e.Title),
        _ => _items.Where(e => IsOffering(e) && !ElectiveDisplay.IsPast(e))
            .OrderBy(e => e.StartsOn ?? DateOnly.MaxValue).ThenBy(e => e.Title)
    };

    private IEnumerable<ElectiveView> MatchingItems
    {
        get
        {
            var search = _search.Trim();
            if (SearchItem is { } id)
                return VisibleItems.Where(e => e.Id == id);

            return VisibleItems.Where(e => e.Title.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || e.Description.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || e.Schedule.Contains(search, StringComparison.CurrentCultureIgnoreCase));
        }
    }

    private string ViewTitle => ActiveView switch
    {
        "katalog" => "Fagkatalog",
        "historik" => "Tidligere hold",
        "mine" => "Mine tilmeldinger",
        _ => "Aktuelle hold"
    };

    private string ViewDescription => ActiveView switch
    {
        "katalog" => "Læs om fagene, deres indhold og forudsætninger.",
        "historik" => "Se tidligere udbudte valgfag og deres undervisningsperioder.",
        "mine" => "Her finder du de valgfag, du selv har tilmeldt dig i LUP.",
        _ => "Se kommende undervisningsperioder, tilmeldingsfrister og ledige pladser."
    };

    private string EmptyText => ActiveView switch
    {
        "katalog" => "Der er endnu ingen fag i kataloget.",
        "historik" => "Der er endnu ingen tidligere hold.",
        "mine" => "Du har endnu ingen valgfagstilmeldinger i LUP.",
        _ => "Der er ingen aktuelle hold lige nu."
    };

    private string? CurrentLink(string view) => ActiveView == view ? "page" : null;

    private int? ArchiveYear(ElectiveView item) => ActiveView == "historik" ? (item.StartsOn ?? item.EndsOn)?.Year : null;

    private static bool IsOffering(ElectiveView elective) => elective.Status is "open" or "closed" or "archived";

    private ElectiveView? FindTemplate(Guid? id) => _items.FirstOrDefault(e => e.Id == id && e.Status == "template");

    protected override Task OnInitializedAsync()
    {
        Auth.AuthStateChanged += AccountChanged;
        ViewAs.Changed += AccountChanged;
        return SyncAccount();
    }

    protected override void OnParametersSet()
    {
        if (_lastView == View)
            return;

        _lastView = View;
        _search = "";
        _notice = null;
    }

    private void AccountChanged() => _ = InvokeAsync(async () =>
    {
        await SyncAccount();
        if (!_disposed)
            StateHasChanged();
    });

    private Task SyncAccount()
    {
        var account = (Auth.IsAuthenticated ? Auth.Subject : null, ViewAs.IsActive, Redirect.ShowsTeacherChrome);
        if (_disposed || _account == account)
            return Task.CompletedTask;

        _account = account;
        _accountScope.Cancel();
        _accountScope.Dispose();
        _accountScope = new();
        _items = [];
        _canManage = false;
        _error = _notice = null;
        StateHasChanged();
        return Auth.IsAuthenticated ? Load() : Task.CompletedTask;
    }

    private async Task Load()
    {
        var ct = _accountScope.Token;
        _loading = true;
        _error = _notice = null;
        try
        {
            var result = await Client.List(ViewAs.IsActive, ct);
            if (!ct.IsCancellationRequested)
            {
                _items = result.Electives.Select(ElectiveDisplay.ForCatalogue).ToArray();
                _canManage = result.CanManage && Redirect.ShowsTeacherChrome;
            }
        }
        catch (Exception ex) when (ex is ElectiveException or HttpRequestException or OperationCanceledException)
        {
            if (!ct.IsCancellationRequested)
                _error = ex is ElectiveException ? ex.Message : "Valgfagene kunne ikke hentes. Prøv igen.";
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                _loading = false;
        }
    }

    private void ElectiveSaved(ElectiveView item)
    {
        Replace(item);
        _notice = item.Status == "template" ? "Faget er gemt i fagkataloget." : "Holdet er gemt.";
        if (Template is not null && item.Status != "template")
            Nav.NavigateTo($"/valgfag?vis=administrer&id={item.Id:D}&deltagere=true", replace: true);
    }

    private void EnrollmentChanged(ElectiveView item)
    {
        Replace(item);
        var title = ElectiveCatalogueText.Clean(item.Title);
        _notice = item.IsEnrolled ? $"Du er tilmeldt {title}." : $"Du er afmeldt {title}.";
    }

    private void Replace(ElectiveView item) => _items = [.. _items.Where(e => e.Id != item.Id), ElectiveDisplay.ForCatalogue(item)];

    public void Dispose()
    {
        _disposed = true;
        Auth.AuthStateChanged -= AccountChanged;
        ViewAs.Changed -= AccountChanged;
        _accountScope.Cancel();
        _accountScope.Dispose();
    }
}
