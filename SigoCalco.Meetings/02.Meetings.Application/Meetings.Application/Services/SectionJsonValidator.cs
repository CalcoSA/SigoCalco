using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Meetings.Domain.Exceptions;

namespace Meetings.Application.Services;

public interface ISectionContentValidator
{
    string ContentType { get; }
    void ValidateConfiguration(JsonElement? configuration);
    void ValidateContent(JsonElement? configuration, JsonElement? content, bool required);
}

public sealed class SectionJsonValidator(IEnumerable<ISectionContentValidator> validators)
{
    public static readonly string[] ContentTypes = ["TEXTO", "LISTA", "TABLA", "FORMULARIO", "PARTICIPANTES"];
    private readonly Dictionary<string, ISectionContentValidator> strategies = validators.ToDictionary(x => x.ContentType);

    public void ValidateConfiguration(string contentType, JsonElement? configuration)
    {
        if (!ContentTypes.Contains(contentType)) throw MeetingsRequestException.Invalid("contentType no admitido.");
        CheckJson(configuration);
        if (strategies.TryGetValue(contentType, out var validator)) validator.ValidateConfiguration(configuration);
        else if (HasValue(configuration))
            throw MeetingsRequestException.Invalid($"La configuración de {contentType} aún no tiene un contrato definido; utilice null.");
    }

    public void ValidateContent(string contentType, JsonElement? configuration, JsonElement? content, bool required)
    {
        ValidateConfiguration(contentType, configuration);
        CheckJson(content);
        if (strategies.TryGetValue(contentType, out var validator)) validator.ValidateContent(configuration, content, required);
        else if (required || HasValue(content))
            throw MeetingsRequestException.Invalid($"No se ha definido un validador de contenido para {contentType}.");
    }

    public static bool HasValue(JsonElement? value) => value.HasValue && value.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);

    private static void CheckJson(JsonElement? value)
    {
        if (!HasValue(value)) return;
        if (value!.Value.GetRawText().Length > 65536) throw MeetingsRequestException.Invalid("JSON demasiado grande.");
        CheckDuplicates(value.Value);
    }

    private static void CheckDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!keys.Add(property.Name)) throw MeetingsRequestException.Invalid("El JSON contiene claves duplicadas.");
                CheckDuplicates(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) CheckDuplicates(item);
    }
}

public sealed class FormSectionValidator : ISectionContentValidator
{
    public string ContentType => "FORMULARIO";
    private static readonly string[] FieldTypes = ["text", "date", "time", "number", "boolean"];
    public void ValidateConfiguration(JsonElement? configuration)
    {
        if (!SectionJsonValidator.HasValue(configuration) || configuration!.Value.ValueKind != JsonValueKind.Object ||
            !configuration.Value.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Array ||
            fields.GetArrayLength() is < 1 or > 100)
            throw MeetingsRequestException.Invalid("FORMULARIO requiere fields con entre 1 y 100 campos.");
        if (configuration.Value.EnumerateObject().Any(x => x.Name != "fields"))
            throw MeetingsRequestException.Invalid("Propiedad de configuración no admitida.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields.EnumerateArray())
        {
            if (field.ValueKind != JsonValueKind.Object ||
                !field.TryGetProperty("key", out var key) || key.ValueKind != JsonValueKind.String ||
                !Regex.IsMatch(key.GetString()!, "^[A-Za-z][A-Za-z0-9_]{0,63}$") ||
                !keys.Add(key.GetString()!) ||
                !field.TryGetProperty("label", out var label) || label.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(label.GetString()) || label.GetString()!.Length > 200 ||
                !field.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String ||
                !FieldTypes.Contains(type.GetString()) ||
                !field.TryGetProperty("required", out var required) || required.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw MeetingsRequestException.Invalid("Campo inválido: revise key única, label, type y required booleano.");
            if (field.EnumerateObject().Any(x => x.Name is not ("key" or "label" or "type" or "required")))
                throw MeetingsRequestException.Invalid("Propiedad de campo no admitida.");
        }
    }

    public void ValidateContent(JsonElement? configuration, JsonElement? content, bool required)
    {
        ValidateConfiguration(configuration);
        var fields = configuration!.Value.GetProperty("fields").EnumerateArray().ToArray();
        if (!SectionJsonValidator.HasValue(content))
        {
            if (required || fields.Any(f => f.GetProperty("required").GetBoolean()))
                throw MeetingsRequestException.Invalid("Falta contenido obligatorio del formulario.");
            return;
        }
        if (content!.Value.ValueKind != JsonValueKind.Object) throw MeetingsRequestException.Invalid("El contenido del formulario debe ser un objeto.");
        var definitions = fields.ToDictionary(f => f.GetProperty("key").GetString()!, StringComparer.Ordinal);
        if (content.Value.EnumerateObject().Any(p => !definitions.ContainsKey(p.Name)))
            throw MeetingsRequestException.Invalid("El contenido contiene campos no configurados.");
        foreach (var (key, field) in definitions)
        {
            if (!content.Value.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                if (field.GetProperty("required").GetBoolean()) throw MeetingsRequestException.Invalid($"El campo {key} es obligatorio.");
                continue;
            }
            var valid = field.GetProperty("type").GetString() switch
            {
                "text" => value.ValueKind == JsonValueKind.String && (!field.GetProperty("required").GetBoolean() || !string.IsNullOrWhiteSpace(value.GetString())),
                "date" => value.ValueKind == JsonValueKind.String && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                "time" => value.ValueKind == JsonValueKind.String && TimeOnly.TryParseExact(value.GetString(), ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                "number" => value.ValueKind == JsonValueKind.Number,
                "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                _ => false
            };
            if (!valid) throw MeetingsRequestException.Invalid($"El campo {key} no cumple el tipo configurado.");
        }
    }
}

