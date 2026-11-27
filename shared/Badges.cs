namespace Mercantec.Tech.Contracts;

public sealed record BadgeDefinition(string Id, string Name, string Topic, string Description, string Icon, int Levels, bool Automatic = false)
{
    public bool Special => Levels == 1;
}

public static class BadgeCatalog
{
    public static readonly BadgeDefinition[] All =
    [
        new("first-submission", "Første aflevering", "Quests", "Aflevér dit arbejde på en quest for første gang.", "book", 1, true),
        new("wordle-win", "Ordknækker", "Ordspil", "Gæt dagens ord i en officiel Wordle-runde.", "star", 1, true),
        new("loop", "Core Loop", "Game Design", "Forklar, hvad spilleren gør igen og igen.", "loop", 5),
        new("playtest", "Playtester", "Game Design", "Adskil observation fra fortolkning.", "test", 5),
        new("gdd", "Game Designer", "Game Design", "Forbind pitch, scope og et afprøveligt spildesign.", "game", 5),
        new("mqtt", "MQTT Explorer", "Arduino & IoT", "Følg en besked fra enhed til broker.", "wifi", 5),
        new("cpp", "C++ Bygger", "Arduino & IoT", "Strukturér Arduino-kode med funktioner og flere filer.", "chip", 5),
        new("friday", "Fancy Friday", "Fællesskab", "Deltag i holdets Fancy Friday efter aftale med din underviser.", "shirt", 1),
        new("api", "API Architect", "Serverside", "Design tydelige endpoints og håndtér fejl.", "code", 5),
        new("sql", "Data Designer", "Database", "Modellér relationer og gem dine målinger.", "database", 5),
        new("git", "Git Together", "Samarbejde", "Brug branches, commits og review i dit team.", "loop", 5),
        new("security", "Security Mindset", "Test & sikkerhed", "Beskyt adgang, og undersøg fejltilstande.", "shield", 5),
        new("ui", "Interface Builder", "Frontend", "Gør data forståelige og mulige at handle på.", "chart", 5),
        new("docs", "Fortæl med kode", "Dokumentation", "Forklar løsningen, så andre kan arbejde videre.", "book", 5),
        new("hawaii", "Hawaii", "Fællesskab", "Deltag i holdets Hawaii-dag med en Hawaii-skjorte.", "flower", 1),
    ];
    public static BadgeDefinition? Find(string? id) => Array.Find(All, b => b.Id == id);
}

public sealed record BadgeHold(Guid Id, string Name);
public sealed record BadgeEntry(Guid Id, string BadgeId, int Rank, string Status, string Evidence, DateTime UpdatedUtc);
public sealed record BadgeCollection(BadgeEntry[] Entries, BadgeHold[] Holds);
public sealed record BadgeApplication(Guid HoldId, int Rank, string Evidence);
public sealed record BadgeReview(Guid Id, string BadgeId, int Rank, string Evidence, string Student, string Hold);
public sealed record BadgeDecision(bool Approve);
