namespace Mercantec.Tech.Web.Pages.SocialGames;

internal static partial class WerewolfCardGuide
{
    private static void AddPlayable(Dictionary<string, RoleGuide> cards)
    {
        cards["wolf"] = new(
            new("Du spiller for flokken. I vinder, når alle, der stadig er i spillet, er varulve. Er der to ulve og to landsbyfolk tilbage, fortsætter spillet.",
                "You play for the pack. You win when everyone left in the game is a werewolf. With two wolves and two players from the village still alive, the game continues."),
            new("Når flokken vågner første gang, kan du se navnene på dine medulve på dit private kort. Hver nat vælger du en levende spiller uden for flokken. Om dagen diskuterer og stemmer du sammen med landsbyen.",
                "When the pack first wakes, your private card shows you the names of your fellow wolves. Each night you choose a living player outside the pack. By day you join the village discussion and vote."),
            new("Du kan se de andre ulves indsendte valg, og dit eget valg ligger fast, så snart du bekræfter det. Udløber en tidsfrist, vælger LUP tilfældigt for hver ulv, der mangler at svare. Alle levende ulve skal vælge samme spiller for at angribe. Beskytteren eller heksens helbredelsesdrik kan stadig redde offeret.",
                "You can see the other wolves’ submitted choices, and your own choice is final once you confirm it. If a timer runs out, LUP picks a target at random for each wolf who has yet to answer. Every living wolf must choose the same player for the attack to go ahead. The protector or the witch’s healing potion may still save the victim."),
            new("Du og Maja er de eneste ulve. Dit kort viser, at Maja har valgt Noa. Vælger du også Noa, angriber I ham. Vælger du Ida, slipper både Noa og Ida for ulveangrebet den nat.",
                "You and Maja are the only wolves. Your card shows that Maja has chosen Noa. Pick Noa too and you attack him. Pick Ida instead and both Noa and Ida escape the wolves that night."));

        cards["villager"] = new(
            new("Du skal hjælpe landsbyen med at finde ulvene. Læg mærke til, hvem der anklager hvem, og hvilke forklaringer der skifter fra dag til dag. Landsbyen vinder, når den sidste ulv er ude, og mindst én fra landsbyen har overlevet.",
                "Help the village find the wolves. Listen for who accuses whom and whose story changes from one day to the next. The village wins once the last wolf is out and someone from the village has survived."),
            new("Om natten venter du, mens rollerne med nattevalg handler. Om morgenen fortæller LUP, hvem der eventuelt er røget ud, og viser deres roller. Mens du er i live, kan du stille spørgsmål i landsbyens diskussion.",
                "At night you wait while players with night actions take their turns. In the morning LUP names anyone who was eliminated and reveals their role. While you are alive, you can ask questions in the village discussion."),
            new("Når værten åbner afstemningen, stemmer du én gang på en anden levende spiller. Hvis Amor har gjort dig forelsket, kan du kun stemme på andre end din partner. Spilleren med flest stemmer ryger ud. Deler flere førstepladsen, går alle videre til natten.",
                "When the host opens voting, cast one vote for another living player. If Cupid made you a lover, your partner is also off limits. The player with the most votes is out. If two or more players tie for the most votes, everyone goes through to the night."),
            new("Liv beskylder Bo for at være ulv. Du spørger, hvad hun har lagt mærke til, før du vælger, hvem du tror på. Fire spillere stemmer, og Liv og Bo får to stemmer hver. Begge er stadig med næste nat.",
                "Liv accuses Bo of being a wolf. Before deciding whom you believe, you ask what she noticed. Four players vote, and Liv and Bo get two votes each. Both stay in the game for the next night."));

        cards["thief"] = new(
            new("Du får lov at vælge, hvilken rolle du vil spille resten af partiet. Beholder du tyvekortet, hjælper du landsbyen med din stemme. Tager du et nyt kort, overtager du både rollens evner og dens mål.",
                "You get to choose which role to play for the rest of the game. Keep the thief card and you help the village with your vote. Take a new card and you inherit both its abilities and its goal."),
            new("Du er den første, der vågner den første nat. Åbn dit private kort for at se de to rollekort, der blev tilovers ved uddelingen. Du får kun denne ene mulighed for at bytte.",
                "You are the first player to wake on the first night. Open your private card to see the two role cards left over from the deal. This is your one chance to swap."),
            new("Vælg et af de to kort, eller behold tyvekortet. Viser begge kort en varulv, skal du tage et af dem. Hvis tiden løber ud i et spil med tidsfrist, beholder du tyvekortet, når det er muligt. Er begge kort ulve, bliver du varulv.",
                "Pick either spare card or keep your thief card. If both spares show werewolves, you must take one. In a timed game, letting the clock run out keeps your thief card whenever that choice is allowed. With two wolf cards, you become a werewolf."),
            new("På dit kort ser du Amor og landsbyboer. Du vælger Amor. Da det øjeblikket efter bliver Amors tur, vælger du Liv og Freja som de forelskede. Begge får nu navnet på deres partner på deres eget kort.",
                "Your choices are Cupid and Villager. You take Cupid. Moments later it is Cupid’s turn, so you choose Liv and Freja as the lovers. Each of them now finds their partner’s name on their own card."));

        cards["seer"] = new(
            new("Du kan skaffe landsbyen sikker viden om en anden spillers rolle. Brug det, du ved, når landsbyen overvejer, hvem den skal stemme ud. Du bestemmer selv, hvor meget du vil fortælle.",
                "You can learn another player’s role with certainty. Use what you know when the village is deciding whom to vote out. You choose how much to tell the others."),
            new("Hver nat, mens du er i live, vælger du én anden levende spiller. Det sker efter beskytterens valg og før ulvenes angreb. Du får svaret med det samme på dit private kort.",
                "Each night you are alive, choose one other living player. Your turn comes after the protector’s choice and before the wolves attack. The answer appears immediately on your private card."),
            new("Du får det præcise rollenavn, og beskeden bliver stående med nattens nummer. At være forelsket ændrer ikke navnet på spillerens rollekort. Du må gerne undersøge den samme spiller igen en senere nat.",
                "You learn the exact role, and the message stays on your card with the night it came from. Being one of the lovers leaves a player’s role name unchanged. You may look at the same player again on a later night."),
            new("Den første nat vælger du Tilde og får at vide, at hun er heks. Næste dag påstår Emil, at han er heks. Du ved, at der kun er ét heksekort, så du beder Emil forklare sin historie, før landsbyen stemmer.",
                "On the first night you choose Tilde and learn that she is the witch. The next day Emil claims to be the witch. There is only one witch card, so you ask Emil to explain his story before the village votes."));

        cards["cupid"] = new(
            new("Du hører til landsbyen og vælger to spillere, der fra nu af deler skæbne. Binder du en varulv til en fra landsbyen, kan de to vinde sammen, hvis de overlever alle andre.",
                "You belong to the village and choose two players whose fates are now linked. If you pair a werewolf with someone from the village, the two of them can win together by outlasting everyone else."),
            new("Du vælger parret én gang den første nat, efter tyvens tur. Vælg to forskellige spillere, gerne dig selv og én anden. Lader du tidsfristen løbe ud, trækker LUP to tilfældige navne. Derefter deltager du i dagens diskussion og afstemning som de andre.",
                "Choose the pair once, on the first night after the thief’s turn. Pick two different players, which may include yourself. If you let the timer run out, LUP draws two names at random. After that, join the village discussion and vote each day."),
            new("Hver af de forelskede får en privat besked med partnerens navn. De kan ikke stemme på hinanden. Ryger den ene ud, ryger den anden straks med. Har parret én ulv og én fra landsbyen, vinder de to sammen, når de er de sidste i live.",
                "Each lover receives a private message naming their partner. They cannot vote for one another. If either is eliminated, the other is out immediately. A pair made up of one wolf and one player from the village wins together when they are the last two alive."),
            new("Du forbinder Ane og Omar uden at kende deres kort. Ane er varulv, og Omar er seer. Til sidst er der kun de to og en landsbyboer tilbage. Da landsbyboeren bliver stemt ud, vinder Ane og Omar sammen.",
                "You pair Ane with Omar without knowing their cards. Ane is a werewolf and Omar is the seer. Eventually only the two of them and a villager remain. When the villager is voted out, Ane and Omar win together."));

        cards["hunter"] = new(
            new("Du leder efter ulvene sammen med landsbyen. Dit særlige valg kommer i det øjeblik, du selv ryger ud. Lyt til de andre, mens du er med, så du har en mistænkt i tankerne.",
                "You hunt for wolves alongside the village. Your special choice comes at the moment you are eliminated. Listen while you are still in the game so you have a suspect in mind."),
            new("Du får et sidste skud, hvis ulvene tager dig, landsbyen stemmer dig ud, eller din forelskede partner ryger ud og trækker dig med. Er der mindst én spiller i live, venter LUP på dit valg, før spillet fortsætter.",
                "You get a final shot if the wolves take you, the village votes you out, or your lover is eliminated and takes you with them. As long as someone is still alive, LUP waits for your choice before play continues."),
            new("Vælg én spiller blandt de overlevende. Den spiller ryger straks ud, og en eventuel forelsket partner følger med. Hvis heksens gift er den direkte årsag til, at du ryger ud, får du intet sidste skud. Dit skud har fri tid, også i et spil med tidsfrist om natten.",
                "Choose one of the survivors. That player is eliminated immediately, along with their lover if they have one. If the witch’s poison alone takes you out directly, you get no final shot. Your shot has no time limit, even in a game with timed night actions."),
            new("Kun du, din forelskede partner Freja og varulven Malik er tilbage. Malik angriber Freja. I ryger begge ud, og du får dit sidste skud. Du vælger Malik, så ingen overlever, og partiet ender uden en vinder.",
                "Only you, your lover Freja and Malik the werewolf remain. Malik attacks Freja. Both of you are out, and you get your final shot. You choose Malik, so nobody survives and the game ends without a winner."));

        cards["witch"] = new(
            new("Du spiller for landsbyen med to drikke, som hver kan bruges én gang i hele partiet. Helbredelsesdrikken kan redde et offer fra ulvene. Giften kan fjerne den spiller, du mistænker for at være ulv.",
                "You play for the village with two potions, each good for one use in the entire game. The healing potion can save a victim from the wolves. The poison can eliminate the player you suspect is a werewolf."),
            new("Din tur kommer efter ulvenes valg. Har du stadig helbredelsesdrikken, og er en spiller ved at blive taget af ulvene, får du navnet og vælger, om du vil redde spilleren. Derefter får du mulighed for at bruge din gift, hvis du har den tilbage.",
                "Your turn comes after the wolves have chosen. If you still have the healing potion and the wolves are about to take someone, you learn their name and decide whether to save them. You then get the option to use your poison if you still have it."),
            new("Du kan redde dig selv, hvis ulvene har valgt dig. Giften kan ramme enhver levende spiller, også én, som beskytteren holder vagt over. Du kan bruge begge drikke samme nat. Udløber tidsfristen for en drik, bliver den gemt til senere.",
                "You can save yourself if the wolves have chosen you. Poison can reach any living player, including someone guarded by the protector. You can use both potions on the same night. If the timer for a potion runs out, you keep it for later."),
            new("Du får at vide, at ulvene har valgt Signe, og giver hende helbredelsesdrikken. Da LUP spørger om giften, vælger du at gemme den. Signe overlever angrebet, og næste nat har du kun giften tilbage.",
                "You learn that the wolves have chosen Signe and give her the healing potion. When LUP asks about the poison, you decide to keep it. Signe survives the attack, and the following night you have only the poison left."));

        cards["guard"] = new(
            new("Du hjælper landsbyen ved at gætte, hvor ulvene vil slå til. Holder du vagt over den spiller, de angriber, afværger du angrebet. Så kan spilleren være med til at finde ulvene næste dag.",
                "Help the village by anticipating where the wolves will strike. Guard the player they attack and you stop the attack, giving someone who might have been lost another day to speak and vote."),
            new("Hver nat vælger du én levende spiller, før seeren og ulvene får deres ture. Du kan også vælge dig selv. Når du bekræfter dit valg, er vagten sat for den nat.",
                "Each night, choose one living player before the seer and the wolves take their turns. You can choose yourself. Once you confirm, that is where you stand guard for the night."),
            new("Du skal skifte mål fra den ene nat til den næste. Efter en nat hos en anden spiller må du gerne vende tilbage. Din vagt standser ulvenes angreb. Den, du beskytter, kan stadig ryge ud af heksens gift eller ved at miste sin forelskede partner.",
                "You must change whom you protect from one night to the next. After a night guarding someone else, you may return. Your watch stops a wolf attack. The person you guard can still be eliminated by the witch’s poison or by losing their lover."),
            new("Første nat beskytter du Iben. Ulvene vælger også Iben, så deres angreb falder bort. Anden nat er hun væk fra din liste, og du vælger at beskytte dig selv. Tredje nat kan du igen vælge Iben, hvis I begge stadig er med.",
                "On the first night you guard Iben. The wolves choose her too, so their attack fails. On the second night she is absent from your list and you guard yourself. On the third night you may choose Iben again if you are both still in the game."));
    }
}
