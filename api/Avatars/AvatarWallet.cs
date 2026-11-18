using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.Avatars;

internal static class AvatarWallet
{
    internal static async Task<int> Gold(AppDbContext db, Guid subject, CancellationToken ct) =>
        await db.AvatarProfiles.Where(profile => profile.UserSub == subject)
            .Select(profile => (int?)profile.Gold).SingleOrDefaultAsync(ct) ?? AvatarEconomy.InitialGold;

    // The caller's transaction covers the wallet, its purchase or reward, and the
    // standard starter ownerships if this is the first use of the account wallet.
    internal static async Task<bool> TryAdjustGold(AppDbContext db, Guid subject, int delta, CancellationToken ct)
    {
        if (delta == 0) return true;
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("A gold mutation requires a transaction.");

        var account = db.AvatarProfiles.Where(profile => profile.UserSub == subject);
        if (!await account.AnyAsync(ct)) await CreateWalletAsync(db, subject, ct);

        var minimum = delta < 0 ? -(long)delta : 0;
        var maximum = delta > 0 ? int.MaxValue - delta : int.MaxValue;
        return await account.Where(profile => profile.Gold >= minimum && profile.Gold <= maximum)
            .ExecuteUpdateAsync(update => update.SetProperty(profile => profile.Gold, profile => profile.Gold + delta)
                .SetProperty(profile => profile.Revision, profile => profile.Revision + 1)
                .SetProperty(profile => profile.LegacyImportAvailable, false), ct) == 1;
    }

    internal static AvatarProfile CreateProfile(Guid subject)
    {
        var profile = new AvatarProfile { UserSub = subject };
        profile.Avatars.Add(new AvatarOwnership { UserSub = subject, AvatarId = AvatarEconomy.StarterId });
        foreach (var id in AvatarEconomy.InitialItemIds)
            profile.Items.Add(new AvatarItemOwnership { UserSub = subject, ItemId = id });
        return profile;
    }

    private static async Task CreateWalletAsync(AppDbContext db, Guid subject, CancellationToken ct)
    {
        var profile = CreateProfile(subject);
        var created = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO avatar_profiles ("UserSub", "SelectedAvatarId", "Gold", "Level", "LegacyImportAvailable", "Revision")
            VALUES ({subject}, {profile.SelectedAvatarId}, {profile.Gold}, {profile.Level}, {profile.LegacyImportAvailable}, {profile.Revision})
            ON CONFLICT ("UserSub") DO NOTHING
            """, ct);
        if (created == 0) return;

        foreach (var avatar in profile.Avatars)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO avatar_ownerships ("UserSub", "AvatarId") VALUES ({subject}, {avatar.AvatarId})
                """, ct);
        foreach (var item in profile.Items)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO avatar_item_ownerships ("UserSub", "ItemId") VALUES ({subject}, {item.ItemId})
                """, ct);
    }
}
