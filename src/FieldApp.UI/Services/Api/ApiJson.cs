using System.Text.Json;
using System.Text.Json.Serialization;

namespace PharmaERP.FieldApp.UI.Services.Api;

/// <summary>Serializer settings matching Web.Api (camelCase + enums as strings, see its Program.cs).</summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
