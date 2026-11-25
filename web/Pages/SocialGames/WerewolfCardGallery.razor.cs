using Mercantec.Tech.Contracts;

namespace Mercantec.Tech.Web.Pages.SocialGames;

public partial class WerewolfCardGallery
{
    private sealed record GalleryCard(string Key, string DanishArt, string EnglishArt,
        string? DanishName = null, string? EnglishName = null, GuideText? Preview = null)
    {
        public bool IsPlayable => SocialRoles.Names.ContainsKey(Key);
        public string Name(SocialGameLanguage lang) => IsPlayable ? lang.Role(Key) : lang.T(DanishName!, EnglishName!);
        public string Summary(SocialGameLanguage lang) => Preview?.In(lang) ?? lang.Rule(Key);
        public string Image => IsPlayable ? SocialText.RoleImage(Key) : $"/images/social-games/karakters/{Key}.webp";
    }

    private sealed record CardSeries(string Id, string DanishName, string EnglishName,
        string DanishDescription, string EnglishDescription, GalleryCard[] Cards)
    {
        public string Name(SocialGameLanguage lang) => lang.T(DanishName, EnglishName);
    }

    private static readonly CardSeries[] Series =
    [
        new("playable", "Spilbare roller", "Playable roles",
            "Disse otte roller kan indgå i en Werewolf-lobby.", "These eight roles are available in Werewolf lobbies.",
            [
                new("wolf", "En mørk ulv foran landsbyen i måneskin.", "A dark wolf in front of the moonlit village."),
                new("villager", "En smilende landsbyboer holder et rundt brød.", "A smiling villager cradles a round loaf of bread."),
                new("thief", "En hætteklædt tyv holder et nøglebundt.", "A hooded thief holds a ring of keys."),
                new("seer", "En ældre kvinde ser ind i en lysende krystalkugle.", "An older woman gazes into a glowing crystal ball."),
                new("cupid", "En vinget Amor med røde blomster i håret.", "A winged Cupid with red flowers in his hair."),
                new("hunter", "En jæger med fjer i hatten og bue i hånden.", "A hunter holds a bow and wears a feathered hat."),
                new("witch", "En heks i lilla hat holder en grøn og en lilla drik.", "A witch in a purple hat holds one green potion and one purple potion."),
                new("guard", "En beskytter bærer et rundt skjold med gyldne blade.", "A protector carries a round shield decorated with golden leaves.")
            ]),
        new("karakters", "Kommende roller", "Upcoming roles",
            "De 16 illustrationer fra Karakters er en forsmag på roller til en senere udgave.",
            "These 16 illustrations from Karakters preview roles for a future edition.",
            [
                new("trusted-villager", "En landsbyboer rækker begge hænder frem i døråbningen.", "A villager holds out both hands in a doorway.", "Den betroede landsbyboer", "The Trusted Villager",
                    new("Alle kan se dit kort fra begyndelsen.", "Everyone can see your card from the start.")),
                new("two-sisters", "To søstre læner sig mod hinanden over en kurv.", "Two sisters lean towards one another over a basket.", "De to søstre", "The Two Sisters",
                    new("Du genkender din søster den første nat.", "You recognise your sister on the first night.")),
                new("three-brothers", "Tre brødre står med armene om hinanden under et træ.", "Three brothers stand arm in arm beneath a tree.", "De tre brødre", "The Three Brothers",
                    new("I er tre brødre, der kender hinanden.", "You and your two brothers know one another.")),
                new("angel", "En engel med hvide vinger holder et lys ved brystet.", "A white-winged angel holds a light to their chest.", "Englen", "The Angel",
                    new("Du kan vinde allerede ved den første afstemning.", "You can win as early as the first vote.")),
                new("stuttering-judge", "En dommer løfter en vægt og holder en træhammer.", "A judge holds a set of scales and a wooden gavel.", "Den stammende dommer", "The Stuttering Judge",
                    new("Dit hemmelige tegn kan udløse endnu en afstemning.", "Your secret signal can trigger a second vote.")),
                new("rusty-sword-knight", "En gråhåret ridder holder et rustent sværd foran sin rustning.", "A grey-haired knight holds a rusty sword in front of his armour.", "Ridderen med det rustne sværd", "The Knight with the Rusty Sword",
                    new("Spiser ulvene dig, bliver én af dem ramt af dit rustne sværd.", "If the wolves eat you, your rusty sword claims one of them.")),
                new("actor", "En skuespiller løfter en glad og en sørgmodig maske.", "An actor holds up a smiling mask and a sorrowful mask.", "Skuespilleren", "The Actor",
                    new("Du kan låne en evne fra et af tre ekstra kort.", "You can borrow an ability from one of three spare cards.")),
                new("devoted-maid", "En tjenestepige med hvid kyse holder en rød kappe.", "A maid in a white bonnet holds a red cloak.", "Den hengivne tjenestepige", "The Devoted Maid",
                    new("Du kan overtage kortet fra den spiller, landsbyen stemmer ud.", "You can take the card of the player voted out by the village.")),
                new("fox", "En rød ræv ser tilbage over sin hale i sneen.", "A red fox looks back over its tail in the snow.", "Ræven", "The Fox",
                    new("Undersøg tre naboer og få én ledetråd om gruppen.", "Check three neighbours for a single clue about the group.")),
                new("bear-tamer", "Titus står ved siden af en stor bjørn med blomster om halsen.", "Titus stands beside a large bear with flowers around its neck.", "Titus og dansebjørnen", "Titus and the Dancing Bear",
                    new("Bjørnens brum kan afsløre, at en ulv sidder tæt på dig.", "A growl from your bear can give away a wolf sitting near you.")),
                new("sectarian", "En mand holder en gylden og en grøn halvdel frem mod to grupper.", "A man holds out a gold half and a green half to two groups.", "Sektlederen", "The Sect Leader",
                    new("Dit eget mål er at få den modsatte gruppe ud af spillet.", "Your personal goal is to see the entire opposite group eliminated.")),
                new("wild-child", "Et barn i en kappe af blade står i skovbrynet.", "A child in a cloak of leaves stands at the forest edge.", "Ulvebarnet", "The Wild Child",
                    new("Den første nat vælger du et forbillede. Dets skæbne afgør dit hold.", "Choose a role model on the first night. Their fate determines your team.")),
                new("wolfhound", "En ruhåret hund ser mod skoven fra et oplyst hus.", "A shaggy dog looks towards the woods from a lamplit house.", "Ulvehunden", "The Wolfhound",
                    new("Du vælger i hemmelighed mellem landsbyen og ulvene.", "Secretly choose between the village and the wolves.")),
                new("white-werewolf", "En hvid ulv sidder under et månesegl i en sneklædt skov.", "A white wolf sits beneath a crescent moon in a snowy forest.", "Den hvide varulv", "The White Werewolf",
                    new("Du jager med flokken, men vil ende som den eneste overlevende.", "You hunt with the pack and aim to be the only survivor.")),
                new("big-bad-wolf", "En mægtig mørk ulv tårner sig op over landsbyens tage.", "A huge dark wolf towers above the village rooftops.", "Den store stygge ulv", "The Big Bad Wolf",
                    new("Du kan jage endnu en gang efter flokkens fælles jagt.", "You can hunt a second time after the pack’s hunt.")),
                new("accursed-wolf", "En grå ulv holder en hvirvel af grønt lys mellem kløerne.", "A grey wolf holds a swirl of green light between its claws.", "Den forbandede urulv", "The Accursed Wolf",
                    new("Én gang kan du gøre flokkens offer til en ny varulv.", "Once per game you can turn the pack’s victim into a werewolf."))
            ]),
        new("bonus", "Bonuskort", "Bonus cards",
            "De to bonusroller til Volle Maan hører også til en senere udgave.",
            "These two bonus roles for Volle Maan are also planned for a future edition.",
            [
                new("spirit-talker", "En kvinde lytter til en skikkelse i røgen fra sit lille kar.", "A woman listens to a figure forming in the smoke from her bowl.", "Åndemaneren", "The Spirit Talker",
                    new("Brug et spiritismekort til at få et svar fra en udgået spiller.", "Use a spirit card to get an answer from an eliminated player.")),
                new("town-crier", "En byudråber i rød uniform står med sin tromme på torvet.", "A town crier in a red uniform stands in the square with his drum.", "Byudråberen", "The Town Crier",
                    new("Du kan læse et begivenhedskort højt og sætte det i spil.", "You can read an event card aloud and bring its effect into play."))
            ])
    ];

    private static readonly GalleryCard[] AllCards = Series.SelectMany(series => series.Cards).ToArray();
}
