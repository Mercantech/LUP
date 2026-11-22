using Mercantec.Tech.Contracts;
using SkiaSharp;

namespace Mercantec.Tech.Api.Students;

public static class ProfilePhotoImages
{
    public static Task<byte[]?> NormalizeAsync(byte[] bytes, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (bytes.Length is 0 or > ProfilePhotoRules.MaxBytes) return Task.FromResult<byte[]?>(null);
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
            return Task.FromResult<byte[]?>(null);
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || info.Width > 10000 || info.Height > 10000
            || (long)info.Width * info.Height > 20_000_000) return Task.FromResult<byte[]?>(null);
        var pixels = new SKImageInfo(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var decoded = new SKBitmap(pixels);
        // Default codec options decode frame zero only. Reject partial/corrupt images.
        if (codec.GetPixels(pixels, decoded.GetPixels()) != SKCodecResult.Success)
            return Task.FromResult<byte[]?>(null);
        ct.ThrowIfCancellationRequested();
        var swapped = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = swapped ? info.Height : info.Width;
        var height = swapped ? info.Width : info.Height;
        var scale = Math.Min(1d, 512d / Math.Max(width, height));
        using var thumbnail = new SKBitmap(Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
        using (var canvas = new SKCanvas(thumbnail))
        {
            canvas.Clear(SKColors.White);
            canvas.Scale((float)scale);
            Orient(canvas, codec.EncodedOrigin, info.Width, info.Height);
            using var paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(decoded, 0, 0, paint);
        }
        // The fresh raster carries no EXIF/GPS, XMP or original embedded metadata.
        using var image = SKImage.FromBitmap(thumbnail);
        using var output = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        return Task.FromResult<byte[]?>(output?.ToArray());
    }

    internal static void Orient(SKCanvas canvas, SKEncodedOrigin origin, int width, int height)
    {
        switch (origin)
        {
            case SKEncodedOrigin.TopRight: canvas.Translate(width, 0); canvas.Scale(-1, 1); break;
            case SKEncodedOrigin.BottomRight: canvas.Translate(width, height); canvas.RotateDegrees(180); break;
            case SKEncodedOrigin.BottomLeft: canvas.Translate(0, height); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.RightTop: canvas.Translate(height, 0); canvas.RotateDegrees(90); break;
            case SKEncodedOrigin.RightBottom: canvas.Translate(height, width); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
            case SKEncodedOrigin.LeftBottom: canvas.Translate(0, width); canvas.RotateDegrees(270); break;
        }
    }
}
