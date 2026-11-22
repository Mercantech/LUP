using Mercantec.Tech.Api.Integrations.OpenAI;
using Mercantec.Tech.Api.Notebooks;
using SkiaSharp;

namespace Mercantec.Tech.Api.Tests;

public sealed class NotebookCoverCaptionTests
{
    [Fact]
    public void Course_title_and_dates_are_rasterized_inside_the_wide_center_crop()
    {
        var source = FlatCover();
        const string title = "Grundforløb i Trådløse Netværk og Sikkerhed";
        string[] dates = ["4. – 12. februar 2026", "7. september 2026", "1. november 2026"];
        using var artOnly = SKBitmap.Decode(NotebookCoverImage.Normalize(source)!);
        using var titleOnly = SKBitmap.Decode(NotebookCoverImage.Normalize(source, new(title, []))!);
        using var withDates = SKBitmap.Decode(NotebookCoverImage.Normalize(source, new(title, dates))!);

        Assert.Equal(1536, withDates.Width);
        Assert.Equal(864, withDates.Height);
        Assert.Equal((0, 0), CountCaptionColors(artOnly));
        var titleCaption = CountCaptionColors(titleOnly);
        Assert.True(titleCaption.Title > 100);
        Assert.Equal(0, titleCaption.Dates);
        var completeCaption = CountCaptionColors(withDates);
        Assert.True(completeCaption.Title > 100);
        Assert.True(completeCaption.Dates > 100);
    }

    private static (int Title, int Dates) CountCaptionColors(SKBitmap image)
    {
        var title = 0;
        var dates = 0;
        var halfCrop = image.Width / 4.8 / 2;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = image.GetPixel(x, y);
                if (pixel.Red < 225 || pixel.Green < 170) continue;
                var isTitle = pixel.Green >= 225 && pixel.Blue >= 205;
                var isDate = pixel.Green < 225 && pixel.Blue <= 185;
                if (isTitle) title++;
                if (isDate) dates++;
                if (isTitle || isDate)
                    Assert.InRange(y, image.Height / 2 - halfCrop, image.Height / 2 + halfCrop);
            }
        }
        return (title, dates);
    }

    private static byte[] FlatCover()
    {
        using var bitmap = new SKBitmap(1536, 864);
        bitmap.Erase(new SKColor(26, 102, 143));
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
