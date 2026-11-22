using Mercantec.Tech.Api.Notebooks;
using SkiaSharp;

namespace Mercantec.Tech.Api.Integrations.OpenAI;

internal static class NotebookCoverCaption
{
    private const string FontResourceName = "cover-font/space-grotesk.ttf";
    private static readonly Lazy<SKTypeface> Typeface = new(() =>
    {
        var stream = typeof(NotebookCoverCaption).Assembly.GetManifestResourceStream(FontResourceName)
            ?? throw new InvalidOperationException("The notebook cover font is missing.");
        return SKTypeface.FromStream(stream) ?? throw new InvalidOperationException("The notebook cover font is invalid.");
    });

    public static void Draw(SKCanvas canvas, int width, int height, NotebookCoverCourseLabel course)
    {
        var words = course.Title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return;

        // The cover is center-cropped into both wide headers and smaller book cards.
        var textWidth = width * .62f;
        var textHeight = Math.Min(height * .47f, width / 4.8f * .88f);
        using var titleFont = new SKFont(Typeface.Value, Math.Min(width * .061f, textHeight * .47f)) { Embolden = true };
        using var dateFont = new SKFont(Typeface.Value, Math.Min(width * .027f, textHeight * .23f));
        var dates = course.Dates.Where(date => !string.IsNullOrWhiteSpace(date)).ToArray();
        const string dateSeparator = "  ·  ";
        if (dates.Length > 0)
        {
            var together = dateFont.MeasureText(string.Join(dateSeparator, dates));
            dateFont.Size *= Math.Clamp(textWidth / together, .8f, 1f);
            FitLongestPart(dates, dateFont, textWidth);
        }
        var dateLines = Wrap(dates, dateSeparator, dateFont, textWidth);
        var dateHeight = dateLines.Count * LineHeight(dateFont);
        if (dateHeight > textHeight * .43f)
        {
            dateFont.Size *= textHeight * .43f / dateHeight;
            dateLines = Wrap(dates, dateSeparator, dateFont, textWidth);
        }
        FitLongestPart(words, titleFont, textWidth);
        var titleLines = Wrap(words, " ", titleFont, textWidth);
        var gap = dateLines.Count == 0 ? 0 : dateFont.Size * .3f;
        while (titleLines.Count > 3
            || (titleLines.Count * LineHeight(titleFont) + gap + dateLines.Count * LineHeight(dateFont) > textHeight))
        {
            titleFont.Size *= .9f;
            titleLines = Wrap(words, " ", titleFont, textWidth);
        }

        var captionHeight = titleLines.Count * LineHeight(titleFont) + gap + dateLines.Count * LineHeight(dateFont);
        var top = (height - captionHeight) / 2;
        var bottom = top + captionHeight;
        var fade = Math.Min(Math.Max(width * .045f, height * .07f), top);
        var dark = new SKColor(10, 23, 38, 216);
        using var background = SKShader.CreateLinearGradient(new(0, top - fade), new(0, bottom + fade),
            [dark.WithAlpha(0), dark, dark, dark.WithAlpha(0)],
            [0f, fade / (captionHeight + 2 * fade), 1f - fade / (captionHeight + 2 * fade), 1f], SKShaderTileMode.Clamp);
        using var paint = new SKPaint { Shader = background };
        canvas.DrawRect(SKRect.Create(0, top - fade, width, captionHeight + 2 * fade), paint);

        WriteLines(canvas, titleLines, titleFont, new SKColor(255, 248, 229), width / 2f, top);
        WriteLines(canvas, dateLines, dateFont, new SKColor(255, 219, 159), width / 2f,
            bottom - dateLines.Count * LineHeight(dateFont));
    }

    private static void FitLongestPart(string[] parts, SKFont font, float width)
    {
        var longest = parts.Max(part => font.MeasureText(part));
        if (longest > width) font.Size *= width / longest;
    }

    private static List<string> Wrap(string[] parts, string separator, SKFont font, float width)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var part in parts)
        {
            var candidate = line.Length == 0 ? part : line + separator + part;
            if (line.Length > 0 && font.MeasureText(candidate) > width)
            {
                lines.Add(line);
                line = part;
            }
            else
            {
                line = candidate;
            }
        }
        if (line.Length > 0) lines.Add(line);
        return lines;
    }

    private static float LineHeight(SKFont font) => font.Metrics.Descent - font.Metrics.Ascent;

    private static void WriteLines(SKCanvas canvas, List<string> lines, SKFont font, SKColor color, float x, float top)
    {
        using var paint = new SKPaint { IsAntialias = true, Color = color };
        var baseline = top - font.Metrics.Ascent;
        foreach (var line in lines)
        {
            canvas.DrawText(line, x, baseline, SKTextAlign.Center, font, paint);
            baseline += LineHeight(font);
        }
    }
}
