using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace Mercantec.Tech.Web.Auth;

public sealed class TokenStore(IJSRuntime js)
{
    public async Task<bool> SetTokensAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt, string loginEpoch) =>
        await js.InvokeAsync<bool>("mercantecAuth.setTokens", accessToken, refreshToken, expiresAt.ToUnixTimeSeconds(), loginEpoch);

    public async Task<(string? Access, string? Refresh, long ExpiresAt)> GetTokensAsync(
        DotNetObjectReference<AuthService>? receiveChanges = null)
    {
        var result = receiveChanges is null
            ? await js.InvokeAsync<TokenBag>("mercantecAuth.getTokens")
            : await js.InvokeAsync<TokenBag>("mercantecAuth.getTokens", receiveChanges);
        return (result?.Access, result?.Refresh, result?.ExpiresAt ?? 0);
    }

    public async Task<RefreshResult> RefreshTokensAsync(string url, string clientId, string? rejectedAccess, bool retryOnFailure) =>
        await js.InvokeAsync<RefreshResult>("mercantecAuth.refreshTokens", url, clientId, rejectedAccess, retryOnFailure);

    public async Task ClearTokensAsync(bool appLogout = false) =>
        await js.InvokeVoidAsync("mercantecAuth.clearTokens", appLogout);

    public async Task SetPkceAsync(string verifier, string state) =>
        await js.InvokeVoidAsync("mercantecAuth.setPkce", verifier, state);

    public async Task<(string? Verifier, string? State, string LoginEpoch)> GetPkceAsync()
    {
        var result = await js.InvokeAsync<PkceBag>("mercantecAuth.getPkce");
        return (result?.Verifier, result?.State, result?.LoginEpoch ?? string.Empty);
    }

    public async Task ClearPkceAsync() =>
        await js.InvokeVoidAsync("mercantecAuth.clearPkce");

    public async Task SetLoginReturnPathAsync(string path) =>
        await js.InvokeVoidAsync("mercantecAuth.setLoginReturnPath", path);

    public async Task<string?> ConsumeLoginReturnPathAsync() =>
        await js.InvokeAsync<string?>("mercantecAuth.consumeLoginReturnPath");

    public async Task ClearLoginReturnPathAsync() =>
        await js.InvokeVoidAsync("mercantecAuth.clearLoginReturnPath");

    public readonly record struct RefreshResult(
        [property: JsonPropertyName("access")] string? Access,
        [property: JsonPropertyName("temporaryFailure")] bool TemporaryFailure,
        [property: JsonPropertyName("sessionEnded")] bool SessionEnded);

    private sealed class TokenBag
    {
        [JsonPropertyName("access")]
        public string? Access { get; set; }

        [JsonPropertyName("refresh")]
        public string? Refresh { get; set; }

        [JsonPropertyName("expiresAt")]
        public long ExpiresAt { get; set; }
    }

    private sealed class PkceBag
    {
        [JsonPropertyName("verifier")]
        public string? Verifier { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("loginEpoch")]
        public string? LoginEpoch { get; set; }
    }
}
