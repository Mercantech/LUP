using Mercantec.Tech.Api.Students;
using Mercantec.Tech.Contracts;
using SkiaSharp;

namespace Mercantec.Tech.Api.Integrations.OpenAI;

public static class NotebookCoverImage
{
    public static byte[]? Normalize(byte[] input)
    {
        if (input.Length is 0 or > NotebookCoverRules.MaxUploadBytes) return null;
        using var data = SKData.CreateCopy(input);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
            return null;

        var source = codec.Info;
        if (source.Width is <= 0 or > 10000 || source.Height is <= 0 or > 10000
            || (long)source.Width * source.Height > 20_000_000)
            return null;

        var pixels = new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var decoded = new SKBitmap(pixels);
        if (codec.GetPixels(pixels, decoded.GetPixels()) != SKCodecResult.Success) return null;

        var swapped = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = swapped ? source.Height : source.Width;
        var height = swapped ? source.Width : source.Height;
        var scale = Math.Min(1d, Math.Min(1536d / width, 1024d / height));
        using var cover = new SKBitmap(Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
        using (var canvas = new SKCanvas(cover))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale((float)scale);
            ProfilePhotoImages.Orient(canvas, codec.EncodedOrigin, source.Width, source.Height);
            using var image = SKImage.FromBitmap(decoded);
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
        }

        // A fresh raster removes EXIF and other source metadata. The same bounded output serves cards and headers.
        using var raster = SKImage.FromBitmap(cover);
        foreach (var quality in new[] { 85, 65, 45 })
        {
            using var encoded = raster.Encode(SKEncodedImageFormat.Webp, quality);
            if (encoded is not null && encoded.Size <= 768 * 1024)
                return encoded.ToArray();
        }

        return null;
    }
}
