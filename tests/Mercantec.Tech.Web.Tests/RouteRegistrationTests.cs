using System.Reflection;
using System.Text.RegularExpressions;
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

    private static string Normalize(string template) => Regex.Replace(
        "/" + template.Trim('/'), @"\{(\*?)[^}:?]+([^}]*)\}", "{$1parameter$2}").ToLowerInvariant();
}
