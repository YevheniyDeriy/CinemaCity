using System.Text;
using System.Text.Json.Nodes;

namespace CinemaCity.ApiGateway;

internal static class GatewayConfiguration
{
    public static Stream Load(IConfiguration configuration, string root)
    {
        var file = configuration["Gateway:RoutesFile"] ?? "ocelot.json";
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(root, file)))!;
        var monolithOnly = string.Equals(configuration["Gateway:Mode"], "Monolith", StringComparison.OrdinalIgnoreCase);
        foreach (var node in document["Routes"]!.AsArray())
        {
            var route = node!;
            var service = monolithOnly ? "Monolith" : route["Metadata"]!["Service"]!.GetValue<string>();
            var address = new Uri(configuration[$"Services:{service}Url"] ?? throw new InvalidOperationException($"Configure Services:{service}Url."));
            route["DownstreamScheme"] = address.Scheme;
            route["DownstreamHostAndPorts"] = new JsonArray(new JsonObject { ["Host"] = address.Host, ["Port"] = address.Port });
            if (monolithOnly) route["DownstreamPathTemplate"] = route["UpstreamPathTemplate"]!.GetValue<string>();
        }
        document["GlobalConfiguration"]!["BaseUrl"] = configuration["Gateway:BaseUrl"] ?? throw new InvalidOperationException("Configure Gateway:BaseUrl.");
        return new MemoryStream(Encoding.UTF8.GetBytes(document.ToJsonString()));
    }
}
