using System.Text.Json;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Integrations.Lectio;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Students;

public sealed class StudentDirectoryService(AppDbContext db, LectioCollectorClient collector, ILogger<StudentDirectoryService> log, TimeProvider? clock = null)
{
    private static readonly TimeZoneInfo SchoolTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    public async Task<StudentDirectoryResponse> ListAsync(string bearerToken, CancellationToken ct = default, Guid? requestingTeacher = null, bool includeTeacherOverrides = true)
    {
        var memberships = await db.Memberships.AsNoTracking().Include(m => m.Hold).ThenInclude(h => h.Forloeb).ToListAsync(ct);
        var teacherSubjects = (await db.NotebookProfiles.AsNoTracking().Where(p => p.IsTeacher).Select(p => p.Sub).ToListAsync(ct)).ToHashSet();
        teacherSubjects.UnionWith(await db.Forloeb.AsNoTracking().Select(f => f.CreatedBySub).ToListAsync(ct));
        if (requestingTeacher is Guid teacher) teacherSubjects.Add(teacher);
        memberships.RemoveAll(m => m.UserSub is Guid subject && teacherSubjects.Contains(subject));

        var links = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in memberships.Where(m => ValidLectioId(m.LectioId) && m.UserSub.HasValue).GroupBy(m => m.LectioId!, StringComparer.OrdinalIgnoreCase))
        {
            var subjects = group.Select(m => m.UserSub!.Value).Distinct().ToArray();
            if (subjects.Length == 1) links[group.Key] = subjects[0];
        }

