using System.Text.RegularExpressions;
using Mercantec.Tech.Contracts;
using Mercantec.Tech.Web.Characters;

namespace Mercantec.Tech.Api.Notebooks;

public sealed record NotebookCoverArt(string? CharacterSlug, string? CharacterAssetPath, string StyleAssetPath,
    string PromptDirection);

public static partial class NotebookCoverStyle
{
    private static readonly Banner Overview = new("images/valgfag/valgfag-overblik.webp",
        "Take the overview reference's depth, dark navy grounding and clear negative space. Render the new subject as a warm, tactile editorial illustration with ivory, muted teal and amber accents. Do not copy the astronaut or the photograph.");
    private static readonly Banner Deployment = new("images/valgfag/deploy-or-die.webp",
        "Use the deployment banner's deep ocean blue, warm ivory, faint grain and bold, simple silhouettes. Build a new scene and leave all lettering and brand marks out.");
    private static readonly Banner GameDesign = new("images/valgfag/game-design.webp",
        "Use the game design banner's midnight backdrop, ivory forms and orange and teal accents. Give the subject the same clear, playful silhouette and generous negative space.");
    private static readonly Banner MachineLearning = new("images/valgfag/machine-learning.webp",
        "Use the machine learning banner's dark petrol blue, cream clay, mint connections and small orange accents. Preserve its sculpted depth and legible shapes without copying the title or arrangement.");
    private static readonly Banner GeneralCourse = new("images/valgfag/machine-learning.webp",
        "Use this LUP elective banner as a reference for its editorial composition, sculpted materials, cool dark backdrop and accents. Build entirely different objects around the actual course subject.");

    private static readonly Topic[] Topics =
    [
        new("whale", Deployment, "Find a visual analogy for the concrete deployment, build or container concept from the material.",
            ["deploy or die", "deployment", "docker", "dockerfile", "docker compose", "dokploy", "containerisering",
                "container image", "kubernetes", "devnet", "ci cd", "ci pipeline", "github actions", ".net aspire"]),
        new("lupp", GameDesign, "Turn the game's specific mechanic, player choice, level or playtest into one coherent miniature scene.",
            ["game design", "spildesign", "spiludvikling", "game development", "gameplay", "game jam", "rune run", "godot",
                "unity engine", "unreal engine", "level design", "spilmekanik", "playtest", "lupp"]),
        new("chameleon", MachineLearning, "Give the actual data or model concept a tactile visual equivalent, such as grouped examples, a prediction or a decision branch when relevant.",
            ["machine learning", "maskinlæring", "scikit learn", "sklearn", "decision tree", "beslutningstræ", "beslutningstræer",
                "random forest", "lineær regression", "linear regression", "logistisk regression", "træningsdata", "konfusionsmatrix",
                "neural network", "neurale netværk", "klassifikationsmodel", "kamæleonen"]),
        new("raymond", null, "Show the actual database relation or query through a clear scene of matching, filtering or organizing tangible objects.",
            ["sql", "postgresql", "postgres", "sqlite", "mysql", "tsql", "databaseprogrammering", "relationel database",
                "relationelle databaser", "foreign key", "fremmednøgle", "inner join", "left join", "group by", "raymond"]),
        new("bilbo", null, "Use a small workshop or jam-stall scene to make the specific backend, data contract or order flow tangible.",
            ["c#", "csharp", "asp.net", "asp.net core", ".net", "ef core", "entity framework", "linq", "backendprogrammering",
                "minimal api", "minimal apis", "idempotens", "bilbo backend"]),
        new("gitmejsen", null, "Translate the actual version-control step into branches, checkpoints or a shared work trail.",
            ["git", "versionsstyring", "version control", "gitmejsen", "merge conflict", "mergekonflikt", "pull request",
                "git rebase", "git commit", "git branch"]),
        new("karla", null, "Make the actual mobile-development task visible through the devices, gestures or layout on Karla's workbench. Preserve her tall coral body, plum utility vest and wavy turquoise spiral shell.",
            ["flutter", "dart", "mobiludvikling", "mobile development", "layoutbuilder", "flutter widgets",
                "widget tree", "widgettest", "widget test", "hot reload", "materialapp", "karla eremitkrebs"]),
        new("gummianden", null, "Show the debugging subject as a small investigation at a desk, with evidence and one observable change.",
            ["debugging", "debugger", "rubber duck debugging", "gummianden", "breakpoint", "breakpoints", "betinget breakpoint",
                "call stack", "off by one", "regressionstest", "fejlrapport"]),
        new("bat", null, "Trace the actual signal, measurement or embedded behavior through a recognizable miniature lab.",
            ["iot", "embedded linux", "embedded systems", "indlejrede systemer", "firmware", "mqtt", "esp32", "arduino",
                "ekkolokalisering", "næsebladet"]),
        new("bee", null, "Make the specific cybersecurity subject visible through an illustrative, non-operational scene of prevention or response.",
            ["cybersikkerhed", "cybersecurity", "cyberbien", "informationssikkerhed", "security awareness", "honeypot",
                "phishing", "incident response", "hændelseshåndtering"]),
        new("beaver", null, "Give the defensive activity an accessible visual focus, such as monitoring or verification.",
            ["blue team", "blåt team", "sikkerhed og test", "siem", "defensive security", "threat hunting"]),
        new("fox", null, "Use an abstract security review scene with evidence and checklists, without operational technical details.",
            ["red team", "rødt team", "penetration testing", "penetrationstest", "pentest"])
    ];

