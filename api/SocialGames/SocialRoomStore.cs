using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Mercantec.Tech.Api.Data;
using Mercantec.Tech.Api.Data.Entities;
using Mercantec.Tech.Api.Notebooks;
using Microsoft.EntityFrameworkCore;

namespace Mercantec.Tech.Api.SocialGames;

public sealed class SocialRoomStore(AppDbContext db)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    public async Task<SocialGameRoom> Create(Guid sub, SocialLobby lobby, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var room = new SocialGameRoom
            {
                Id = Guid.NewGuid(),
                Code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(CultureInfo.InvariantCulture),
                HostSub = sub, StateJson = JsonSerializer.Serialize(lobby, Json), ExpiresAtUtc = DateTime.UtcNow + Retention
            };
            db.SocialGameRooms.Add(room);
            try
            {
                await db.SaveChangesAsync(ct);
                db.Entry(room).State = EntityState.Detached;
                return room;
            }
            catch (DbUpdateException ex) when (NotebookPeriods.IsUniqueViolation(ex))
            {
                db.Entry(room).State = EntityState.Detached;
            }
        }
        throw new SocialRoomException(503, "Der var ingen ledig lobbykode. Prøv igen.");
    }

    public async Task<T> Read<T>(string code, Func<SocialGameRoom, SocialLobby, T> project, CancellationToken ct)
    {
        var row = await Query(code, lockRow: false).AsNoTracking().SingleOrDefaultAsync(ct) ?? throw Missing();
        if (row.NextDeadlineUtc <= DateTime.UtcNow)
            return await Change(code, (room, state, _) => new Mutation<T>(project(room, state), false), ct);
        return project(row, ReadState(row));
    }

    public sealed record Mutation<T>(T Result, bool Changed = true);

    public async Task<T> Change<T>(string code, Func<SocialGameRoom, SocialLobby, DateTimeOffset, Mutation<T>> command,
        CancellationToken ct, Guid? markInvitation = null)
    {
        CheckCode(code);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var row = await Query(code, lockRow: true).AsNoTracking().SingleOrDefaultAsync(ct) ?? throw Missing();
        var state = ReadState(row);
        var now = DateTimeOffset.UtcNow;
        // Save the deadline separately. A late command must not undo the automatic move.
        if (state.Game?.AdvanceExpired(now) == true)
            await Save(row, state, now, ct);
        Mutation<T> result;
        try { result = command(row, state, now); }
        catch (Exception ex) when (ex is SocialRoomException or WerewolfException)
        {
            await transaction.CommitAsync(ct);
            throw;
        }
        if (result.Changed) await Save(row, state, now, ct);
        if (markInvitation is Guid sub)
        {
            await db.SocialGameInvitations.Where(i => i.RoomId == row.Id && i.RecipientSub == sub && i.ReadAtUtc == null)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.ReadAtUtc, now.UtcDateTime), ct);
        }
        await transaction.CommitAsync(ct);
        return result.Result;
    }

    public async Task RunDeadlines(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var due = await db.SocialGameRooms.AsNoTracking().Where(r => r.NextDeadlineUtc <= now && r.ExpiresAtUtc > now)
            .OrderBy(r => r.NextDeadlineUtc).Select(r => r.Code).Take(100).ToListAsync(ct);
        foreach (var code in due)
        {
            try { await Change(code, (_, _, _) => new Mutation<bool>(true, false), ct); }
            catch (SocialRoomException ex) when (ex.Status == 404) { }
        }
    }

    private async Task Save(SocialGameRoom row, SocialLobby state, DateTimeOffset now, CancellationToken ct)
    {
        row.StateJson = JsonSerializer.Serialize(state, Json);
        row.NextDeadlineUtc = state.Game?.DeadlineUtc?.UtcDateTime;
        row.IsStarted = state.Game is not null;
        var previousRevision = row.Revision++;
        row.ExpiresAtUtc = now.UtcDateTime + Retention;
        var written = await db.SocialGameRooms.Where(r => r.Id == row.Id && r.Revision == previousRevision)
            .ExecuteUpdateAsync(set => set
                .SetProperty(r => r.StateJson, row.StateJson)
                .SetProperty(r => r.Revision, row.Revision)
                .SetProperty(r => r.IsStarted, row.IsStarted)
                .SetProperty(r => r.NextDeadlineUtc, row.NextDeadlineUtc)
                .SetProperty(r => r.ExpiresAtUtc, row.ExpiresAtUtc), ct);
        if (written != 1) throw new DbUpdateConcurrencyException("Lobbyen blev ændret under opdateringen.");
    }

    private IQueryable<SocialGameRoom> Query(string code, bool lockRow)
    {
        CheckCode(code);
        // Lock just this lobby across API replicas. SQLite acquires its writer lock when the transaction starts.
        var rooms = lockRow && db.Database.IsNpgsql()
            ? db.SocialGameRooms.FromSqlInterpolated($"SELECT * FROM social_game_rooms WHERE \"Code\" = {code} FOR UPDATE")
            : db.SocialGameRooms.Where(r => r.Code == code);
        var now = DateTime.UtcNow;
        return rooms.Where(r => r.ExpiresAtUtc > now);
    }

    private static void CheckCode(string code)
    {
        if (code.Length != 6 || !code.All(char.IsAsciiDigit)) throw Missing();
    }

    private static SocialRoomException Missing() => new(404, "Lobbyen findes ikke.");
    private static SocialLobby ReadState(SocialGameRoom room) => JsonSerializer.Deserialize<SocialLobby>(room.StateJson, Json)!;
}
