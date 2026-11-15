using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Web.Auth;

public sealed class AuthService(
    IOptions<AuthOptions> options,
    TokenStore tokens,
    NavigationManager navigation)
{
    private readonly AuthOptions _options = options.Value;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private static readonly HttpClient TokenHttp = new();

    public bool IsInitialized { get; private set; }
    public bool IsAuthenticated { get; private set; }
    public bool SessionRecoveryPending { get; private set; }
    public string? AccessToken { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Subject { get; private set; }
    public string? Email { get; private set; }
    public string? LoginMethod { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = [];

    public event Action? AuthStateChanged;

    public async Task InitializeAsync()
    {
        var (access, refresh, expiresAt) = await tokens.GetTokensAsync();
        if (CanUseAccessToken(access, expiresAt) && (!IsExpiring(expiresAt) || string.IsNullOrEmpty(refresh)))
        {
            ApplyAccessToken(access!);
        }
        else if (!string.IsNullOrEmpty(refresh))
        {
            var result = await RefreshAsync(retryOnFailure: true);
            SessionRecoveryPending = result.Access is null && result.TemporaryFailure;
        }
        else
        {
            if (!string.IsNullOrEmpty(access)) await tokens.ClearTokensAsync();
            ClearLocalState();
        }

        IsInitialized = true;
        Notify();
    }

    public string LoginPath() => AuthReturnPath.LoginUrl(CurrentReturnPath());

    private string? CurrentReturnPath()
    {
        var path = "/" + navigation.ToBaseRelativePath(navigation.Uri);
        if (AuthReturnPath.Normalize(path) is { } current) return current;
        var uri = navigation.ToAbsoluteUri(navigation.Uri);
        if (!uri.AbsolutePath.TrimEnd('/').Equals("/login", StringComparison.OrdinalIgnoreCase)) return null;
        return AuthReturnPath.Normalize(System.Web.HttpUtility.ParseQueryString(uri.Query)["returnUrl"]);
    }

    public async Task<string?> ConsumeLoginReturnPathAsync(string? explicitPath = null)
    {
        var stored = await tokens.ConsumeLoginReturnPathAsync();
        return AuthReturnPath.Normalize(explicitPath) ?? AuthReturnPath.Normalize(stored);
    }

    public async Task BeginLoginAsync(string? returnPath = null)
    {
        var target = AuthReturnPath.Normalize(returnPath) ?? CurrentReturnPath();
        if (target is not null) await tokens.SetLoginReturnPathAsync(target);
        var verifier = Pkce.CreateVerifier();
        var challenge = Pkce.CreateChallenge(verifier);
        var state = Pkce.CreateState();
        await tokens.SetPkceAsync(verifier, state);

        var redirectUri = GetRedirectUri();
        var url =
            $"{_options.AuthorizeUrl}" +
            $"?response_type=code" +
            $"&client_id={Uri.EscapeDataString(_options.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&scope={Uri.EscapeDataString(_options.Scope)}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&code_challenge={Uri.EscapeDataString(challenge)}" +
            $"&code_challenge_method=S256";

        // Fuld browser-navigation — ikke fetch
        navigation.NavigateTo(url, forceLoad: true);
    }

    public async Task<bool> CompleteLoginAsync(string? code, string? state, string? error)
    {
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
            return false;

        var (verifier, expectedState) = await tokens.GetPkceAsync();
        await tokens.ClearPkceAsync();

        if (string.IsNullOrEmpty(verifier) || string.IsNullOrEmpty(expectedState) || expectedState != state)
            return false;

        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = GetRedirectUri(),
            ["client_id"] = _options.ClientId,
            ["code_verifier"] = verifier
        };

        var (payload, _) = await RequestTokensAsync(body);
        if (payload is null) return false;

        await StoreTokensAsync(payload, existingRefresh: null);
        return true;
    }

    public async Task<string?> GetValidAccessTokenAsync()
    {
        var (access, refresh, expiresAt) = await tokens.GetTokensAsync();
        if (CanUseAccessToken(access, expiresAt) && (!IsExpiring(expiresAt) || string.IsNullOrEmpty(refresh)))
        {
            if (!IsAuthenticated) ApplyAccessToken(access!);
            return access;
        }

        return string.IsNullOrEmpty(refresh) ? null : (await RefreshAsync()).Access;
    }

    public async Task<string?> RefreshAfterUnauthorizedAsync(string rejectedAccess) =>
        (await RefreshAsync(rejectedAccess)).Access;

    public async Task LogoutLocalAsync()
    {
        // Kun lokal logout — ryd tokens/PKCE. Kald ikke auth-host /signout (SSO-session
        // kan leve videre). Undgå at starte /oauth/authorize automatisk efter logud;
        // ellers logger SSO brugeren stille ind igen.
        await _refreshLock.WaitAsync();
        try
        {
            await tokens.ClearTokensAsync();
            await tokens.ClearPkceAsync();
            await tokens.ClearLoginReturnPathAsync();
            ClearLocalState();
            IsInitialized = true;
            Notify();
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<(string? Access, bool TemporaryFailure)> RefreshAsync(
        string? rejectedAccess = null, bool retryOnFailure = false)
    {
        await _refreshLock.WaitAsync();
        try
        {
            // Read inside the lock so queued requests use the latest rotated credentials.
            var (access, refresh, expiresAt) = await tokens.GetTokensAsync();
            if (access != rejectedAccess && CanUseAccessToken(access, expiresAt)
                && (rejectedAccess is not null || !IsExpiring(expiresAt)))
                return (access, false);

            if (string.IsNullOrEmpty(refresh)) return (null, false);

            var body = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refresh,
                ["client_id"] = _options.ClientId
            };
            var (payload, invalidGrant) = await RequestTokensAsync(body);
            if (payload is null && !invalidGrant && retryOnFailure && !CanUseAccessToken(access, expiresAt))
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                (payload, invalidGrant) = await RequestTokensAsync(body);
            }

            if (payload is not null)
            {
                await StoreTokensAsync(payload, refresh);
                return (payload.AccessToken, false);
            }

            if (invalidGrant)
            {
                await tokens.ClearTokensAsync();
                ClearLocalState();
                if (IsInitialized) Notify();
                return (null, false);
            }

            // A temporary outage can leave the previous access token usable until its expiry.
            if (rejectedAccess is null && CanUseAccessToken(access, expiresAt))
            {
                if (!IsAuthenticated) ApplyAccessToken(access!);
                return (access, true);
            }

            return (null, true);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<(TokenResponse? Payload, bool InvalidGrant)> RequestTokensAsync(Dictionary<string, string> body)
    {
        try
        {
            using var content = new FormUrlEncodedContent(body);
            using var response = await TokenHttp.PostAsync(_options.TokenUrl, content);
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var failure = await response.Content.ReadFromJsonAsync<TokenResponse>();
                return (null, failure?.Error == "invalid_grant");
            }
            if (!response.IsSuccessStatusCode) return (null, false);

            var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();
            return string.IsNullOrWhiteSpace(payload?.AccessToken) ? (null, false) : (payload, false);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            return (null, false);
        }
    }

    private async Task StoreTokensAsync(TokenResponse payload, string? existingRefresh)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn > 0 ? payload.ExpiresIn : 900);
        await tokens.SetTokensAsync(payload.AccessToken!, payload.RefreshToken ?? existingRefresh ?? string.Empty, expiresAt);
        ApplyAccessToken(payload.AccessToken!);
        if (IsInitialized) Notify();
    }

    private static bool CanUseAccessToken(string? access, long expiresAt) =>
        !string.IsNullOrEmpty(access) && (expiresAt <= 0 || DateTimeOffset.UtcNow.ToUnixTimeSeconds() < expiresAt);

    private static bool IsExpiring(long expiresAt) =>
        expiresAt > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expiresAt - 30;

    private void ApplyAccessToken(string accessToken)
    {
        AccessToken = accessToken;
        IsAuthenticated = true;
        SessionRecoveryPending = false;
        try
        {
            // Undgå DefaultInboundClaimTypeMap ("role" → lang ClaimTypes.Role-URI)
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler
            {
                MapInboundClaims = false
            };
            var jwt = handler.ReadJwtToken(accessToken);

            Subject = FirstClaim(jwt, "sub");
            Email = FirstClaim(jwt, "email");
            LoginMethod = FirstClaim(jwt, "login_method");
            DisplayName = FirstClaim(jwt, "name") ?? Email ?? Subject;
            Roles = ReadRoles(jwt);
        }
        catch
        {
            DisplayName = null;
            Subject = null;
            Email = null;
            LoginMethod = null;
            Roles = [];
        }
    }

    private static string? FirstClaim(System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwt, string type) =>
        jwt.Claims.FirstOrDefault(c => c.Type == type)?.Value
        ?? jwt.Payload.Claims.FirstOrDefault(c => c.Type == type)?.Value;

    private static string[] ReadRoles(System.IdentityModel.Tokens.Jwt.JwtSecurityToken jwt)
    {
        // Gentagne "role"-claims og evt. array i payload
        var fromClaims = jwt.Claims
            .Where(IsRoleClaim)
            .Select(c => c.Value)
            .ToList();

        if (jwt.Payload.TryGetValue("role", out var roleValue))
            fromClaims.AddRange(FlattenPayloadRoles(roleValue));

        if (jwt.Payload.TryGetValue("roles", out var rolesValue))
            fromClaims.AddRange(FlattenPayloadRoles(rolesValue));

        return fromClaims
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(r => r)
            .ToArray();
    }

    private static bool IsRoleClaim(System.Security.Claims.Claim c) =>
        c.Type is "role" or "roles"
        || c.Type.Equals(System.Security.Claims.ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
        || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> FlattenPayloadRoles(object? value) => value switch
    {
        string s => [s],
        System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.String } el => [el.GetString()!],
        System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Array } arr =>
            arr.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0),
        System.Collections.IEnumerable list when value is not string =>
            list.Cast<object?>().Select(o => o?.ToString() ?? "").Where(x => x.Length > 0),
        _ => []
    };

    private void ClearLocalState()
    {
        AccessToken = null;
        IsAuthenticated = false;
        SessionRecoveryPending = false;
        DisplayName = null;
        Subject = null;
        Email = null;
        LoginMethod = null;
        Roles = [];
    }

    private string GetRedirectUri()
    {
        var baseUri = navigation.BaseUri.TrimEnd('/');
        var path = _options.RedirectPath.StartsWith('/') ? _options.RedirectPath : "/" + _options.RedirectPath;
        return baseUri + path;
    }

    private void Notify() => AuthStateChanged?.Invoke();

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
