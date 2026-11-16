namespace Mercantec.Tech.Contracts;

public static class GlobalSearchCategories
{
    public const string All = "all";
    public const string People = "people";
    public const string Assignments = "assignments";
    public const string Badges = "badges";
    public const string Projects = "projects";
    public const string Courses = "courses";
    public const string Materials = "materials";
    public const string Notes = "notes";
    public const string Holds = "holds";
    public const string Games = "games";
    public const string KnowledgeChecks = "knowledge-checks";
    public const string Events = "events";
    public const string Polls = "polls";
    public const string Files = "files";
    public const string Electives = "electives";
    public const string Characters = "characters";
    public const string Pages = "pages";

    public static bool IsValid(string? category) => category is All or People or Assignments or Badges or Projects
        or Courses or Materials or Notes or Holds or Games or KnowledgeChecks or Events or Polls or Files or Electives or Characters or Pages;
}

public sealed record GlobalSearchHit(string Category, string Kind, string Title, string Href, string Context, string? Description = null);

public sealed record GlobalSearchResult(GlobalSearchHit[] Items, int Total);
