using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Mercantec.Tech.Contracts;
using Microsoft.Extensions.Options;

namespace Mercantec.Tech.Api.Integrations.Azure;

public sealed class AzureGraphCalendarClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AzureGraphOptions> options,
    ILogger<AzureGraphCalendarClient> logger)
{
    private const string EventFields = "id,subject,start,end,isAllDay,location,sensitivity,isOnlineMeeting,onlineMeeting,webLink,isCancelled,showAs";

    private static readonly Regex DuplicateMailtoAnnotation = new(
        @"(?<address>[^@\s<>]+@[^@\s<>]+)[ \t]*<mailto:(?<target>[^<>\s]+)>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AzureGraphOptions _options = options.Value;
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt;

    public bool IsConfigured => _options.IsConfigured;

    public string? ResolveTeacherUpn(string? sub, string? email = null) =>
        _options.ResolveTeacherUpn(sub, email);

    public IReadOnlyDictionary<string, string> GetCalendarCopyMailboxes(string? sub, string? email)
    {
        var targets = _options.ParseCalendarWriteTargets();
        var caller = _options.ResolveMappedTeacherUpn(sub, email);
        return caller is not null && targets.Values.Contains(caller, StringComparer.OrdinalIgnoreCase)
            ? targets : new Dictionary<string, string>();
    }

    public async Task<string> CreateCalendarEventAsync(string targetUpn, HoldCalendarEventView appointment,
        string transactionId, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Azure Graph er ikke konfigureret");
        if (!_options.ParseCalendarWriteTargets().Values.Contains(targetUpn, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Postkassen er ikke konfigureret til Outlook-kopier.", nameof(targetUpn));
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);

        var token = await GetAccessTokenAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(targetUpn)}/calendar/events")
        {
            Content = JsonContent.Create(new
            {
                subject = appointment.Title,
                body = new { contentType = "text", content = appointment.Description },
                start = CalendarWriteTime(appointment.StartUtc),
                end = CalendarWriteTime(appointment.EndUtc),
                location = new { displayName = appointment.Location },
                transactionId
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var http = httpClientFactory.CreateClient("azure-graph");
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Graph kalenderoprettelse fejlede: {Status}", (int)response.StatusCode);
            throw new HttpRequestException("Kunne ikke oprette Outlook-aftalen.", null, response.StatusCode);
        }

        var created = await response.Content.ReadFromJsonAsync<GraphEvent>(JsonOptions, ct);
        if (string.IsNullOrWhiteSpace(created?.Id))
            throw new HttpRequestException("Outlook svarede uden id for aftalen.");
        return created.Id;
    }

    private static object CalendarWriteTime(DateTimeOffset time) => new
    {
        dateTime = time.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture),
        timeZone = "UTC"
    };

    public async Task<IReadOnlyList<TeacherCalendarEvent>> GetCalendarViewAsync(
        string azureUpn,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Azure Graph er ikke konfigureret");

        var token = await GetAccessTokenAsync(ct);
        var http = httpClientFactory.CreateClient("azure-graph");
        var events = new List<TeacherCalendarEvent>();

        var query = new Dictionary<string, string>
        {
            ["startDateTime"] = from.UtcDateTime.ToString("o"),
            ["endDateTime"] = to.UtcDateTime.ToString("o"),
            ["$select"] = EventFields,
            ["$orderby"] = "start/dateTime",
            ["$top"] = "100"
        };
        var qs = string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        string? url =
            $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(azureUpn)}/calendarView?{qs}";

        while (url is not null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation("Prefer", "outlook.timezone=\"Romance Standard Time\"");

            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Graph calendarView fejlede: {Status}", (int)response.StatusCode);
                throw new HttpRequestException(
                    $"Graph calendarView fejlede ({(int)response.StatusCode})", null, response.StatusCode);
            }

            var page = await response.Content.ReadFromJsonAsync<GraphEventPage>(JsonOptions, ct)
                ?? new GraphEventPage();
            foreach (var item in page.Value ?? [])
                events.Add(Map(item));

            url = page.NextLink;
        }

        return events;
    }

    public async Task<TeacherCalendarEventDetails> GetEventAsync(string azureUpn, string eventId, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        var fields = Uri.EscapeDataString($"{EventFields},body,bodyPreview,organizer,attendees,hideAttendees,isOrganizer");
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(azureUpn)}/events/{Uri.EscapeDataString(eventId)}?$select={fields}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("Prefer", "outlook.timezone=\"Romance Standard Time\", outlook.body-content-type=\"text\"");

        var http = httpClientFactory.CreateClient("azure-graph");
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Graph aftaleopslag fejlede: {Status}", (int)response.StatusCode);
            throw new HttpRequestException("Kunne ikke hente Outlook-aftalen.", null, response.StatusCode);
        }

        var item = await response.Content.ReadFromJsonAsync<GraphEvent>(JsonOptions, ct);
        if (item is null || string.IsNullOrWhiteSpace(item.Id))
            throw new HttpRequestException("Outlook svarede uden en aftale.");

        var summary = Map(item);
        if (summary.IsPrivate)
            return new TeacherCalendarEventDetails(summary, null, null, [], true);

        // Outlook skjuler deltagerlisten for modtagere, når arrangøren har valgt det.
        var hideAttendees = item.HideAttendees == true && item.IsOrganizer != true;
        var attendees = new List<TeacherCalendarAttendee>();
        if (!hideAttendees && item.Attendees is not null)
        {
            foreach (var attendee in item.Attendees)
            {
                if (attendee is not null && MapPerson(attendee.EmailAddress) is { } person)
                    attendees.Add(new(person.Name, person.Address, NullIfEmpty(attendee.Type), NullIfEmpty(attendee.Status?.Response)));
            }
        }

        return new TeacherCalendarEventDetails(summary, DescriptionText(item.Body) ?? PlainDescriptionText(item.BodyPreview),
            MapPerson(item.Organizer?.EmailAddress), attendees, hideAttendees);
    }

    private static TeacherCalendarPerson? MapPerson(GraphEmailAddress? email)
    {
        var name = NullIfEmpty(email?.Name);
        var address = NullIfEmpty(email?.Address);
        return name is null && address is null ? null : new(name, address);
    }

    private static string? DescriptionText(GraphEventBody? body)
    {
        if (body?.Content is not { } content || string.IsNullOrWhiteSpace(content))
            return null;
        if (string.Equals(body.ContentType, "text", StringComparison.OrdinalIgnoreCase))
            return PlainDescriptionText(content);
        if (!string.Equals(body.ContentType, "html", StringComparison.OrdinalIgnoreCase))
            return null;

        // Parse without a network loader. Only text is extracted from Outlook's document.
        using var document = new HtmlParser().ParseDocument(content);
        if (document.Body is not { } documentBody)
            return null;
        foreach (var node in documentBody.QuerySelectorAll("script,style,template,noscript,iframe,object,svg,[hidden]"))
            node.Remove();
        foreach (var node in documentBody.QuerySelectorAll("p,div,li,br,hr,h1,h2,h3,h4,h5,h6,blockquote,tr"))
            node.Parent?.InsertBefore(document.CreateTextNode("\n"), node.NextSibling);

        return NullIfEmpty(string.Join('\n',
            documentBody.TextContent.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)));
    }

    private static string? PlainDescriptionText(string? value)
    {
        if (NullIfEmpty(value) is not { } text)
            return null;

        // Outlook's plain-text conversion can append the link target to an already visible email address.
        return DuplicateMailtoAnnotation.Replace(text, static match =>
        {
            var address = match.Groups["address"].Value;
            return address == match.Groups["target"].Value ? address : match.Value;
        });
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cachedToken is not null && _tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return _cachedToken;

        var http = httpClientFactory.CreateClient("azure-graph");
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["scope"] = "https://graph.microsoft.com/.default",
            ["grant_type"] = "client_credentials"
        });

        var tokenUrl =
            $"https://login.microsoftonline.com/{Uri.EscapeDataString(_options.TenantId)}/oauth2/v2.0/token";
        using var response = await http.PostAsync(tokenUrl, content, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Azure token-fejl: {Status}", (int)response.StatusCode);
            throw new HttpRequestException($"Kunne ikke hente Azure-token ({(int)response.StatusCode})");
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, ct);
        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            throw new HttpRequestException("Azure svarede uden access-token.");
        _cachedToken = token.AccessToken;
        _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn - 60));
        return _cachedToken;
    }

    private static TeacherCalendarEvent Map(GraphEvent e)
    {
        var isAllDay = e.IsAllDay == true;
        var isPrivate = e.Sensitivity?.ToLowerInvariant() is "private" or "personal" or "confidential";
        var start = ParseGraphDate(e.Start, isAllDay);
        var end = ParseGraphDate(e.End, isAllDay);
        return new TeacherCalendarEvent(
            e.Id ?? Guid.NewGuid().ToString("N"),
            isPrivate ? "Privat" : (string.IsNullOrWhiteSpace(e.Subject) ? "(uden titel)" : e.Subject.Trim()),
            start,
            end,
            isAllDay,
            isPrivate ? null : NullIfEmpty(e.Location?.DisplayName),
            !isPrivate && e.IsOnlineMeeting == true,
            isPrivate ? null : NullIfEmpty(e.OnlineMeeting?.JoinUrl),
            isPrivate ? null : NullIfEmpty(e.WebLink),
            isPrivate,
            e.IsCancelled == true,
            NullIfEmpty(e.ShowAs));
    }

    private static DateTimeOffset ParseGraphDate(GraphDateTime? value, bool isAllDay)
    {
        if (value?.DateTime is null)
            return DateTimeOffset.UtcNow;

        var raw = value.DateTime;
        if (isAllDay)
        {
            var date = DateOnly.Parse(raw.Length >= 10 ? raw[..10] : raw);
            return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        }

        // Romance Standard Time / lokal tid uden Z → behandl som local wall-clock (DK).
        if (DateTimeOffset.TryParse(raw, out var dto))
        {
            if (raw.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
                || raw.Contains('+', StringComparison.Ordinal)
                || (raw.Length > 19 && raw[^6] is '+' or '-'))
                return dto;
            var unspecified = DateTime.Parse(raw, null, System.Globalization.DateTimeStyles.AssumeLocal);
            var tz = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Copenhagen");
            var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(unspecified, DateTimeKind.Unspecified), tz);
            return new DateTimeOffset(utc);
        }

        return DateTimeOffset.UtcNow;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private sealed class GraphEventPage
    {
        [JsonPropertyName("value")]
        public List<GraphEvent>? Value { get; set; }

        [JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; set; }
    }

    private sealed class GraphEvent
    {
        public string? Id { get; set; }
        public string? Subject { get; set; }
        public bool? IsAllDay { get; set; }
        public string? Sensitivity { get; set; }
        public GraphDateTime? Start { get; set; }
        public GraphDateTime? End { get; set; }
        public GraphLocation? Location { get; set; }
        public bool? IsOnlineMeeting { get; set; }
        public GraphOnlineMeeting? OnlineMeeting { get; set; }
        public string? WebLink { get; set; }
        public bool? IsCancelled { get; set; }
        public string? ShowAs { get; set; }
        public GraphEventBody? Body { get; set; }
        public string? BodyPreview { get; set; }
        public GraphRecipient? Organizer { get; set; }
        public List<GraphAttendee?>? Attendees { get; set; }
        public bool? HideAttendees { get; set; }
        public bool? IsOrganizer { get; set; }
    }

    private sealed class GraphEventBody
    {
        public string? ContentType { get; set; }
        public string? Content { get; set; }
    }

    private sealed class GraphRecipient
    {
        public GraphEmailAddress? EmailAddress { get; set; }
    }

    private sealed class GraphEmailAddress
    {
        public string? Name { get; set; }
        public string? Address { get; set; }
    }

    private sealed class GraphAttendee
    {
        public GraphEmailAddress? EmailAddress { get; set; }
        public string? Type { get; set; }
        public GraphResponseStatus? Status { get; set; }
    }

    private sealed class GraphResponseStatus
    {
        public string? Response { get; set; }
    }

    private sealed class GraphDateTime
    {
        public string? DateTime { get; set; }
        public string? TimeZone { get; set; }
    }

    private sealed class GraphLocation
    {
        public string? DisplayName { get; set; }
    }

    private sealed class GraphOnlineMeeting
    {
        public string? JoinUrl { get; set; }
    }
}
