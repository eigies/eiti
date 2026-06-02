using System.Text.Json;
using System.Text.Json.Nodes;

namespace eiti.Application.Common.Behaviors;

/// <summary>
/// Serializa el request de un comando a JSON ocultando propiedades sensibles
/// (contraseñas, tokens, secretos) en cualquier nivel del objeto.
/// </summary>
public static class AuditPayloadSerializer
{
    private const string Redacted = "***";

    private static readonly string[] SensitiveFragments =
    {
        "password",
        "token",
        "secret"
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    public static string? Serialize(object request)
    {
        try
        {
            var node = JsonSerializer.SerializeToNode(request, request.GetType(), SerializerOptions);
            if (node is null)
            {
                return null;
            }

            Redact(node);
            return node.ToJsonString(SerializerOptions);
        }
        catch
        {
            // Nunca debe romper el flujo del request por un problema de serialización.
            return null;
        }
    }

    private static void Redact(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToList())
                {
                    if (IsSensitive(property.Key))
                    {
                        obj[property.Key] = Redacted;
                        continue;
                    }

                    if (property.Value is not null)
                    {
                        Redact(property.Value);
                    }
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    if (item is not null)
                    {
                        Redact(item);
                    }
                }

                break;
        }
    }

    private static bool IsSensitive(string propertyName)
    {
        foreach (var fragment in SensitiveFragments)
        {
            if (propertyName.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
