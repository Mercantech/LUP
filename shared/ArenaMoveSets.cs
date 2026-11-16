namespace Mercantec.Tech.Contracts;

public sealed record ArenaMoveDefinition(string Id, string Key, string Name, string Effect);
public sealed record ArenaMoveSet(string Id, string Role, string Resource, IReadOnlyList<ArenaMoveDefinition> Moves);

/// <summary>Player-facing controls. Combat values remain authoritative in ArenaSimulation.</summary>
public static class ArenaMoveSets
{
    public static ArenaMoveSet? Find(string? avatarId) => avatarId switch
    {
        "bip01" => Bip01,
        "raymond" => Raymond,
        "santa" => Santa,
        "lupp" => Lupp,
        AvatarEconomy.BilboId => Bilbo,
        _ => null
    };

    public static ArenaMoveSet Bip01 { get; } = new("bip01", "Tekniker", "Energi driver teknikken. Træffere lader kerneoverload.",
    [
        new("attack", "J", "Kredsløbsslag", "Hurtige slag og luftangreb."),
        new("pulse", "K", "Pulsbolt", "Hurtigt, lige energiprojektil."),
        new("grab", "U", "Magnetgreb", "Trækker fjender ind og absorberer deres projektiler."),
        new("shield", "L", "Bobleskjold", "Et præcist skjold kan sende projektiler tilbage."),
        new("dash", "A/D + K", "Raketryk", "Kort, hurtigt ryk med et beskyttet afsæt."),
        new("recovery", "W + K", "Rakethop", "Stejlt løft tilbage mod banen."),
        new("repair", "S + K", "Reparationsfelt", "Bruger energi til at reparere skade. Kan afbrydes."),
        new("ultimate", "Hold I", "Kerneoverload", "Fuld måler: eksplosion omkring BIP. Tømmer energien.")
    ]);

    public static ArenaMoveSet Raymond { get; } = new("raymond", "Kontrol", "Skjold bruges til gendannelse. Træffere og absorberede projektiler lader JOIN.",
    [
        new("attack", "J", "Snabelslag", "Langsommere, tungere slag med længere rækkevidde."),
        new("pulse", "K", "Snabelpuls", "Stort, langsomt projektil med kraftigt tilbageskub."),
        new("grab", "U", "Relationsgreb", "Fastholder én fjende. Vælg retning og kast med U eller J."),
        new("shield", "L", "Tryghedsskjold", "Absorberer projektiler og lader JOIN."),
        new("dash", "A/D + K", "Elefantstorm", "Vedvarende storm, der kan modstå én træffer."),
        new("recovery", "W + K", "Snabelløft", "Tungt løft med et bredt redningsangreb."),
        new("repair", "S + K", "Gendannelse", "Omsætter skjold til heling efter en sårbar opladning."),
        new("ultimate", "Hold I", "Den store JOIN", "Fuld måler: komet trækker fjender sammen og eksploderer.")
    ]);
    public static ArenaMoveSet Lupp { get; } = new("lupp", "Spildesigner", "Idékraft driver prototyperne. Træffere lader Game Jam. Afprøv, få feedback og prøv igen.",
    [
        new("attack", "J", "Modellerslag", "Et elastisk, hurtigt slag med kort rækkevidde."),
        new("pulse", "K", "Idépuls", "En idégnist springer én gang på banen. Koster 12 idékraft."),
        new("grab", "U", "Feedbackgreb", "Vender og skubber en nærliggende fjende. Koster 16 idékraft."),
        new("shield", "L", "Prototypeværn", "Omsætter et blokeret projektil til en langsommere prototypekube."),
        new("dash", "A/D + K", "Iterationsryk", "Et elastisk ryk med et kort, beskyttet afsæt. Koster 20 idékraft."),
        new("recovery", "W + K", "Nyt forsøg", "Et let hop tilbage mod banen. Koster 18 idékraft."),
        new("repair", "S + K", "Playtestpause", "30 idékraft: heler 10 skade efter en sårbar pause. Kan afbrydes."),
        new("ultimate", "Hold I", "Game Jam", "Fuld måler: fem prototypekuber i en vifte, der springer på banen. Kan afbrydes.")
    ]);
    public static ArenaMoveSet Bilbo { get; } = new(AvatarEconomy.BilboId, "Banekontrol",
        "Bilbo bruger sit lager til evner. Lageret fyldes langsomt op, og træffere lader Ordrekøen.",
    [
        new("attack", "J", "Bodslag", "Et fast slag med længst rækkevidde fra jorden."),
        new("pulse", "K", "Syltetøjskast", "Koster 14 lager. Glasset hopper én gang og kan efterlade en klistret plet, som sænker modstanderne."),
        new("grab", "U", "Køgreb", "Trækker en nær modstander ind mod boden fra jorden. Koster 18 lager."),
        new("shield", "L", "Glasværn", "Bruger mindre skjold på at stoppe projektiler."),
        new("dash", "A/D + K", "Bodryk", "Ryk frem og skub en modstander væk. Koster 22 lager."),
        new("recovery", "W + K", "Bodspring", "Spring op mod banen. Koster 20 lager."),
        new("repair", "S + K", "Smagepause", "Koster 37 lager. Heler op til 13 skade efter en kort pause, der kan afbrydes."),
        new("ultimate", "Hold I", "Ordrekøen", "Fuld måler. Tre glas slynges afsted ét ad gangen i forskellige buer. Træffere kan afbryde de resterende kast.")
    ]);

    public static ArenaMoveSet Santa { get; } = new("santa", "Julemagi", "Julemagi driver evnerne. Julefred omsætter projektiler til magi. Træffere lader gavefesten.",
    [
        new("attack", "J", "Juleslag", "Et roligt slag med mellemstor rækkevidde."),
        new("pulse", "K", "Stjerneskud", "Et gyldent stjerneskud følger en let bue. Koster 14 magi."),
        new("grab", "U", "Gavegreb", "Løfter én nærliggende fjende gennem skjoldet. Koster 20 magi."),
        new("shield", "L", "Julefred", "Blokerer projektiler og gendanner 10 magi pr. blokering."),
        new("dash", "A/D + K", "Kanesus", "Et svævende ryk fremad. Koster 22 magi."),
        new("recovery", "W + K", "Skorstensspring", "Et højt løft med en kort beskyttende slagbevægelse. Koster 16 magi."),
        new("repair", "S + K", "Julevarme", "45 magi: heler 12 skade og gendanner 12 skjold efter en sårbar pause."),
        new("ultimate", "Hold I", "Den store gavefest", "Fuld måler: tre magiske snebolde i en vifte. Kan afbrydes under opladningen.")
    ]);
}
