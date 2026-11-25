namespace Mercantec.Tech.Contracts;

public static class WerewolfMessages
{
    public static string RoleEnglish(string roleKey) => roleKey switch
    {
        "villager" => "Villager", "wolf" => "Werewolf", "thief" => "Thief", "seer" => "Seer",
        "cupid" => "Cupid", "hunter" => "Hunter", "witch" => "Witch", "guard" => "Protector",
        _ => roleKey
    };

    public static string RoleLabelEnglish(string danish)
    {
        foreach (var (key, label) in SocialRoles.Names)
            if (label == danish) return RoleEnglish(key);
        return danish;
    }

    // Saved private messages contain one name at most. Translate only their known envelope.
    public static string PrivateEnglish(string message)
    {
        const string thief = "Du overtog rollen ";
        const string partner = "Du er forelsket i ";
        const string pack = "Dine medulve: ";
        const string victim = "Ulvene har valgt ";
        const string vote = "Dit valg er registreret: ";
        if (!message.EndsWith('.')) return message;
        if (message.StartsWith(thief, StringComparison.Ordinal))
            return $"Your new role is {RoleLabelEnglish(message[thief.Length..^1])}.";
        if (message.StartsWith(partner, StringComparison.Ordinal))
            return $"You are in love with {message[partner.Length..^1]}.";
        if (message.StartsWith(pack, StringComparison.Ordinal))
            return $"Your fellow werewolves: {message[pack.Length..^1]}.";
        if (message.StartsWith(victim, StringComparison.Ordinal))
            return $"The werewolves chose {message[victim.Length..^1]}.";
        if (message.StartsWith(vote, StringComparison.Ordinal))
            return $"Your choice was recorded: {message[vote.Length..^1]}.";
        if (message.StartsWith("Nat ", StringComparison.Ordinal))
        {
            var colon = message.IndexOf(": ", StringComparison.Ordinal);
            var lastIs = message.LastIndexOf(" er ", StringComparison.Ordinal);
            if (colon > 4 && int.TryParse(message.AsSpan(4, colon - 4), out var night) && lastIs > colon + 2)
                return $"Night {night}: {message[(colon + 2)..lastIs]} has the role {RoleLabelEnglish(message[(lastIs + 4)..^1])}.";
        }
        return message switch
        {
            "Du er den eneste varulv." => "You are the only werewolf.",
            "Helbredelsesdrik: tilbage. Gift: tilbage." => "Healing potion: available. Poison: available.",
            "Helbredelsesdrik: tilbage. Gift: brugt." => "Healing potion: available. Poison: used.",
            "Helbredelsesdrik: brugt. Gift: tilbage." => "Healing potion: used. Poison: available.",
            "Helbredelsesdrik: brugt. Gift: brugt." => "Healing potion: used. Poison: used.",
            _ => message
        };
    }

    public static string? ResultEnglish(string? result) => result switch
    {
        "Ingen overlevede. Partiet har ingen vinder." => "No one survived. The game has no winner.",
        "Landsbyen vinder!" => "The village wins!",
        "Varulvene vinder!" => "The werewolves win!",
        _ => result
    };

    public static (string English, string Remainder) PublicOpening(string text)
    {
        const string first = "Den første nat er begyndt.";
        const string tie = "Stemmerne stod lige. Ingen blev stemt ud.";
        const string voted = "Landsbyen har stemt.";
        const string dawn = "Det er morgen efter nat ";
        const string survived = " Alle overlevede natten.";
        if (text.StartsWith(first, StringComparison.Ordinal)) return ("The first night has begun.", text[first.Length..]);
        if (text.StartsWith(tie, StringComparison.Ordinal)) return ("The vote was tied. No one was voted out.", text[tie.Length..]);
        if (text.StartsWith(voted, StringComparison.Ordinal)) return ("The village has voted.", text[voted.Length..]);
        if (text.StartsWith(dawn, StringComparison.Ordinal))
        {
            var dot = text.IndexOf('.', dawn.Length);
            if (dot > dawn.Length && int.TryParse(text.AsSpan(dawn.Length, dot - dawn.Length), out var night))
            {
                var rest = text[(dot + 1)..];
                var en = $"Dawn breaks after night {night}.";
                return rest == survived ? (en + " Everyone survived the night.", "") : (en, rest);
            }
        }
        return (text, "");
    }
}
