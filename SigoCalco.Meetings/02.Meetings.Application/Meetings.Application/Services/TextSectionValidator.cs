using System.Text.Json;
using Meetings.Domain.Exceptions;

namespace Meetings.Application.Services;

public sealed class TextSectionValidator : ISectionContentValidator
{
    public string ContentType => "TEXTO";

    public void ValidateConfiguration(JsonElement? configuration)
    {
        // Historical null/absent mode means EDITABLE. It does not rewrite the version's JSON.
        if (!SectionJsonValidator.HasValue(configuration)) return;
        var config = configuration!.Value;
        if (config.ValueKind != JsonValueKind.Object)
            throw MeetingsRequestException.Invalid("TEXTO admite un objeto con mode y defaultText, o configuración histórica nula.");
        var hasDefault = config.TryGetProperty("defaultText", out var defaultText);
        if (hasDefault && defaultText.ValueKind != JsonValueKind.String)
            throw MeetingsRequestException.Invalid("defaultText debe ser un string.");
        if (!config.TryGetProperty("mode", out var mode)) return;
        if (config.EnumerateObject().Any(p => p.Name is not ("mode" or "defaultText")))
            throw MeetingsRequestException.Invalid("Propiedad de configuración de TEXTO no admitida.");
        if (mode.ValueKind != JsonValueKind.String || mode.GetString() is not ("FIXED" or "EDITABLE"))
            throw MeetingsRequestException.Invalid("mode debe ser FIXED o EDITABLE.");
        if (!hasDefault || mode.GetString() == "FIXED" && string.IsNullOrWhiteSpace(defaultText.GetString()))
            throw MeetingsRequestException.Invalid("TEXTO con mode requiere defaultText string; FIXED requiere texto no vacío.");
    }

    public void ValidateContent(JsonElement? configuration, JsonElement? content, bool required) =>
        ResolveContent(configuration, content, required);

    public JsonElement? ResolveContent(JsonElement? configuration, JsonElement? content, bool required)
    {
        ValidateConfiguration(configuration);
        var fixedText = SectionJsonValidator.HasValue(configuration) &&
            configuration!.Value.TryGetProperty("mode", out var mode) && mode.GetString() == "FIXED";
        var defaultText = SectionJsonValidator.HasValue(configuration) &&
            configuration!.Value.TryGetProperty("defaultText", out var initial) ? initial.GetString() : null;
        if (SectionJsonValidator.HasValue(content) && content!.Value.ValueKind != JsonValueKind.String)
            throw MeetingsRequestException.Invalid("contentJson de TEXTO debe ser un string JSON.");
        if (fixedText)
        {
            if (SectionJsonValidator.HasValue(content) && content!.Value.GetString() != defaultText)
                throw MeetingsRequestException.Invalid("El texto FIXED no puede modificarse; pertenece a la versión original de la plantilla.");
            // Canonical server value, even when the client omits the section or submits null.
            return JsonSerializer.SerializeToElement(defaultText);
        }
        var resolved = SectionJsonValidator.HasValue(content) ? content
            : !string.IsNullOrEmpty(defaultText) ? JsonSerializer.SerializeToElement(defaultText) : null;
        if (required && (!SectionJsonValidator.HasValue(resolved) || string.IsNullOrWhiteSpace(resolved!.Value.GetString())))
            throw MeetingsRequestException.Invalid("Falta contenido obligatorio de TEXTO.");
        return resolved;
    }
}

public sealed class SubtitleSectionValidator : ISectionContentValidator
{
    public string ContentType => "SUBTITULO";
    public void ValidateConfiguration(JsonElement? configuration)
    {
        if (SectionJsonValidator.HasValue(configuration))
            throw MeetingsRequestException.Invalid("SUBTITULO utiliza title y requiere configurationJson nulo.");
    }
    public void ValidateContent(JsonElement? configuration, JsonElement? content, bool required)
    {
        ValidateConfiguration(configuration);
        if (required || SectionJsonValidator.HasValue(content))
            throw MeetingsRequestException.Invalid("SUBTITULO es de presentación: no admite contenido ni isRequired=true.");
    }
}