        var imported = new List<Row>();
        var lectioAvailable = false;
        string? warning = null;
        // Two bulk calls at most; never one study-plan request per student.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        try
        {
            var data = await collector.GetProgrammeringseleverAsync(bearerToken, timeout.Token);
            var schoolToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime((clock ?? TimeProvider.System).GetUtcNow(), SchoolTimeZone).DateTime);
            imported = ReadLectioStudents(data, schoolToday);
            lectioAvailable = true;
            try
            {
                var resolved = await collector.ResolveAuthLinksAsync(imported.Select(s => s.LectioId!).Concat(memberships.Where(m => ValidLectioId(m.LectioId)).Select(m => m.LectioId!)), bearerToken, timeout.Token);
                foreach (var (id, sub) in resolved)
                {
                    // An existing claimed membership is not reassigned because an external link changed.
                    if (sub != Guid.Empty && !links.ContainsKey(id)) links[id] = sub;
                }
            }
            catch (Exception error) when (Unavailable(error, ct))
            {
                warning = "Kontokoblinger fra Lectio kunne ikke opdateres. Eleverne vises med de kendte koblinger.";
                log.LogWarning("Student directory account links unavailable ({ErrorType}).", error.GetType().Name);
            }
        }
        catch (Exception error) when (Unavailable(error, ct))
        {
            warning = "Lectio er midlertidigt utilgængelig. Oversigten viser elever fra LUPs hold; Lectio-elever uden et LUP-hold kan mangle.";
            log.LogWarning("Student directory Lectio source unavailable ({ErrorType}).", error.GetType().Name);
        }

        var rows = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
        var byUser = new Dictionary<Guid, Row>();
        foreach (var row in imported)
        {
            if (links.TryGetValue(row.LectioId!, out var user)) row.UserSub = user;
            if (row.UserSub is Guid sub && teacherSubjects.Contains(sub)) continue;
            if (row.UserSub is Guid account && byUser.TryGetValue(account, out var existing))
            {
                // One account is displayed once even if Collector supplies duplicate identities.
                rows[row.Key] = existing;
                continue;
            }
            rows[row.Key] = row;
            if (row.UserSub is Guid subject) byUser[subject] = row;
        }

        foreach (var member in memberships.OrderBy(m => ValidLectioId(m.LectioId) ? 0 : 1))
        {
            var lectioId = ValidLectioId(member.LectioId) ? member.LectioId!.Trim() : null;
            var user = lectioId is not null && links.TryGetValue(lectioId, out var resolved) ? resolved : member.UserSub;
            if (user is Guid subject && teacherSubjects.Contains(subject)) continue;
            if (lectioId is null && (user is null || user == Guid.Empty)) continue;
            var key = lectioId is not null ? LectioKey(lectioId) : UserKey(user!.Value);
            if (!rows.TryGetValue(key, out var row) && !(user is Guid account && byUser.TryGetValue(account, out row)))
            {
                row = new Row { Key = key, LectioId = lectioId, UserSub = user, DisplayName = Clean(member.DisplayName) ?? "Ukendt elev" };
                rows[key] = row;
            }
            row!.UserSub ??= user;
            if (row.UserSub is Guid linked) byUser[linked] = row;
            if (row.Holds.All(h => h.Id != member.HoldId))
                row.Holds.Add(new(member.HoldId, member.Hold.Name, member.Hold.Forloeb.Name));
        }

        var students = rows.Values.Distinct().ToList();
        var subjectsToRead = students.Where(r => r.UserSub.HasValue).Select(r => r.UserSub!.Value).ToArray();
        var names = await db.ProfileNames.AsNoTracking().Where(p => subjectsToRead.Contains(p.UserSub)).ToDictionaryAsync(p => p.UserSub, p => p.DisplayName, ct);
        var notebookNames = await db.NotebookProfiles.AsNoTracking().Where(p => subjectsToRead.Contains(p.Sub)).ToDictionaryAsync(p => p.Sub, p => p.DisplayName, ct);
        var avatars = await db.AvatarProfiles.AsNoTracking().Where(p => subjectsToRead.Contains(p.UserSub)).ToDictionaryAsync(p => p.UserSub, p => p.SelectedAvatarId, ct);
        var photos = (await db.ProfilePhotos.AsNoTracking().Where(p => subjectsToRead.Contains(p.UserSub)).Select(p => p.UserSub).ToListAsync(ct)).ToHashSet();
        var details = await db.StudentDirectoryDetails.AsNoTracking().ToListAsync(ct);
        var result = new List<StudentDirectoryStudent>();
        foreach (var row in students)
        {
            var name = row.DisplayName;
            if (row.UserSub is Guid sub)
            {
                if (names.TryGetValue(sub, out var ownName)) name = ownName;
                else if (name == "Ukendt elev" && notebookNames.TryGetValue(sub, out var registeredName)) name = registeredName;
            }
            var correction = includeTeacherOverrides ? MergeCorrections(details.Where(d => Matches(d, row.Key, row.LectioId, row.UserSub))) : null;
            var student = new StudentDirectoryStudent(row.Key, row.LectioId, row.UserSub, name, row.ClassName,
                row.Holds.OrderBy(h => h.Name, StringComparer.CurrentCultureIgnoreCase).ToList(), row.Placement,
                row.CurrentSchoolPeriod, row.WorkplaceName, row.LastPassedSchoolPeriod, false,
                row.UserSub is Guid avatarSub && avatars.TryGetValue(avatarSub, out var avatar) ? avatar : null,
                row.UserSub is Guid photoSub && photos.Contains(photoSub), null);
            result.Add(Apply(student, correction));
        }
        return new(result.OrderBy(s => s.DisplayName, StringComparer.CurrentCultureIgnoreCase).ThenBy(s => s.Key).ToList(), lectioAvailable, warning);
    }

    public async Task<StudentDirectoryStudent?> FindAsync(string key, string bearerToken, CancellationToken ct = default, Guid? requestingTeacher = null) =>
        (await ListAsync(bearerToken, ct, requestingTeacher)).Students.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public async Task<StudentDirectoryStudent> UpdateAsync(StudentDirectoryStudent student, UpdateStudentDirectoryDetails request, Guid teacherSub, CancellationToken ct)
    {
        var rows = await db.StudentDirectoryDetails.ToListAsync(ct);
        var matches = rows.Where(d => Matches(d, student.Key, student.LectioId, student.UserSub)).ToArray();
        var previous = MergeCorrections(matches);
        if (request.ResetToLectio)
        {
            db.StudentDirectoryDetails.RemoveRange(matches);
            await db.SaveChangesAsync(ct);
            // The caller reloads the unmodified source to avoid returning the old override.
            return student with { HasTeacherOverrides = false, UpdatedAtUtc = null };
        }
        if (request.Placement is null && request.CurrentSchoolPeriod is null && request.WorkplaceName is null && request.LastPassedSchoolPeriod is null)
            return student;
        var entry = matches.FirstOrDefault(d => d.Key == student.Key);
        if (entry is null)
        {
            entry = new StudentDirectoryDetails { Key = student.Key };
            db.StudentDirectoryDetails.Add(entry);
        }
        entry.LectioId = student.LectioId;
        entry.UserSub = student.UserSub;
        entry.Placement = request.Placement?.Trim().ToLowerInvariant() ?? previous?.Placement;
        entry.CurrentSchoolPeriod = request.CurrentSchoolPeriod?.Trim() ?? previous?.CurrentSchoolPeriod;
        entry.WorkplaceName = request.WorkplaceName?.Trim() ?? previous?.WorkplaceName;
        entry.LastPassedSchoolPeriod = request.LastPassedSchoolPeriod?.Trim() ?? previous?.LastPassedSchoolPeriod;
        entry.UpdatedBySub = teacherSub;
        entry.UpdatedAtUtc = (clock ?? TimeProvider.System).GetUtcNow();
        db.StudentDirectoryDetails.RemoveRange(matches.Where(d => d != entry));
        await db.SaveChangesAsync(ct);
        return Apply(student, entry);
    }

    public static bool ValidRequest(UpdateStudentDirectoryDetails? request) => request is not null && (request.ResetToLectio ||
        (request.Placement is null || request.Placement is "school" or "workplace" or "unknown")
        && ValidText(request.CurrentSchoolPeriod, 160) && ValidText(request.WorkplaceName, 200) && ValidText(request.LastPassedSchoolPeriod, 120));

    private static StudentDirectoryStudent Apply(StudentDirectoryStudent student, StudentDirectoryDetails? correction)
    {
        if (correction is null) return student;
        var placement = correction.Placement ?? student.Placement;
        return student with
        {
            Placement = placement,
            CurrentSchoolPeriod = placement == "school" ? Clean(correction.CurrentSchoolPeriod ?? student.CurrentSchoolPeriod) : null,
            WorkplaceName = Clean(correction.WorkplaceName ?? student.WorkplaceName),
            LastPassedSchoolPeriod = Clean(correction.LastPassedSchoolPeriod ?? student.LastPassedSchoolPeriod),
            HasTeacherOverrides = true,
            UpdatedAtUtc = correction.UpdatedAtUtc
        };
    }

    private static bool Matches(StudentDirectoryDetails details, string key, string? lectioId, Guid? subject) =>
        details.LectioId is not null
            ? lectioId is not null && string.Equals(details.LectioId, lectioId, StringComparison.OrdinalIgnoreCase)
            : details.Key.Equals(key, StringComparison.OrdinalIgnoreCase) || subject is not null && details.UserSub == subject;

    private static StudentDirectoryDetails? MergeCorrections(IEnumerable<StudentDirectoryDetails> corrections)
    {
        var ordered = corrections.OrderByDescending(d => d.UpdatedAtUtc).ToArray();
        if (ordered.Length == 0) return null;
        return new()
        {
            Placement = ordered.Select(d => d.Placement).FirstOrDefault(value => value is not null),
            CurrentSchoolPeriod = ordered.Select(d => d.CurrentSchoolPeriod).FirstOrDefault(value => value is not null),
            WorkplaceName = ordered.Select(d => d.WorkplaceName).FirstOrDefault(value => value is not null),
            LastPassedSchoolPeriod = ordered.Select(d => d.LastPassedSchoolPeriod).FirstOrDefault(value => value is not null),
            UpdatedAtUtc = ordered[0].UpdatedAtUtc
        };
    }

    private static List<Row> ReadLectioStudents(JsonElement data, DateOnly today)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("students", out var students) || students.ValueKind != JsonValueKind.Array)
            throw new JsonException("Invalid student directory shape.");
        var result = new List<Row>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var student in students.EnumerateArray())
        {
            var id = Text(student, "lectioId");
            if (!ValidLectioId(id) || !seen.Add(id!)) continue;
            var row = new Row { Key = LectioKey(id!), LectioId = id, DisplayName = Text(student, "name") ?? "Ukendt elev", ClassName = Text(student, "className") };
            if (Object(student, "workplace", out var workplace)) row.WorkplaceName = Text(workplace, "name");
            // Only an explicit placement or a dated current school period establishes whereabouts.
            var placement = Text(student, "placement") ?? Text(student, "currentPlacement");
            if (placement is "school" or "workplace") row.Placement = placement;
            if (Object(student, "workplace", out workplace) && Boolean(workplace, "isCurrent") == true) row.Placement = "workplace";
            var periods = new List<JsonElement>();
            if (Object(student, "h6", out var h6)) periods.Add(h6);
            if (student.TryGetProperty("schoolPeriods", out var array) && array.ValueKind == JsonValueKind.Array) periods.AddRange(array.EnumerateArray());
            foreach (var period in periods)
            {
                if (Date(period, "startDate") is DateOnly start && Date(period, "endDate") is DateOnly end && start <= today && today <= end)
                {
                    row.Placement = "school";
                    row.CurrentSchoolPeriod = Text(period, "level") ?? Text(period, "name");
                    break;
                }
            }
            row.LastPassedSchoolPeriod = Text(student, "lastPassedSchoolPeriod");
            if (row.LastPassedSchoolPeriod is null)
            {
                // Never equate an ended period with an exam/course having been passed.
                var passed = periods.Where(p => Boolean(p, "passed") == true || Text(p, "status") == "passed")
                    .OrderByDescending(p => Date(p, "endDate")).FirstOrDefault();
                row.LastPassedSchoolPeriod = Text(passed, "level") ?? Text(passed, "name");
            }
            result.Add(row);
        }
        return result;
    }

    private static bool Unavailable(Exception error, CancellationToken ct) =>
        error is HttpRequestException or LectioCollectorException or JsonException || error is OperationCanceledException && !ct.IsCancellationRequested;
    private static bool ValidLectioId(string? id) => !string.IsNullOrWhiteSpace(id) && id.Trim().Length <= 64 && !id.Any(char.IsControl);
    private static bool ValidText(string? text, int limit) => text is null || text.Length <= limit && !text.Any(char.IsControl);
    private static string LectioKey(string id) => "lectio:" + id.Trim().ToLowerInvariant();
    private static string UserKey(Guid id) => "user:" + id.ToString("D");
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Text(JsonElement el, string name) => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? Clean(value.GetString()) : null;
    private static bool? Boolean(JsonElement el, string name) => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
    private static bool Object(JsonElement el, string name, out JsonElement value)
    {
        value = default;
        return el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object;
    }
    private static DateOnly? Date(JsonElement el, string name) => DateOnly.TryParse(Text(el, name), out var date) ? date : null;

    private sealed class Row
    {
        public string Key { get; set; } = "";
        public string? LectioId { get; set; }
        public Guid? UserSub { get; set; }
        public string DisplayName { get; set; } = "Ukendt elev";
        public string? ClassName { get; set; }
        public List<StudentDirectoryHold> Holds { get; } = [];
        public string Placement { get; set; } = "unknown";
        public string? CurrentSchoolPeriod { get; set; }
        public string? WorkplaceName { get; set; }
        public string? LastPassedSchoolPeriod { get; set; }
    }
}
