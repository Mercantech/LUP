using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Electives;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Mercantec.Tech.Web.Pages.Electives;

public partial class ElectiveRoster
{
    [Parameter, EditorRequired] public ElectiveView Item { get; set; } = null!;
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback<ElectiveView> OnChanged { get; set; }
    [Parameter] public EventCallback<bool> OnSavingChanged { get; set; }

    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _activeSearch;
    private ElectiveParticipant[] _participants = [];
    private ElectiveLectioStudent[]? _results;
    private ElementReference _heading, _searchInput, _cancelRemove;
    private Guid? _removeId;
    private string _query = "";
    private string? _loadError, _searchError, _changeError, _notice;
    private bool _loading, _searching, _saving, _focusCancel, _focusSearch, _useReserved;

    protected override Task OnInitializedAsync()
    {
        _useReserved = Item.ReservedSeats > 0;
        return LoadParticipants();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await _heading.FocusAsync();
        else if (_focusCancel && _removeId is not null)
        {
            _focusCancel = false;
            await _cancelRemove.FocusAsync();
        }
        else if (_focusSearch && !_loading && _loadError is null && !_saving)
        {
            _focusSearch = false;
            await _searchInput.FocusAsync();
        }
    }

    private async Task LoadParticipants()
    {
        _loading = true;
        _loadError = null;
        var ct = _lifetime.Token;
        try
        {
            var participants = await Client.Participants(Item.Id, ct);
            if (!ct.IsCancellationRequested)
                _participants = participants;
        }
        catch (Exception ex) when (IsRequestError(ex))
        {
            if (!ct.IsCancellationRequested)
                _loadError = Error(ex, "Deltagerlisten kunne ikke hentes.");
        }
        finally { _loading = false; }
    }

    private Task Search(ChangeEventArgs args)
    {
        _query = args.Value?.ToString() ?? "";
        return FindStudents(_query);
    }

    private async Task FindStudents(string query)
    {
        ResetSearch();
        query = query.Trim();
        if (query.Length < 2)
            return;

        using var search = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _activeSearch = search;
        _searching = true;
        try
        {
            await Task.Delay(250, search.Token);
            var students = await Client.SearchLectioStudents(query, search.Token);
            if (!search.IsCancellationRequested)
                _results = students;
        }
        catch (Exception ex) when (IsRequestError(ex))
        {
            if (!search.IsCancellationRequested)
                _searchError = Error(ex, "Søgningen i Lectio mislykkedes.");
        }
        finally
        {
            if (_activeSearch == search)
            {
                _activeSearch = null;
                _searching = false;
            }
        }
    }

    private void ResetSearch()
    {
        _activeSearch?.Cancel();
        _activeSearch = null;
        _results = null;
        _searchError = null;
        _searching = false;
    }

    private void SearchKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            _query = "";
            ResetSearch();
        }
    }

    private void ConfirmRemove(Guid id)
    {
        _removeId = id;
        _focusCancel = true;
        _changeError = _notice = null;
    }

    private void CancelRemove()
    {
        _removeId = null;
        _focusSearch = true;
    }

    private Task AddStudent(ElectiveLectioStudent student) =>
        Change(ct => Client.AddLectioParticipant(Item.Id, student.LectioId, Item.ReservedSeats > 0 && _useReserved, ct),
            $"{student.DisplayName} er tilmeldt holdet.");

    private Task RemoveStudent(ElectiveParticipant person) =>
        Change(ct => Client.RemoveLectioParticipant(Item.Id, person.LectioId!, ct), $"{person.DisplayName} er fjernet fra holdet.");

    private async Task Change(Func<CancellationToken, Task<ElectiveView>> mutation, string notice)
    {
        if (_saving) return;

        var ct = _lifetime.Token;
        _saving = true;
        _changeError = _notice = null;
        try
        {
            await OnSavingChanged.InvokeAsync(true);
            var updated = await mutation(ct);
            if (ct.IsCancellationRequested) return;

            _removeId = null;
            _query = "";
            ResetSearch();
            _notice = notice;
            await OnChanged.InvokeAsync(updated);
            if (!ct.IsCancellationRequested)
                await LoadParticipants();
            _focusSearch = true;
        }
        catch (Exception ex) when (IsRequestError(ex))
        {
            if (!ct.IsCancellationRequested)
                _changeError = Error(ex, "Deltagerlisten kunne ikke ændres. Prøv igen.");
        }
        finally
        {
            _saving = false;
            if (!ct.IsCancellationRequested)
                await OnSavingChanged.InvokeAsync(false);
        }
    }

    private static string StudentInfo(ElectiveLectioStudent student) => string.IsNullOrWhiteSpace(student.ClassName)
        ? $"Lectio-id {student.LectioId}" : $"{student.ClassName} · Lectio-id {student.LectioId}";

    private static bool IsRequestError(Exception ex) => ex is ElectiveException or HttpRequestException or OperationCanceledException;
    private static string Error(Exception ex, string fallback) => ex is ElectiveException ? ex.Message : fallback;

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
