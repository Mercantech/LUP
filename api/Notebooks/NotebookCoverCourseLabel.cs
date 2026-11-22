using System.Globalization;
using System.Text.RegularExpressions;

namespace Mercantec.Tech.Api.Notebooks;

public sealed partial record NotebookCoverCourseLabel(string Title, IReadOnlyList<string> Dates)
{
    private const int MaximumTitleLength = 200;
    private const int MaximumDates = 3;
    private const string Month = @"januar|februar|marts|april|maj|juni|juli|august|september|oktober|november|december|jan\.|feb\.|mar\.|apr\.|jun\.|jul\.|aug\.|sep\.|okt\.|nov\.|dec\.";
    private const string Day = @"[0-9]{1,2}";
    private const string Year = @"[0-9]{4}";
    private const string Weekday = @"(?:(?:mandag|tirsdag|onsdag|torsdag|fredag|lørdag|søndag)[\p{Zs}\t]+)?";
    private const string Date = @"(?:" + Year + "-" + Day + "-" + Day
        + "|" + Day + @"(?:\." + Day + @"\.|/" + Day + "/|-" + Day + "-)(?:" + Year + "|[0-9]{2})"
        + "|" + Day + @"\.?[\p{Zs}\t]+(?:" + Month + @")(?:[\p{Zs}\t]+" + Year + ")?"
        + "|" + Day + @"(?:/" + Day + @"|\." + Day + @"\.?))";
    private const string DateStart = @"(?<![\p{L}\p{N}_./-])";
    private const string DateEnd = @"(?![\p{L}\p{N}_/-]|\.[0-9])";
    private const string Dash = @"[-–—]";
    private const string WeekNumber = @"(?:0?[1-9]|[1-4][0-9]|5[0-3])";
    private static readonly string[] Months =
        ["januar", "februar", "marts", "april", "maj", "juni", "juli", "august", "september", "oktober", "november", "december"];

    public static NotebookCoverCourseLabel? Create(string title, string pageText)
    {
        var cleanTitle = PlainTitle(title);
        if (cleanTitle.Length == 0) return null;
        if (cleanTitle.Length > MaximumTitleLength)
        {
            var end = char.IsHighSurrogate(cleanTitle[MaximumTitleLength - 1]) ? MaximumTitleLength - 1 : MaximumTitleLength;
            cleanTitle = cleanTitle[..end].TrimEnd();
        }

        var periods = new List<string>();
        var schedule = new List<string>();
        var ranges = new List<string>();
        var dates = new List<string>();
        var weeks = new List<string>();
        var afterScheduleLabel = false;
        foreach (var row in BoundedPageText(pageText).Split('\n'))
        {
            var line = IgnoreInText().Replace(row.Trim(), " ");
            if (string.IsNullOrWhiteSpace(line) || CodeLine().IsMatch(line)) continue;
            var scheduleLabel = ScheduleLabel().Match(line);
            var isSchedule = afterScheduleLabel || scheduleLabel.Success;
            afterScheduleLabel = scheduleLabel.Success && string.IsNullOrWhiteSpace(line[scheduleLabel.Length..]);
            var dateRanges = Range().Matches(line).Select(FormatRange).OfType<string>().ToArray();
            if (dateRanges.Length > 0)
            {
                (isSchedule ? periods : ranges).AddRange(dateRanges);
                continue;
            }

            foreach (Match match in SingleDate().Matches(line))
            {
                var value = match.Groups["date"].Value;
                if (ReadDate(value) is { } date && (isSchedule || date.Year.HasValue || value.Any(char.IsLetter)))
                    (isSchedule ? schedule : dates).Add(Compact(value));
            }
            if (WeekRange().Match(line) is { Success: true } week)
                weeks.Add(Compact(week.Groups["week"].Value));
        }

        var shownDates = new[] { periods, schedule, ranges, dates, weeks }.FirstOrDefault(list => list.Count > 0) ?? [];
        return new(cleanTitle, shownDates.Distinct(StringComparer.OrdinalIgnoreCase).Take(MaximumDates).ToArray());
    }

