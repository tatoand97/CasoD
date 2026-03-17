using System.Text.Json;
using System.IO;

namespace CasoD.Services;

internal static class AppConfigLoader
{
    private const string SettingsFileName = "appsettings.json";

    public static AppConfig Load()
    {
        string settingsPath = Path.Combine(AppContext.BaseDirectory, SettingsFileName);

        if (!File.Exists(settingsPath))
        {
            // Create a template appsettings.json so the developer can fill it in.
            string template = @"{
  ""ProjectEndpoint"": ""https://contoso.foundry.microsoft.com/api/projects/<project-id>"",
  ""ModelDeploymentName"": ""deployment-name"",
  ""OrderAgentId"": ""order-agent-id"",
  ""RefundAgentId"": null
}";

            File.WriteAllText(settingsPath, template);
        }

        string json = File.ReadAllText(settingsPath);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        static string GetRequired(JsonElement root, string key)
        {
            if (!root.TryGetProperty(key, out JsonElement prop) || prop.ValueKind == JsonValueKind.Null)
            {
                throw new InvalidOperationException($"Missing required configuration: {key} in {SettingsFileName}");
            }

            if (prop.ValueKind != JsonValueKind.String)
            {
                // allow numeric/other kinds only if they can be represented as string
                string asText = prop.ToString();
                if (string.IsNullOrWhiteSpace(asText))
                {
                    throw new InvalidOperationException($"Missing required configuration: {key} in {SettingsFileName}");
                }

                return asText.Trim();
            }

            string? value = prop.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Missing required configuration: {key} in {SettingsFileName}");
            }

            return value.Trim();
        }

        static string? GetOptional(JsonElement root, string key)
        {
            if (!root.TryGetProperty(key, out JsonElement prop) || prop.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                string? s = prop.GetString();
                return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            }

            string asText = prop.ToString();
            return string.IsNullOrWhiteSpace(asText) ? null : asText.Trim();
        }

        string endpointRaw = GetRequired(root, "ProjectEndpoint");
        string deployment = GetRequired(root, "ModelDeploymentName");
        string orderAgentId = GetRequired(root, "OrderAgentId");
        string? refundAgentId = GetOptional(root, "RefundAgentId");

        Uri endpoint = ValidateProjectEndpoint(endpointRaw);

        return new AppConfig(
            ProjectEndpoint: endpoint,
            ModelDeploymentName: deployment,
            OrderAgentId: orderAgentId,
            RefundAgentId: refundAgentId);
    }

    private static Uri ValidateProjectEndpoint(string rawEndpoint)
    {
        if (!Uri.TryCreate(rawEndpoint, UriKind.Absolute, out Uri? endpoint) ||
            !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"ProjectEndpoint must be an absolute HTTPS URL.");
        }

        if (!rawEndpoint.Contains("/api/projects/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"ProjectEndpoint must be a Foundry Project endpoint containing '/api/projects/'.");
        }

        if (rawEndpoint.Contains(".openai.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"ProjectEndpoint cannot be an Azure OpenAI resource endpoint.");
        }

        return endpoint;
    }
}
