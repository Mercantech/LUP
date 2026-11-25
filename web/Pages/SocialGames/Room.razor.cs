using System.Net;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Games;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Pages.SocialGames;

public partial class Room
{
    [CascadingParameter] public SocialGameLanguage Lang { get; set; } = new();
    [Parameter] public string Code { get; set; } = "";
    private SocialRoomView? RoomState;
    private CancellationTokenSource _scope = new();
    private string? _route;
    private Exception? _error;
    private bool InvalidCode => Code.Length != 6 || !Code.All(char.IsAsciiDigit);
    private string _nickname = "", _chat = "";
    private bool _loading, _busy, _canJoin, _canRetry, _connectionLost, _manualLink, _copied;
    private int _generation;
    private string InviteUrl => Nav.ToAbsoluteUri($"/games/social/{Uri.EscapeDataString(Code)}").AbsoluteUri;

    protected override Task OnParametersSetAsync()
    {
        if (_route == Code) return Task.CompletedTask;
        _route = Code;
        _scope.Cancel();
        _scope.Dispose();
        _scope = new();
        RoomState = null;
        _nickname = _chat = "";
        _busy = _connectionLost = _manualLink = _copied = false;
        return LoadRoomAsync();
    }

    private async Task LoadRoomAsync()
    {
        var token = _scope.Token;
        _error = null;
        _canJoin = _canRetry = false;
        if (InvalidCode)
        {
            _loading = false;
            return;
        }

        _loading = true;
        try
        {
            var result = await Client.GetAsync(Code);
            if (token.IsCancellationRequested) return;
            RoomState = result;
            _ = PollAsync(token);
        }
        catch (Exception ex) when (SocialText.IsRequestError(ex))
        {
            if (token.IsCancellationRequested) return;
            if (ex is SocialRoomException { Status: HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized }) _canJoin = true;
            else
            {
                _canRetry = ex is not SocialRoomException { Status: HttpStatusCode.NotFound };
                _error = ex;
            }
        }
        finally { if (!token.IsCancellationRequested) _loading = false; }
    }

    private async Task PollAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                if (RoomState?.Game?.Finished == true && !RoomState.CanChat) break;
                if (_busy) continue;
                var version = _generation;
                try
                {
                    var result = await Client.GetAsync(Code);
                    if (token.IsCancellationRequested) return;
                    if (version != _generation || _busy) continue;
                    RoomState = result;
                    _connectionLost = false;
                }
                catch (Exception ex) when (SocialText.IsRequestError(ex))
                {
                    if (token.IsCancellationRequested || version != _generation) continue;
                    if (ex is SocialRoomException { Status: HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized or HttpStatusCode.NotFound })
                    {
                        RoomState = null;
                        _error = ex;
                        _canRetry = true;
                        await InvokeAsync(StateHasChanged);
                        return;
                    }
                    _connectionLost = true;
                }
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private async Task<bool> UpdateAsync(Func<Task<SocialRoomView>> operation)
    {
        if (_busy) return false;
        var token = _scope.Token;
        _busy = true;
        _generation++;
        _error = null;
        try
        {
            var result = await operation();
            if (token.IsCancellationRequested) return false;
            RoomState = result;
            _connectionLost = false;
            return true;
        }
        catch (Exception ex) when (SocialText.IsRequestError(ex))
        {
            if (!token.IsCancellationRequested) _error = ex;
            return false;
        }
        finally { if (!token.IsCancellationRequested) _busy = false; }
    }

    private async Task JoinAsync()
    {
        var token = _scope.Token;
        if (await UpdateAsync(() => Client.JoinAsync(Code, Auth.IsAuthenticated ? null : _nickname.Trim())))
        {
            _canJoin = false;
            _ = PollAsync(token);
        }
    }

    private Task ChangeSettingsAsync(SocialGameSettings settings) => UpdateAsync(() => Client.SettingsAsync(Code, settings));
    private Task StartAsync() => UpdateAsync(() => Client.StartAsync(Code));
    private Task ActAsync(SocialGameActionRequest action) => UpdateAsync(() => Client.ActAsync(Code, action));

    private async Task SendChatAsync()
    {
        var draft = _chat;
        if (!string.IsNullOrWhiteSpace(draft) && await UpdateAsync(() => Client.ChatAsync(Code, draft.Trim())) && _chat == draft)
            _chat = "";
    }

    private async Task CopyAsync()
    {
        try { _copied = await JS.InvokeAsync<bool>("lupLive.copy", InviteUrl); }
        catch (JSException) { _copied = false; }
        _manualLink = !_copied;
    }

    private string PlayerStatus(SocialRoomView room, SocialPlayer player)
    {
        if (room.Game is { } game && !game.Alive.Contains(player.Id)) return Lang.T("Ude", "Eliminated");
        return player.IsGuest ? Lang.T("Gæst", "Guest") : "LUP";
    }

    public void Dispose()
    {
        _scope.Cancel();
        _scope.Dispose();
    }
}
