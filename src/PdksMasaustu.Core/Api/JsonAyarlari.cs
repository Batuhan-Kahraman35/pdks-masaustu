using System.Text.Json;
using System.Text.Json.Serialization;

namespace PdksMasaustu.Core.Api;

internal static class JsonAyarlari
{
    /// <summary>
    /// API camelCase alan adları ve snake_case enum değerleri kullanır
    /// (MolaGiris ⇄ "mola_giris", Gelmedi ⇄ "gelmedi").
    /// </summary>
    public static JsonSerializerOptions Secenekler { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
