namespace Mercantec.Tech.Contracts;

public sealed record ProfilePhotoView(bool HasCustomPhoto, DateTimeOffset? UpdatedAtUtc);

public static class ProfilePhotoRules
{
    public const int MaxBytes = 2 * 1024 * 1024;
}
