namespace Mercantec.Tech.Web.Characters;

internal static class BatMotionMaterials
{
    public static IReadOnlyList<CharacterIllustration> Scenes { get; } =
    [
        new("Teskeen i parkeringskælderen", "/images/characters/bat/scenes/teskeens-parkeringskaelder.png?v=5c15516e", "Næsebladet optager en teske over en betonklods under en enkelt lampe i den tomme parkeringskælder.", 1536, 1024)
        {
            Preview = "/images/characters/bat/scenes/teskeens-parkeringskaelder.webp?v=5c15516e"
        },
        new("Kalibrering ved målebænken", "/images/characters/bat/scenes/kalibrering-ved-maaalebaenken.png?v=5c15516e", "Næsebladet sammenligner en afstandssensors måling med en lineal og en flytbar træplade på målebænken.", 1536, 1024)
        {
            Preview = "/images/characters/bat/scenes/kalibrering-ved-maaalebaenken.webp?v=5c15516e"
        },
        new("Drivhusets Linux-gateway", "/images/characters/bat/scenes/drivhusets-linux-gateway.png?v=5c15516e", "En sonde i drivhusets plantebed er forbundet til Næsebladets målestation, hvor en skærm viser værdier over tid.", 1536, 1024)
        {
            Preview = "/images/characters/bat/scenes/drivhusets-linux-gateway.webp?v=5c15516e"
        },
        new("Natudsendelse med én teske", "/images/characters/bat/scenes/teskeens-natudsendelse.png?v=5c15516e", "Næsebladet står i teatrets lydstudie med en teske, en betonklods og en mikrofon under varme scenelamper.", 1536, 1024)
        {
            Preview = "/images/characters/bat/scenes/teskeens-natudsendelse.webp?v=5c15516e"
        }
    ];

    public static IReadOnlyList<CharacterAnimation> Animations { get; } =
    [
        new(
            "pulsen-og-ekkoet", "Pulsen og ekkoet", "Ultralyd",
            "Sensoren sender en lydpuls og måler tiden, til ekkoet vender tilbage. Gang lydhastigheden med den målte tid, og dividér med to, fordi pulsen rejser både ud og hjem.",
            "/images/characters/bat/animations/pulsen-og-ekkoet.gif?v=5c15516e",
            "/images/characters/bat/animations/pulsen-og-ekkoet.webp?v=5c15516e",
            "En cyan lydpuls går fra sensoren til træpladen. Ekkoet går tilbage til sensoren, og stopuret viser tiden for hele turen.",
            512, 512),
        new(
            "naar-ekkoet-udebliver", "Når ekkoet udebliver", "Timeout og tilstandsmaskine",
            "Mens målingen venter på et ekko, kører programmets andre opgaver videre. Hvis fristen udløber, markeres målingen som manglende, og tilstandsmaskinen gør klar til næste forsøg.",
            "/images/characters/bat/animations/naar-ekkoet-udebliver.gif?v=5c15516e",
            "/images/characters/bat/animations/naar-ekkoet-udebliver.webp?v=5c15516e",
            "En puls sendes uden at komme tilbage. En ravfarvet lampe markerer timeout, mens den separate ventilator fortsætter.",
            512, 512),
        new(
            "maalingens-vej-gennem-mqtt", "Målingens vej gennem MQTT", "MQTT",
            "Gatewayen læser sensoren og publicerer målingen på et topic. MQTT-brokeren sender beskeden videre til abonnenter med et matchende abonnement, så målingen kan vises på et dashboard.",
            "/images/characters/bat/animations/maalingens-vej-gennem-mqtt.gif?v=5c15516e",
            "/images/characters/bat/animations/maalingens-vej-gennem-mqtt.webp?v=5c15516e",
            "En ravfarvet datapakke flytter sig fra sensoren og dens gateway gennem brokeren til en skærm, hvor en ny søjle lyser.",
            512, 512)
    ];
}