    private static readonly Dictionary<string, CharacterEmojiAsset> CharacterReferences = CharacterEmojiCatalog.All
        .GroupBy(emoji => emoji.CharacterSlug, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.FirstOrDefault(emoji => emoji.Name == $"{group.Key}_hello")
            ?? group.FirstOrDefault(emoji => emoji.Name == $"{group.Key}_wave") ?? group.First(),
            StringComparer.OrdinalIgnoreCase);

    public static NotebookCoverArt Select(string kind, string title, IReadOnlyList<string> excerpts,
        IReadOnlyList<string> characterSlugs, string? courseTitle = null, string? illustrationDescription = null)
    {
        var titleTopic = FindTopic(title);
        var courseTopic = FindTopic(courseTitle);
        var subject = FindTopic(illustrationDescription) ?? titleTopic ?? courseTopic ?? FindTopic(string.Join('\n', excerpts));
        var explicitCharacter = characterSlugs.Select(slug => CharacterReferences.GetValueOrDefault(slug))
            .FirstOrDefault(reference => reference is not null);
        var character = explicitCharacter ?? (subject is null ? null : CharacterReferences.GetValueOrDefault(subject.CharacterSlug));
        var characterTopic = Topics.FirstOrDefault(topic => topic.CharacterSlug == character?.CharacterSlug);
        var banner = titleTopic?.Banner ?? courseTopic?.Banner ?? subject?.Banner ?? characterTopic?.Banner
            ?? (!string.IsNullOrWhiteSpace(courseTitle) ? GeneralCourse : Overview);
        var scope = kind is "collection" or "notebook"
            ? "Create one cover motif that reflects the notebook as a whole."
            : "Illustrate one concrete idea that this page is about.";
        var direction = subject?.Direction ?? "Choose one concrete, original visual motif grounded in the material.";
        var characterReference = character ?? CharacterReferences.GetValueOrDefault("lupp");
        return new NotebookCoverArt(character?.CharacterSlug, characterReference?.Preview.Split('?')[0].TrimStart('/'),
            banner.ImagePath, $"{scope} {banner.Direction} {direction} {CharacterDirection(character)}");
    }

    public static bool IsSupportedCharacter(string? slug) => slug is not null && CharacterReferences.ContainsKey(slug);

    private static Topic? FindTopic(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var words = " " + string.Join(' ', TopicToken().Matches(text).Select(match => match.Value.ToLowerInvariant())) + " ";
        return Topics.Select(topic => (Topic: topic, Score: topic.Terms
                .Where(term => words.Contains($" {term} ", StringComparison.Ordinal))
                .Sum(term => 1 + term.Count(character => character == ' '))))
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .Select(candidate => candidate.Topic)
            .FirstOrDefault();
    }

    private static string CharacterDirection(CharacterEmojiAsset? character)
    {
        if (character is null)
            return "Use the LUPP reference only for tactile clay materials, softened silhouettes and warm expressive shapes. The new subject is the focus; do not depict LUPP or add a mascot.";
        var profile = CharacterCatalog.Find(character.CharacterSlug);
        var appearance = profile?.DisplayPortrait?.Alt ?? character.Alt;
        return $"Include the authentic LUP character {profile?.Name ?? character.CharacterSlug} in the new scene. " +
            $"Reference appearance: {appearance}. Preserve the face, species, signature palette and clothing. " +
            "Give the character a fresh pose or action that serves the subject. Do not copy the sample composition, badges or lettering.";
    }

    [GeneratedRegex(@"(?<![\p{L}\p{N}.])(?:asp\.net|\.net)(?![\p{L}\p{N}])|(?<![\p{L}\p{N}])c#(?![\p{L}\p{N}])|[\p{L}\p{N}]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TopicToken();

    private sealed record Banner(string ImagePath, string Direction);
    private sealed record Topic(string CharacterSlug, Banner? Banner, string Direction, string[] Terms);
}
