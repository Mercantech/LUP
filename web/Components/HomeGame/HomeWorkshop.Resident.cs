using System.Text.Json;
using Mercantec.Tech.Contracts.HomeGame;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Components.HomeGame;

public partial class HomeWorkshop
{
    // Rendering every simulation tick would also re-render the editor and every board tile.
    private const int ResidentTicksPerFrame = 2;
    private HomeSimulation? _residentSimulation;
    private HomeWorldSnapshot? _residentFrame;
    private HomeWorldSnapshot? _residentEnvironment;
    private IJSObjectReference? _residentClock;
    private DotNetObjectReference<HomeWorkshop>? _residentCallback;
    private string _runIntent = "auto";

    private sealed record ResidentCheckpoint(HomeResidentState Resident, HomeCell? Cell, string Intent);

    private string ResidentSource => _replay is not null ? "replay" : IsActive ? "run" : "room";
    private HomeWorldSnapshot? ResidentFrame => _replay is not null ? _replay.Snapshot
        : IsActive ? _run?.Snapshot : _residentFrame;
    private HomeCell? AvatarCell => ResidentFrame is { } frame ? frame.AvatarCell : DisplayLayout.Grid.Entry;
    private static bool IsResidentIntent(string? intent) => intent is "auto" or "idle" or "read" or "relax" or "away";

    private async Task OpenResidentRoomAsync()
    {
        _residentCallback = DotNetObjectReference.Create(this);
        _residentClock = await (await ModuleAsync()).InvokeAsync<IJSObjectReference>("openResidentRoom", Token,
            _residentCallback, Subject, _state!.IsTeacherSandbox, HomeCatalog.TickMs * ResidentTicksPerFrame);
        ResidentCheckpoint? saved = null;
        try { saved = await _residentClock.InvokeAsync<ResidentCheckpoint?>("read", Token); }
        catch (JsonException) { }
        catch (JSException) { }
        if (saved?.Resident is not null && IsResidentIntent(saved.Intent)) _intent = saved.Intent;
        ResetResidentRoom(saved?.Resident, saved?.Cell);
    }

    private void ResetResidentRoom(HomeResidentState? previous, HomeCell? cell)
    {
        _residentSimulation = HomeSimulation.ForResident(_state!.Layout, previous, cell, _intent);
        _residentEnvironment = null;
        _residentFrame = _residentSimulation.Snapshot();
    }

    private void ResetResidentRoom() => ResetResidentRoom(_residentFrame?.Resident, _residentFrame?.AvatarCell);

    private async Task ChangeIntentAsync(string intent)
    {
        if (_working || _replay is not null || !IsResidentIntent(intent)) return;
        if (IsActive)
        {
            await ControlAsync(new("input", Intent: intent));
            if (_error is null) _runIntent = intent;
            return;
        }
        _intent = intent;
        _residentSimulation?.SetIntention(intent);
        await RememberResidentAsync();
    }

    [JSInvokable]
    public async Task AdvanceResidentAsync()
    {
        if (_disposed || _replay is not null || IsActive || _residentSimulation is not { } simulation) return;
        var environment = LightingSnapshot;
        if (!ReferenceEquals(_residentEnvironment, environment))
        {
            simulation.SetResidentEnvironment(environment);
            _residentEnvironment = environment;
        }
        for (var i = 0; i < ResidentTicksPerFrame; i++) simulation.AdvanceTick();
        var next = simulation.Snapshot();
        var previous = _residentFrame;
        _residentFrame = next;
        if (previous?.AvatarCell == next.AvatarCell && previous?.Activity == next.Activity &&
            previous?.Resident == next.Resident && previous?.Message == next.Message) return;
        StateHasChanged();
        await RememberResidentAsync();
    }

    private async Task RememberResidentAsync()
    {
        if (_residentFrame is { Resident: { } resident } frame && _residentClock is { } clock && !_disposed)
            await clock.InvokeVoidAsync("remember", Token, new ResidentCheckpoint(resident, frame.AvatarCell, _intent));
    }

    private async ValueTask CloseResidentRoomAsync()
    {
        if (_residentClock is not null)
        {
            await _residentClock.InvokeVoidAsync("close");
            await _residentClock.DisposeAsync();
        }
        _residentCallback?.Dispose();
    }
}
