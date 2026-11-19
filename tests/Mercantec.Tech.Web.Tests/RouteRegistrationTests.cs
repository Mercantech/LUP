using System.Reflection;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;

namespace Mercantec.Tech.Web.Tests;

public sealed class RouteRegistrationTests
{
    [Fact]
    public void Routable_components_have_unique_templates_so_the_router_can_start()
    {
        // Router builds the table for the whole app before opening any page. A duplicate
        // hidden in an old demo therefore breaks navigation to unrelated pages as well.
        var duplicateRoutes = typeof(App).Assembly.GetTypes()
            .Where(type => typeof(IComponent).IsAssignableFrom(type))
            .SelectMany(type => type.GetCustomAttributes<RouteAttribute>(inherit: false)
                .Select(route => new { Template = Normalize(route.Template), Component = type.FullName }))
            .GroupBy(route => route.Template, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(route => route.Component))}")
            .ToArray();

        Assert.True(duplicateRoutes.Length == 0,
            $"Ambiguous routes prevent the app router from starting:{Environment.NewLine}{string.Join(Environment.NewLine, duplicateRoutes)}");
    }

    [Theory]
    [InlineData("/notes", "Home", null)]
    [InlineData("/notes/connections", "Connections", null)]
    [InlineData("/notes/graph", "Graph", null)]
    [InlineData("/notes/11111111-aaaa-4111-8111-111111111111", "Index", "PageId")]
    [InlineData("/notes/ml2027", "Index", "CollectionSlug")]
    public void Notes_routes_match_literal_pages_UUIDs_and_slugs(string url, string expectedComponent, string? expectedParameter)
    {
        using var context = new BunitContext();
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(url);
        RouteData? route = null;
        context.Render<Router>(parameters => parameters
            .Add(p => p.AppAssembly, typeof(App).Assembly)
            .Add(p => p.Found, (RouteData match) => _ => route = match));

        Assert.Equal(expectedComponent, route?.PageType.Name);
        if (expectedParameter is not null) Assert.NotNull(route!.RouteValues[expectedParameter]);
    }

    [Fact]
    public void Legacy_note_route_preserves_query_and_fragment()
    {
        using var context = new BunitContext();
        var nav = context.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/notion/collections/11111111-aaaa-4111-8111-111111111111?published=true#side");
        context.Render<Pages.Notion.LegacyNotesRoute>();
        Assert.EndsWith("/notes/collections/11111111-aaaa-4111-8111-111111111111?published=true#side", nav.Uri);
    }

    private static string Normalize(string template) => Regex.Replace(
        "/" + template.Trim('/'), @"\{(\*?)[^}:?]+([^}]*)\}", "{$1parameter$2}").ToLowerInvariant();
}
