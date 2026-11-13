using System.Text.Json;
using Meetings.Application.Services;
using Meetings.Domain.Exceptions;

namespace Meetings.Test;

public sealed class SectionValidationTests
{
    private readonly SectionJsonValidator validator = new([new FormSectionValidator()]);
    private static JsonElement Json(string value) => JsonSerializer.Deserialize<JsonElement>(value);

    [Fact]
    public void Validates_form_configuration_and_filled_data_separately()
    {
        validator.ValidateConfiguration("FORMULARIO", TemplateApiTests.Form);
        validator.ValidateContent("FORMULARIO", TemplateApiTests.Form, Json("""{"fecha":"2026-11-09","hora":"09:00","lugar":"Sala"}"""), true);
    }

    [Theory]
    [InlineData("""{"fecha":"2026-02-30","hora":"09:00","lugar":"Sala"}""")]
    [InlineData("""{"fecha":"2026-11-09","hora":"25:00","lugar":"Sala"}""")]
    [InlineData("""{"fecha":"2026-11-09","hora":"09:00"}""")]
    [InlineData("""{"fecha":"2026-11-09","hora":"09:00","lugar":" "}""")]
    [InlineData("""{"fecha":"2026-11-09","hora":"09:00","lugar":123}""")]
    [InlineData("""{"fecha":"2026-11-09","hora":"09:00","lugar":"Sala","extra":1}""")]
    [InlineData("""{"fecha":"2026-11-09","hora":"09:00","lugar":"A","lugar":"B"}""")]
    [InlineData("null")]
    public void Rejects_invalid_form_content(string value) =>
        Assert.Throws<MeetingsRequestException>(() => validator.ValidateContent("FORMULARIO", TemplateApiTests.Form, Json(value), true));

    [Theory]
    [InlineData("""{"fields":[]}""")]
    [InlineData("""{"fields":[{"key":"a","label":"A","type":"unknown","required":true}]}""")]
    [InlineData("""{"fields":[{"key":"a","label":"A","type":"text","required":"true"}]}""")]
    [InlineData("""{"fields":[{"key":"a","label":"A","type":"text","required":true},{"key":"a","label":"B","type":"text","required":false}]}""")]
    [InlineData("""{"fields":[{"key":"a","key":"b","label":"A","type":"text","required":true}]}""")]
    [InlineData("""{"fields":[{"key":"a","type":"text","required":true}]}""")]
    [InlineData("""{"fields":[{"key":"a","label":"A","type":"text","required":true,"mystery":1}]}""")]
    [InlineData("null")]
    public void Rejects_invalid_form_configuration(string value) =>
        Assert.Throws<MeetingsRequestException>(() => validator.ValidateConfiguration("FORMULARIO", Json(value)));

    [Theory]
    [InlineData("TEXTO")]
    [InlineData("LISTA")]
    [InlineData("TABLA")]
    [InlineData("PARTICIPANTES")]
    public void Undefined_contracts_allow_placeholder_not_arbitrary_configuration(string type)
    {
        validator.ValidateConfiguration(type, null);
        Assert.Throws<MeetingsRequestException>(() => validator.ValidateConfiguration(type, Json("{}")));
        Assert.Throws<MeetingsRequestException>(() => validator.ValidateContent(type, null, Json("{}"), false));
    }

    [Fact]
    public void Supports_number_boolean_and_optional_values()
    {
        var config = Json("""{"fields":[{"key":"n","label":"N","type":"number","required":true},{"key":"b","label":"B","type":"boolean","required":true},{"key":"s","label":"S","type":"text","required":false}]}""");
        validator.ValidateContent("FORMULARIO", config, Json("""{"n":1.5,"b":false}"""), true);
        Assert.Throws<MeetingsRequestException>(() => validator.ValidateContent("FORMULARIO", config, Json("""{"n":"1.5","b":false}"""), true));
    }
}

