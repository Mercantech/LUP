namespace Mercantec.Tech.Web.Pages.SocialGames;

internal static partial class WerewolfCardGuide
{
    private static void AddExpansion(Dictionary<string, RoleGuide> cards)
    {
        cards["bear-tamer"] = new(
            new("Hjælp landsbyen med at finde ulvene. Din bjørn kan lugte en ulv på pladsen ved siden af dig.",
                "Help the village find the wolves. Your bear can smell a wolf sitting next to you."),
            new("Første nat kalder spillederen dig frem for at se, hvor du sidder. Hver morgen lytter hele bordet efter bjørnen, så snart nattens ofre er blevet nævnt.",
                "On the first night the moderator calls you to see where you sit. Each morning everyone listens for the bear as soon as the overnight eliminations have been announced."),
            new("Spring alle udgåede spillere over, og find din nærmeste levende nabo på hver side. Spillederen brummer højt, hvis mindst én af dem er en varulv. Bliver du selv smittet af urulven, brummer bjørnen hver morgen, så længe du er i spil.",
                "Skip any eliminated players and find your nearest living neighbour on each side. The moderator growls aloud if either neighbour is a werewolf. If the Accursed Wolf infects you, the bear growls every morning for as long as you remain in play."),
            new("Du er Titus. Dine nærmeste levende naboer er Liv til venstre og Omar til højre. Bjørnen brummer. Så længe du selv er usmittet, ved du, at Liv, Omar eller begge er ulve. Du må bruge samtalen til at finde ud af hvem.",
                "You are Titus. Your nearest living neighbours are Liv on your left and Omar on your right. The bear growls. As long as you are uninfected, you know that Liv, Omar or both are wolves. What they say during the day may help you narrow it down."),
            14);

        cards["sectarian"] = new(
            new("Du har dit eget mål. Vind ved at blive i spil, indtil alle i den modsatte gruppe er ude, uanset hvilke rollekort de har.",
                "You have your own goal. Stay in play until everyone in the opposite group is out, whatever role cards they hold."),
            new("Før kortene kommer i spil, annoncerer spillederen en synlig inddeling af hele bordet i to grupper. Du tilhører selv den ene. Grupperne må gerne være forskellige i størrelse.",
                "Before play begins, the moderator publicly divides the table into two groups using a visible distinction. You belong to one of them. The groups can be different sizes."),
            new("Du bruger samtalen og din almindelige stemme til at påvirke, hvem der ryger ud. Når den sidste i den anden gruppe er ude, vinder du straks og forlader spillet. De øvrige fortsætter, hvis de kan. Bliver du smittet af urulven inden da, opgiver du dit mål og spiller videre som almindelig varulv.",
                "Use the discussion and your normal vote to influence who goes out. Once the last member of the other group is out, you claim your win and leave the game. The others continue if they can. If the Accursed Wolf infects you first, you give up your goal and play on as an ordinary werewolf."),
            new("Spillederen bruger bordets to langsider som skel. Du sidder ved vinduet, og Alba er den sidste spiller på dørsiden. Ryger Alba ud, har du vundet, selv om der stadig sidder en ulv på din egen side.",
                "The moderator divides the table by its two long sides. You sit on the window side. Alba is the last player on the door side. When Alba goes out, you win even if a wolf is still sitting on your side."),
            15);

        cards["wild-child"] = new(
            new("Du starter på landsbyens side. Dit forbilledes skæbne afgør, om du senere skal vinde med ulvene.",
                "You start on the village side. What happens to your role model determines whether you will later play to win with the wolves."),
            new("Første nat peger du i hemmelighed på en spiller som dit forbillede, når spillederen vækker dig.",
                "On the first night, when the moderator wakes you, secretly point to the player you choose as your role model."),
            new("Så længe dit forbillede lever, er du landsbyboer, også hvis forbilledet selv er en ulv. Ryger spilleren ud, bliver du straks varulv og deltager i flokkens jagt fra næste nat. Du må gerne stemme på dit eget forbillede. Ryger du selv ud, kan spillederen holde dit sidste hold hemmeligt.",
                "You remain a villager while your role model is alive, even if that player is a wolf. If they are eliminated, you immediately become a werewolf and join the pack’s hunt from the following night. You may vote against your own role model. If you go out, the moderator may keep your final allegiance secret."),
            new("Du vælger Sam. Bliver dagens afstemning Sams sidste, åbner du øjnene med ulvene i nat. Havde landsbyen i stedet stemt den sidste ulv ud, mens Sam stadig levede, ville du have vundet med landsbyen.",
                "You choose Sam. If the village votes Sam out today, you open your eyes with the wolves tonight. Had the village voted out the last wolf while Sam was still alive, you would have won with the village."),
            16);

        cards["wolfhound"] = new(
            new("Vælg, hvem du vil hjælpe med at vinde. Du kan spille hele partiet som almindelig landsbyboer eller som varulv.",
                "Choose whom you want to help win. You can play the whole game as an ordinary villager or as a werewolf."),
            new("Du træffer dit valg den første nat, når spillederen kalder ulvene frem. Valget står fast. Ryger du ud, kan spillederen holde dit valg hemmeligt for bordet.",
                "Make your choice on the first night when the moderator calls the wolves. Your decision is final. If you go out, the moderator may keep your choice secret from the table."),
            new("Vil du med flokken, åbner du øjnene, møder de andre ulve og vælger nattens offer sammen med dem hver nat. Vil du hjælpe landsbyen, holder du øjnene lukket og bruger diskussionen og afstemningen til at finde ulvene.",
                "To join the pack, open your eyes, meet the other wolves and help choose their victim each night. To join the village, keep your eyes closed and use the daytime discussion and vote to find the wolves."),
            new("Da ulvene vågner, holder du øjnene lukket. Næste dag bliver din ven Noor stemt ud som varulv. Du fortsætter på landsbyens side, selv om du nu hellere ville have hjulpet Noor.",
                "You keep your eyes closed when the wolves wake. The next day your friend Noor is voted out and turns out to be a werewolf. You carry on playing for the village, even if you now wish you had helped Noor."),
            18);

        cards["white-werewolf"] = new(
            new("Vind alene ved at være den sidste overlevende. En sejr til de øvrige ulve afslutter spillet uden at give dig sejren.",
                "Win alone by being the last player alive. If the other wolves reach their winning condition, the game ends without a win for you."),
            new("Hver nat jager du med flokken. Hver anden nat vækker spillederen dig også alene, efter at flokken har valgt sit offer.",
                "Hunt with the pack every night. On alternate nights the moderator also wakes you alone after the pack has chosen its victim."),
            new("På din hemmelige ekstratur kan du pege på en anden levende varulv og få den ud af spillet. Du må gerne lade turen gå. Du skal både holde dig i live og sørge for, at flokken ikke når sit fælles mål først.",
                "On your secret extra turn, you may point to another living werewolf to eliminate them. You can also pass. You need to stay alive and keep the pack from reaching its own goal first."),
            new("Du og Malik vælger Freja under flokkens jagt. Da spillederen vækker dig alene bagefter, kan du også vælge Malik. Lader du ham leve, har du stadig en allieret ved næste afstemning og en rival til den sidste plads.",
                "You and Malik pick Freja during the pack’s hunt. When the moderator wakes you alone afterwards, you can also choose Malik. If you spare him, you keep an ally for the next vote and a rival for the last place."),
            19);

        cards["big-bad-wolf"] = new(
            new("Vind sammen med ulvene. Tidligt i spillet kan du hjælpe flokken ved at tage et ekstra offer.",
                "Win with the wolves. Early in the game, you can help the pack claim an extra victim."),
            new("Du jager med ulvene hver nat. Så længe ingen varulv, intet ulvebarn og ingen ulvehund er røget ud, vækker spillederen dig igen efter flokkens jagt.",
                "Hunt with the wolves every night. As long as no werewolf, Wild Child or Wolfhound has been eliminated, the moderator wakes you again after the pack’s hunt."),
            new("På din anden tur udpeger du endnu et offer, som ikke må være en varulv. Så snart én af de nævnte roller ryger ud, bortfalder din ekstra jagt resten af spillet. Du deltager stadig i flokkens almindelige jagt.",
                "On your second turn, choose another victim who is not a werewolf. As soon as any of the listed roles goes out, you lose this extra hunt for the rest of the game. You still take part in the pack’s regular hunt."),
            new("Første nat vælger flokken Thea, og på din egen tur vælger du Bo. Næste dag stemmer landsbyen ulvehunden ud. Fra nu af vågner du kun med flokken, også hvis ulvehunden havde valgt landsbyens side.",
                "On the first night the pack picks Thea, then you pick Bo on your own turn. The next day the village votes out the Wolfhound. From now on you wake only with the pack, even if the Wolfhound had chosen the village side."),
            19);

        cards["accursed-wolf"] = new(
            new("Hjælp flokken med at vinde. Én gang kan du gøre det mere værdifuldt at lade nattens offer leve ved at få spilleren over på ulvenes side.",
                "Help the pack win. Once per game you can make a victim more valuable alive by bringing that player over to the wolves’ side."),
            new("Jagt med de andre hver nat. Når ulvene har valgt offeret og lukket øjnene igen, kan du bruge din ene smitte ved at løfte hånden.",
                "Hunt with the others every night. After the pack has picked its victim and closed its eyes again, raise your hand if you want to use your one chance to infect."),
            new("Smitten erstatter flokkens angreb på den spiller, I allerede har udpeget. Spillederen giver spilleren et hemmeligt tegn. Spilleren bliver straks varulv, vågner med flokken fra næste nat og beholder som udgangspunkt sin særlige evne. Sektlederen mister sit personlige mål ved smitte.",
                "Infection replaces the pack’s attack on the player it already chose. The moderator signals to that player in secret. They become a werewolf immediately, join the pack from the next night and normally keep their special ability. An infected Sect Leader loses their personal goal."),
            new("Flokken peger på Asta, og du bruger smitten. Asta er seeren. Næste nat kan hun både undersøge et rollekort og vågne med flokken. Hendes viden hjælper nu ulvene.",
                "The pack points to Asta and you use your infection. Asta is the seer. The next night she can both inspect a role and wake with the pack. Her information now helps the wolves."),
            20);

        cards["spirit-talker"] = new(
            new("Hjælp landsbyen med at få et offentligt svar fra den første spiller, der røg ud. Din evne bruger de fem spiritismekort fra udvidelsen Volle Maan.",
                "Help the village get a public answer from the first player eliminated. Your ability uses the five Spiritualism cards from the Volle Maan (New Moon) expansion."),
            new("Spillederen lægger de fem kort til side ved start. Hver nat må du vælge at bruge et af dem eller vente. Svaret kommer næste morgen.",
                "The moderator sets the five cards aside at setup. Each night you can use a card or save it for later. The answer comes the next morning."),
            new("Når du bruger evnen, vælger spillederen et af kortene og læser de fire spørgsmål højt. Vis med én til fire fingre, hvilket spørgsmål du vælger, og peg på den spiller, der skal være medium. Om morgenen stiller mediet spørgsmålet højt. Den først udgåede spiller svarer ja eller nej, og kortet lægges væk.",
                "When you use your ability, the moderator picks a card and reads its four questions aloud. Hold up one to four fingers to choose the question, then point to the player who will act as the medium. In the morning the medium asks it aloud. The first player eliminated answers yes or no, and the card is discarded."),
            new("Iben var den første, der røg ud. Du viser tre fingre og peger på Emil. Ved daggry stiller Emil kortets tredje spørgsmål, og hele landsbyen hører Ibens svar.",
                "Iben was the first person eliminated. You hold up three fingers and point to Emil. At dawn Emil asks the third question on the card, and the whole village hears Iben’s answer."),
            25);

        cards["town-crier"] = new(
            new("Du beholder dit eget rollekort og dit mål. Som byudråber får du desuden lov til at vælge, hvilke begivenheder fra Volle Maan der kommer i spil.",
                "You keep your own role card and its goal. As town crier, you also get to choose which events from Volle Maan (New Moon) enter play."),
            new("Borgmesteren udpeger dig offentligt og kan kun vælge en anden end sig selv. Fra udnævnelsen spørger spillederen dig om morgenen, om du har noget at bekendtgøre.",
                "The mayor appoints you publicly and must choose someone other than themselves. Once appointed, the moderator asks you in the morning whether you have an announcement."),
            new("Før spillet vælger spillederen dine begivenhedskort og lader spiritismekortene ligge. Du kan gemme kortene eller læse ét højt. I følger kortets regel og lægger det væk. Efter en bekendtgørelse og før afstemningen kan borgmesteren udpege en ny byudråber. Ryger du ud, udpeger borgmesteren din afløser. Bliver du borgmester, giver du hvervet videre. Din afløser overtager de ubrugte kort. En ny borgmester kan beholde byudråberen eller straks udnævne en anden.",
                "The moderator gives you a hand of event cards chosen at setup, excluding Spiritualism cards. You can pass or read one aloud. Apply its effect and discard it. After an announcement and before the vote, the mayor can replace you. If you go out, the mayor appoints a successor. If you become mayor, you pass on the office. The next crier inherits the unused cards. A new mayor may keep the current crier or immediately appoint a new one."),
            new("Borgmester Eli giver dig hvervet. Du får tre begivenhedskort og bruger ét i dag. Da Eli udpeger Uma efter bekendtgørelsen, giver du Uma de to kort, der er tilbage. Dit eget rollekort bliver hos dig.",
                "Mayor Eli appoints you. You receive three event cards and use one today. When Eli names Uma as crier after the announcement, you hand Uma the two remaining cards. Your own role card stays with you."),
            26);
    }
}
