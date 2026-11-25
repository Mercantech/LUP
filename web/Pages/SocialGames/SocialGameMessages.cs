using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Pages.SocialGames;

public static class SocialGameMessages
{
    // Only apply this catalog to system text and the thief's spare-card labels.
    // Chat, participant names and ordinary action options belong to the players.
    public static string English(string text) => text switch
    {
        "Åbn afstemningen" => "Open voting",
        "Vælg jægerens sidste mål" => "Choose the hunter's final target",
        "Overtag et af de to kort" => "Take one of the two cards",
        "Behold tyvens kort" => "Keep the thief card",
        "Vælg de to forelskede" => "Choose the two lovers",
        "Beskyt en spiller i nat" => "Protect a player tonight",
        "Se en anden spillers rolle" => "Look at another player's role",
        "Vælg ulvenes offer" => "Choose the werewolves' victim",
        "Brug helbredelsesdrikken" => "Use the healing potion",
        "Gem helbredelsesdrikken" => "Keep the healing potion",
        "Giv gift til en spiller" => "Poison a player",
        "Gem giften" => "Keep the poison",
        "Stem på en spiller" => "Vote for a player",

        "Werewolf kræver 5–40 spillere." => "Werewolf requires 5–40 players.",
        "Vælg hvert specialkort højst én gang." => "Choose each special card only once.",
        "Der skal være mindst én varulv og én spiller på landsbyens side." => "You need at least one werewolf and one player on the village side.",
        "Der er flere valgte kort end spillere." => "There are more selected cards than players.",
        "Hver spiller skal have sin egen plads." => "Each player needs a separate seat.",
        "Vælg en handling." => "Choose an action.",
        "Spillet er gået videre. Hent den aktuelle fase." => "The game has moved on. Refresh to see the current phase.",
        "Du har ingen plads i spillet." => "You don't have a seat in this game.",
        "Denne handling er ikke tilgængelig nu." => "This action is not available right now.",
        "Vælg blandt de viste muligheder." => "Choose one of the options shown.",
        "Vælg gyldige specialkort, højst ét af hver slags." => "Choose valid special cards, at most one of each.",
        "Vælg mindst én varulv og giv plads til landsbyen." => "Choose at least one werewolf and leave room for the village.",
        "Deltag i lobbyen for at åbne den." => "Join the lobby to open it.",
        "Spillet er allerede startet." => "The game has already started.",
        "Der var ingen ledig lobbykode. Prøv igen." => "No lobby code was available. Try again.",
        "Lobbyen findes ikke." or "Lobbyen blev ikke fundet." => "Lobby not found.",
        "Gæstenøglen er ugyldig." => "The guest key is invalid.",
        "Denne gæstenøgle giver ikke adgang til lobbyen." => "This guest key does not grant access to the lobby.",
        "Lobbyen er fuld." => "The lobby is full.",
        "Skriv et kaldenavn på 1–30 tegn med mindst ét bogstav eller tal." => "Enter a nickname of 1–30 characters with at least one letter or number.",
        "Kaldenavnet er optaget i denne lobby." => "That nickname is already in use in this lobby.",
        "Spillet er endnu ikke startet." => "The game has not started yet.",
        "Spilhandlingen er ugyldig." => "That game action is invalid.",
        "Skriv en besked på højst 500 tegn." => "Enter a message of up to 500 characters.",
        "Beskedens id er allerede i brug." => "That message ID is already in use.",
        "Du kan ikke skrive i landsbyens chat i denne fase." => "You can't post in village chat during this phase.",
        "Vent et øjeblik, før du skriver igen." => "Wait a moment before sending another message.",
        "Kun værten kan gøre dette." => "Only the host can do this.",
        "Søg med et kortere navn." => "Search using a shorter name.",
        "Lobbyen er lukket, eller du er ikke vært." => "The lobby is closed or you're not the host.",
        "Vælg en registreret LUP-bruger." => "Choose a registered LUP user.",
        "Spilserveren sendte et tomt svar." => "The game server returned an empty response.",
        "Log ind eller deltag som gæst." => "Sign in or join as a guest.",
        "Du har ikke adgang til denne handling." => "You don't have permission to do this.",
        "Vent et øjeblik, og prøv igen." => "Wait a moment and try again.",
        "Handlingen kunne ikke gennemføres. Prøv igen." => "The action could not be completed. Try again.",
        "Forbindelsen til spillet blev afbrudt. Prøv igen." => "The connection to the game was lost. Try again.",
        _ => OtherSystemText(text)
    };

    private static string OtherSystemText(string text)
    {
        var role = WerewolfMessages.RoleLabelEnglish(text);
        if (role != text) return role;
        var secret = WerewolfMessages.PrivateEnglish(text);
        if (secret != text) return secret;
        var result = WerewolfMessages.ResultEnglish(text);
        if (result != text) return result!;
        var (opening, rest) = WerewolfMessages.PublicOpening(text);
        return opening + rest;
    }
}
