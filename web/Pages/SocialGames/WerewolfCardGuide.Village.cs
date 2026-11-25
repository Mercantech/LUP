namespace Mercantec.Tech.Web.Pages.SocialGames;

internal static partial class WerewolfCardGuide
{
    private static void AddVillage(Dictionary<string, RoleGuide> cards)
    {
        cards["trusted-villager"] = new(
            new("Hjælp landsbyen med at finde alle varulvene. Alle kender din rolle fra begyndelsen.",
                "Help the village find every werewolf. Everyone knows your role from the start."),
            new("Dit kort viser en landsbyboer på begge sider. Allerede under uddelingen kan alle se, hvilket kort du får.",
                "Your card shows a villager on both sides. Everyone can see which card you receive as it is dealt."),
            new("Om dagen stiller du spørgsmål og stemmer som de andre landsbyboere. Du kan stadig tage fejl i din mistanke, og hvis urulven senere smitter dig, skifter du side.",
                "Question the other players and cast your vote during the day. Your suspicions can still be wrong, and a bite from the Accursed Wolf can change your allegiance later."),
            new("Liv spørger, hvorfor du forsvarede Omar. Hun kender allerede dit kort, så du kan gå lige til sagen og forklare, hvem Omar stemte på i går.",
                "Liv asks why you defended Omar. She already knows your card, so you can go straight to explaining who Omar voted for yesterday."), 4);

        cards["two-sisters"] = new(
            new("Find varulvene sammen med landsbyen. Du begynder med én allieret, som du kan genkende.",
                "Find the werewolves with the village. You start with one ally you can recognise."),
            new("I får hvert jeres søsterkort. Den første nat beder spillederen jer åbne øjnene samtidig, mens resten af landsbyen sover.",
                "Each sister receives a separate card. On the first night the moderator asks both of you to open your eyes while the village sleeps."),
            new("Læg mærke til, hvem din søster er. I har hver jeres stemme om dagen. I et erfarent selskab kan spillederen kalde jer igen på udvalgte nætter, så I kan udveksle mistanker med tegn.",
                "Remember who your sister is. Each of you has your own vote during the day. In an experienced group, the moderator may wake you on later nights so you can share suspicions by gesturing."),
            new("Da du åbner øjnene, ser du Aya kigge tilbage. Næste dag beskylder Emil hende for at være ulv. Du kan spørge, hvad han bygger det på, mens du holder jeres slægtskab hemmeligt.",
                "You open your eyes and see Aya looking back. The next day Emil accuses her of being a wolf. You can ask what evidence he has while keeping your connection secret."), 5);

        cards["three-brothers"] = new(
            new("Hjælp landsbyen med at finde ulvene. Dine to brødre giver dig to kendte allierede fra start.",
                "Help the village find the wolves. Your two brothers give you two known allies from the start."),
            new("Brug alle tre brødrekort i en stor landsby. Første nat vækker spillederen jer sammen, så hver bror kan se de to andre.",
                "Use all three brother cards in a large village. On the first night the moderator wakes you together so each brother can see the other two."),
            new("Hver bror stemmer selv. I kan lytte til forskellige spillere og sammenholde jeres mistanker. Med erfarne spillere kan spillederen give jer flere stille møder på udvalgte nætter.",
                "Each brother casts his own vote. You can listen to different players and compare suspicions. With experienced players, the moderator may allow further silent meetings on selected nights."),
            new("Du genkender Leo og Malik den første nat. Ved jeres næste nattemøde peger Leo på Nora, mens Malik ryster på hovedet. Dagen efter kan du spørge Nora, hvorfor hun skiftede stemme.",
                "You recognise Leo and Malik on the first night. At your next night meeting, Leo points at Nora and Malik shakes his head. The next day you can ask Nora why she changed her vote."), 6);

        cards["angel"] = new(
            new("Du vinder alene, hvis du bliver stemt ud ved den første afstemning eller bliver slået ud i løbet af den første nat.",
                "You win on your own if the first vote eliminates you or if you are eliminated during the first night."),
            new("Når englen er med, begynder spillet om dagen. Landsbyen diskuterer og stemmer, inden nogen har haft en nattehandling.",
                "When the Angel is in play, the game starts during the day. The village debates and votes before anyone has taken a night action."),
            new("Lykkes det, slutter spillet straks. Er du stadig med ved starten af dag to, bliver du almindelig landsbyboer og hjælper med at finde ulvene. Bliver du en af Amors forelskede, opgiver du din personlige sejr.",
                "Meet your goal and the game ends immediately. If you are still playing when day two begins, you become an ordinary villager and help find the wolves. If Cupid makes you one of the lovers, you give up your personal victory."),
            new("Du påstår på første dag, at du undersøgte Maja i nat. Maja minder alle om, at det endnu ikke har været nat. Stemmer landsbyen dig ud for den historie, har du vundet.",
                "On day one you claim you investigated Maja last night. Maja points out that there hasn't been a night yet. If the village votes you out for that story, you win."), 7);

        cards["stuttering-judge"] = new(
            new("Hjælp landsbyen med at finde varulvene. Én gang kan du give byen mulighed for at stemme endnu en spiller ud samme dag.",
                "Help the village find the werewolves. Once per game you can give everyone the chance to vote out a second player on the same day."),
            new("Vis spillederen et hemmeligt tegn den første nat. Giv tegnet under den afstemning, hvor du vil bruge evnen.",
                "Show the moderator a secret signal on the first night. Give that signal during the vote when you want to use your power."),
            new("Den første afstemning afgøres normalt. Når den valgte spiller er ude, sætter spillederen straks en ny afstemning i gang. Alle tilbageværende stemmer med det samme uden en ny diskussion.",
                "Resolve the first vote as usual. Once that player is out, the moderator immediately calls a fresh vote. The remaining players vote again without another discussion."),
            new("Du og spillederen har aftalt, at du rører ved øret. Du gør det, mens folk stemmer på Anton. Da Anton er ude, kan landsbyen også stemme om Liv, som flere har mistænkt.",
                "You and the moderator agreed that you would touch your ear. You do so while people are voting for Anton. Once Anton is out, the village can also vote for Liv, whom several players suspect."), 8);

        cards["rusty-sword-knight"] = new(
            new("Hjælp landsbyen med at finde ulvene. Selv hvis flokken æder dig, kan dit sværd koste den en varulv.",
                "Help the village find the wolves. Even if the pack eats you, your sword can still cost it a werewolf."),
            new("Evnen udløses, når varulvene æder dig. Du ryger ud som sædvanligt. Sværdet virker automatisk, og du skal ikke udpege et mål.",
                "Your power activates when the werewolves eat you. You are eliminated as usual. The sword takes effect automatically, so you do not choose a target."),
            new("Følg pladserne til venstre fra din stol. Den første varulv i den retning bliver såret, spiller videre hele næste dag og dør ved slutningen af næste nat. Spillederen fortæller landsbyen, at såret var årsagen.",
                "Follow the seats to the left from your chair. The first werewolf in that direction is wounded, plays through the next day and dies at the end of the following night. The moderator announces that the wound caused the death."),
            new("Til venstre for dig sidder Liv, så varulven Omar. Ulvene æder dig den første nat. Omar kan stadig stemme dagen efter. Da han dør af såret ved slutningen af nat to, ved landsbyen, at Liv ikke var varulv den første nat.",
                "Liv sits to your left, followed by Omar the werewolf. The wolves eat you on the first night. Omar can still vote the next day. When the wound kills him at the end of night two, the village knows Liv wasn't a werewolf on night one."), 9);

        cards["actor"] = new(
            new("Brug de evner, du får undervejs, til at hjælpe landsbyen med at finde ulvene.",
                "Use the powers you pick up along the way to help the village find the wolves."),
            new("Før spillet vælger spillederen tre specialkort uden varulve og lægger dem med bagsiden opad midt på bordet. Hver nat får du mulighed for at vælge et af de resterende kort.",
                "Before play the moderator sets aside three special cards, excluding werewolves, and places them face down in the middle. Each night you may choose one of the remaining cards."),
            new("Du får kortets evne frem til næste nat. Et brugt kort fjernes. Du må gerne vente med at vælge, så du har kort tilbage senere i spillet.",
                "You gain that card's power until the next night. Used cards are removed. You may pass, leaving a card available for later in the game."),
            new("Første nat tager du kortet i midten. Det er seeren, så du undersøger Ella. Næste nat husker du stadig, hvad du så, og der ligger nu to andre kort at vælge imellem.",
                "On the first night you take the middle card. It is the Seer, so you investigate Ella. The next night you still remember what you saw, and two other cards remain to choose from."), 10);

        cards["devoted-maid"] = new(
            new("Du starter på landsbyens side. Hvis du overtager et andet kort, bliver det din nye rolle og bestemmer dit mål.",
                "You start on the village side. If you take another card, it becomes your role and sets your goal."),
            new("Når landsbyen har stemt en anden spiller ud, har du et kort øjeblik til at handle. Du skal melde dig, før den udstemtes kort bliver vist.",
                "When the village votes another player out, you have a brief moment to act. Speak up before the eliminated player's card is revealed."),
            new("Vis dit eget kort og tag den udstemtes kort uden at vise det til de andre. Rollen er nu din resten af spillet, og den udstemte er stadig ude. Den nye evne begynder som regel forfra. Som skuespiller får du dog kun de kort, der er tilbage på bordet. Er du selv en af Amors forelskede, kan du ikke bytte.",
                "Show your card and take the eliminated player's card without revealing it. You keep that role for the rest of the game, and the other player stays out. The new power usually starts fresh. As the Actor, though, you get only the cards still on the table. If you are one of Cupid's lovers, you cannot make the swap."),
            new("Nora er stemt ud. Du viser dit kort, før hun vender sit, og opdager, at hun var heks. Fra næste nat har du begge heksens drikke til rådighed, også selv om Nora allerede brugte en.",
                "Nora has been voted out. You show your card before she flips hers and discover she was the Witch. From the next night you have both potions, even if Nora had already used one."), 11);

        cards["fox"] = new(
            new("Find ud af, hvilken del af bordet der gemmer en varulv, og brug det spor i dagens diskussion.",
                "Work out which part of the table hides a werewolf and bring that clue into the day's discussion."),
            new("Spillederen kalder på dig hver nat. Du vælger selv, om du vil undersøge en gruppe eller gemme evnen til en senere nat.",
                "The moderator calls you each night. You can investigate a group or save your power for another night."),
            new("Peg på spilleren i midten af tre naboer. En tommel op betyder, at mindst én af de tre er varulv. Hvem det er, forbliver hemmeligt. En tommel ned frikender alle tre i det øjeblik og koster dig evnen resten af spillet.",
                "Point to the middle player in a group of three neighbours. A thumbs-up means at least one is a werewolf. Their identity stays secret. A thumbs-down clears all three at that moment and uses up your power for the rest of the game."),
            new("Asta, Bo og Liv sidder på række. Du peger på Bo og får tommel op. Da Liv anklager Asta næste dag, har du en grund til at lytte nøje til begge og holde øje med Bo.",
                "Asta, Bo and Liv are sitting in a row. You point to Bo and get a thumbs-up. When Liv accuses Asta the next day, you have a reason to listen closely to both and keep an eye on Bo."), 13);
    }
}