    public static string? FindCourseTitle(string title, string pageText)
    {
        var cleanTitle = PlainTitle(title);
        if (ElectiveTitle().IsMatch(cleanTitle)) return cleanTitle;

        var heading = BoundedPageText(pageText).Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).Take(4)
            .Prepend(cleanTitle).Select(line => CourseHeading().Match(line)).FirstOrDefault(match => match.Success);
        return heading is null ? null : PlainTitle(heading.Groups["name"].Value);
    }

    private static string BoundedPageText(string value) => value[..Math.Min(value.Length, NotebookCoverEmojiSignals.MaxTextLength)];

    private static string PlainTitle(string title) => Compact(Shortcode().Replace(title, " ").Replace('\uFFFC', ' '));

    private static string Compact(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? FormatRange(Match match)
    {
        var first = match.Groups["first"].Value;
        var last = match.Groups["last"].Value;
        if (ReadDate(last) is not { } end) return null;
        if (match.Groups["day"].Success)
        {
            var dayText = match.Groups["day"].Value;
            var day = int.Parse(dayText.TrimEnd('.'), CultureInfo.InvariantCulture);
            return day >= 1 && day <= end.Day ? $"{dayText.TrimEnd('.')}.–{Compact(last)}" : null;
        }

        return ReadDate(first) is null ? null : $"{Compact(first)} – {Compact(last)}";
    }

    private static (int Day, int? Year)? ReadDate(string value)
    {
        var parts = value.Split([' ', '\t', '\u00a0', '\u202f', '/', '.', '-'], StringSplitOptions.RemoveEmptyEntries
            | StringSplitOptions.TrimEntries);
        if (parts.Length is < 2 or > 3) return null;
        var isoOrder = parts[0].Length == 4;
        if (!int.TryParse(isoOrder ? parts[^1] : parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var day))
            return null;
        var month = int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number : Array.FindIndex(Months, name => name.StartsWith(parts[1], StringComparison.OrdinalIgnoreCase)) + 1;
        int? year = null;
        if (parts.Length == 3)
        {
            var yearText = isoOrder ? parts[0] : parts[2];
            if (!int.TryParse(yearText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedYear)) return null;
            year = yearText.Length == 2 ? 2000 + parsedYear : parsedYear;
        }
        return year != 0 && month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year ?? 2000, month)
            ? (day, year) : null;
    }

    [GeneratedRegex(DateStart + @"(?:(?<first>" + Date + @")[\p{Zs}\t]*(?:" + Dash + @"|[\p{Zs}\t]+til(?:[\p{Zs}\t]+og[\p{Zs}\t]+med)?[\p{Zs}\t]+)[\p{Zs}\t]*"
        + Weekday + @"(?<last>" + Date + @")|(?<day>" + Day + @"\.?)[\p{Zs}\t]*" + Dash + @"[\p{Zs}\t]*(?<last>" + Date + @"))" + DateEnd,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Range();

    [GeneratedRegex(DateStart + @"(?<date>" + Date + ")" + DateEnd, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SingleDate();

    [GeneratedRegex(@"^(?:(?:start(?:dato)?|slut(?:dato)?|periode|forløbsperiode|kursusperiode|undervisningsperiode|datoer?|fra|til)\b[\p{Zs}\t]*[:–—-]?[\p{Zs}\t]*"
        + @"|(?:forløbet|kurset|undervisningen|valgfaget)\b.{0,85}?\b(?:fra|start(?:er|ede)|slut(?:ter|tede))[\p{Zs}\t]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ScheduleLabel();

    [GeneratedRegex(@"^(?<week>uger?\b[\p{Zs}\t]*:?[\p{Zs}\t]*" + WeekNumber + @"(?:(?:[\p{Zs}\t]*" + Dash + @"[\p{Zs}\t]*|[\p{Zs}\t]*,[\p{Zs}\t]*|[\p{Zs}\t]+(?:og|til)[\p{Zs}\t]+|[\p{Zs}\t]*&[\p{Zs}\t]*)"
        + WeekNumber + @")+(?:[\p{Zs}\t]+20[0-9]{2})?)(?![0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WeekRange();

    [GeneratedRegex(@"(?:https?://|ftp://|www\.)\S+|\x60[^\x60]*\x60", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IgnoreInText();

    [GeneratedRegex(@"^[\p{Zs}\t]*(?:[{}\[\];]|(?:let|var|const|return|function|import|def|select)[\p{Zs}\t]+\S)|(?:=>|=)[\p{Zs}\t]*['""]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CodeLine();

    [GeneratedRegex(@"^(?:deploy or die|game design|machine learning)(?:[\p{Zs}\t]+20[0-9]{2})?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ElectiveTitle();

    [GeneratedRegex(@"^[\p{Zs}\t]*(?:forløb(?:snavn|stitel)?|kursus(?:navn)?|valgfag)[\p{Zs}\t]*[:–—][\p{Zs}\t]*(?<name>\S[^\r\n]*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CourseHeading();

    [GeneratedRegex(@"(?<![\p{L}\p{N}_]):[a-zA-Z0-9_-]{2,32}:", RegexOptions.CultureInvariant)]
    private static partial Regex Shortcode();
}
