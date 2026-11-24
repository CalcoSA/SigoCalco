using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Domain.Exceptions;
using Meetings.Domain.Models.Actas;

namespace Meetings.Application.Services;

public sealed partial class ActaApplication
{
    private async Task<MeetingsIdentity> RequireActorAsync(CancellationToken ct)
    {
        var actor = await identity.GetAsync(ct);
        if (!MeetingsPermissions.Allowed(await templates.GetPermissionsAsync(ct), actor, MeetingsPermissions.ActasManage))
            throw MeetingsRequestException.Forbidden();
        return actor;
    }
    public async Task<PagedActasDto> GetAsync(ActaQuery query, CancellationToken ct)
    {
        var actor = await RequireActorAsync(ct);
        ValidatePage(query);
        return await store.GetOwnedAsync(actor.UserId, query, ct);
    }
    private static void ValidatePage(ActaQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue)
            throw MeetingsRequestException.Invalid("Paginación inválida: page >= 1 y pageSize entre 1 y 100.");
    }
    private async Task<MeetingsIdentity> RequireReaderAsync(CancellationToken ct)
    {
        var actor = await identity.GetAsync(ct);
        if (ParticipantIdentity.NormalizeLogin(actor.UserLogin) is null)
            throw new MeetingsRequestException(401, "La identidad autenticada no contiene un userLogin válido.");
        return actor;
    }
    public async Task<PagedActasDto> GetSharedAsync(ActaQuery query, CancellationToken ct)
    {
        var actor = await RequireReaderAsync(ct);
        ValidatePage(query);
        return await store.GetSharedAsync(actor.UserId, actor.UserLogin!, query, ct);
    }
    public async Task<ActaDetailDto> GetByIdAsync(int id, CancellationToken ct)
    {
        var actor = await RequireReaderAsync(ct);
        var canReadOwned = MeetingsPermissions.Allowed(await templates.GetPermissionsAsync(ct), actor, MeetingsPermissions.ActasManage);
        return await store.GetReadableDetailAsync(id, actor.UserId, actor.UserLogin!, canReadOwned, ct);
    }
    public async Task<ActaDetailDto> UpdateAsync(int id, UpdateActaDto input, CancellationToken ct)
    {
        var actor = await RequireActorAsync(ct);
        var current = await store.GetOwnedDetailAsync(id, actor.UserId, ct);
        ActaLifecycle.RequireDraft(current.Acta.Status);
        if (input.DocumentDate == default) throw MeetingsRequestException.Invalid("documentDate es obligatorio.");
        var source = await store.FindVersionAsync(current.TemplateVersionId, ct) ?? throw MeetingsRequestException.NotFound("No se encontró la versión original.");
        var sections = ValidateSections(source, input.Sections);
        await store.UpdateOwnedAsync(id, actor.UserId, Text(input.Title, 250, "title"), input.DocumentDate, sections, ct);
        return await store.GetOwnedDetailAsync(id, actor.UserId, ct);
    }
    public async Task<ActaDetailDto> UpdateParticipantsAsync(int id, UpdateActaParticipantsDto input, CancellationToken ct)
    {
        var actor = await RequireActorAsync(ct);
        var current = await store.GetOwnedDetailAsync(id, actor.UserId, ct);
        ActaLifecycle.RequireDraft(current.Acta.Status);
        await store.ReplaceParticipantsAsync(id, actor.UserId, ValidateParticipants(input.Participants), ct);
        return await store.GetOwnedDetailAsync(id, actor.UserId, ct);
    }
    public async Task<ActaDetailDto> UpdateAttendanceAsync(int id, int participantId, UpdateAttendanceDto input, CancellationToken ct)
    {
        var actor = await RequireActorAsync(ct);
        await store.UpdateAttendanceAsync(id, actor.UserId, participantId, input.Attended, ct);
        return await store.GetOwnedDetailAsync(id, actor.UserId, ct);
    }
    public async Task<ActaDetailDto> ChangeStatusAsync(int id, UpdateActaStatusDto input, CancellationToken ct)
    {
        var actor = await RequireActorAsync(ct);
        var status = input.Status?.Trim().ToUpperInvariant();
        if (status is null || !ActaLifecycle.States.Contains(status)) throw MeetingsRequestException.Invalid("status debe ser BORRADOR, PUBLICADA o ANULADA.");
        await store.ChangeStatusAsync(id, actor.UserId, status, ct);
        return await store.GetOwnedDetailAsync(id, actor.UserId, ct);
    }
    public async Task<PagedAvailableTemplatesDto> GetAvailableTemplatesAsync(TemplateQuery query, CancellationToken ct)
    {
        await RequireActorAsync(ct);
        var page = await templateApplication.GetAsync(query with { LatestVersionOnly = true }, ct);
        return new(page.Items.Select(t =>
        {
            var version = t.Versions.SingleOrDefault();
            return new AvailableTemplateDto(t.TemplateId, t.Name, t.ActaTypeId, t.Scope, t.SolutionCenterCode,
                version?.TemplateVersionId, version?.VersionNumber, version?.Sections ?? [], version is not null);
        }).ToArray(), page.Page, page.PageSize, page.TotalCount, page.TotalPages);
    }
}
