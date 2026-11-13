using System.Text.RegularExpressions;
using Meetings.Domain.Models.Actas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meetings.Infrastructure.Persistence.Mappings;

// Shared by the runtime model and the SQL compatibility test.
public static class ActasModel
{
    public static void Configure(ModelBuilder model)
    {
        var type = Table<ActaType>(model, "acta_type", nameof(ActaType.ActaTypeId));
        Text(type, nameof(ActaType.Code), 32);
        Text(type, nameof(ActaType.Name), 150);
        type.HasIndex(x => x.Code).IsUnique();
        type.HasIndex(x => x.Name).IsUnique();

        var template = Table<Template>(model, "template", nameof(Template.TemplateId));
        Text(template, nameof(Template.Name), 200);
        Text(template, nameof(Template.SolutionCenterCode), 64);
        template.Property(x => x.Scope).HasColumnType("enum('GLOBAL','CENTRO')");
        template.HasAlternateKey(x => new { x.TemplateId, x.ActaTypeId });
        template.HasIndex(x => new { x.ActaTypeId, x.Scope, x.SolutionCenterCode });
        template.HasOne(x => x.ActaType).WithMany().HasForeignKey(x => x.ActaTypeId).OnDelete(DeleteBehavior.Restrict);
        template.ToTable(t => t.HasCheckConstraint("chk_meetings_template_scope",
            "(scope = 'GLOBAL' AND solution_center_code IS NULL) OR (scope = 'CENTRO' AND solution_center_code IS NOT NULL AND CHAR_LENGTH(TRIM(solution_center_code)) > 0)"));

        var version = Table<TemplateVersion>(model, "template_version", nameof(TemplateVersion.TemplateVersionId));
        version.HasIndex(x => new { x.TemplateId, x.VersionNumber }).IsUnique();
        version.HasAlternateKey(x => new { x.TemplateVersionId, x.ActaTypeId });
        version.HasOne<Template>().WithMany(x => x.Versions)
            .HasForeignKey(x => new { x.TemplateId, x.ActaTypeId })
            .HasPrincipalKey(x => new { x.TemplateId, x.ActaTypeId }).OnDelete(DeleteBehavior.Restrict);
        version.ToTable(t => t.HasCheckConstraint("chk_meetings_version_number", "version_number > 0"));

        var section = Table<TemplateSection>(model, "template_section", nameof(TemplateSection.TemplateSectionId));
        Text(section, nameof(TemplateSection.Title), 200);
        section.Property(x => x.ContentType).HasColumnType("enum('TEXTO','LISTA','TABLA','FORMULARIO','PARTICIPANTES')");
        section.Property(x => x.ConfigurationJson).HasColumnType("json");
        section.HasIndex(x => new { x.TemplateVersionId, x.SortOrder }).IsUnique();
        section.HasAlternateKey(x => new { x.TemplateSectionId, x.TemplateVersionId });
        section.HasOne<TemplateVersion>().WithMany(x => x.Sections).HasForeignKey(x => x.TemplateVersionId).OnDelete(DeleteBehavior.Restrict);
        section.ToTable(t => t.HasCheckConstraint("chk_meetings_section_order", "sort_order > 0"));

        var actaStatus = Table<ActaStatus>(model, "acta_status", nameof(ActaStatus.ActaStatusId));
        Text(actaStatus, nameof(ActaStatus.Name), 50);
        actaStatus.HasIndex(x => x.Name).IsUnique();
        var acta = Table<Acta>(model, "acta", nameof(Acta.ActaId));
        Text(acta, nameof(Acta.ActaCode), 50);
        Text(acta, nameof(Acta.Title), 250);
        Text(acta, nameof(Acta.SolutionCenterCode), 64);
        acta.HasIndex(x => x.ActaCode).IsUnique();
        acta.HasIndex(x => new { x.SolutionCenterCode, x.CreatedByUser, x.DocumentDate });
        acta.HasAlternateKey(x => new { x.ActaId, x.TemplateVersionId });
        acta.HasOne<ActaType>().WithMany().HasForeignKey(x => x.ActaTypeId).OnDelete(DeleteBehavior.Restrict);
        acta.HasOne<TemplateVersion>().WithMany().HasForeignKey(x => new { x.TemplateVersionId, x.ActaTypeId })
            .HasPrincipalKey(x => new { x.TemplateVersionId, x.ActaTypeId }).OnDelete(DeleteBehavior.Restrict);
        acta.HasOne<ActaStatus>().WithMany().HasForeignKey(x => x.ActaStatusId).OnDelete(DeleteBehavior.Restrict);

        var content = Table<ActaSection>(model, "acta_section", nameof(ActaSection.ActaSectionId));
        content.Property(x => x.ContentJson).HasColumnType("json");
        content.HasIndex(x => new { x.ActaId, x.TemplateSectionId }).IsUnique();
        content.HasOne<Acta>().WithMany().HasForeignKey(x => new { x.ActaId, x.TemplateVersionId })
            .HasPrincipalKey(x => new { x.ActaId, x.TemplateVersionId }).OnDelete(DeleteBehavior.Restrict);
        content.HasOne<TemplateSection>().WithMany().HasForeignKey(x => new { x.TemplateSectionId, x.TemplateVersionId })
            .HasPrincipalKey(x => new { x.TemplateSectionId, x.TemplateVersionId }).OnDelete(DeleteBehavior.Restrict);

        var participant = Table<Participant>(model, "participant", nameof(Participant.ParticipantId));
        Text(participant, nameof(Participant.UserLogin), 60);
        Text(participant, nameof(Participant.UserName), 250);
        participant.Property(x => x.UserLogin).HasColumnName("userLogin");
        participant.Property(x => x.UserName).HasColumnName("userName");
        Text(participant, nameof(Participant.Email), 254);
        participant.Property(x => x.ParticipantType).HasColumnType("enum('INTERNO','EXTERNO')");
        participant.HasIndex(x => x.Email).IsUnique();
        var ap = Table<ActaParticipant>(model, "acta_participant", nameof(ActaParticipant.ActaParticipantId));
        ap.HasIndex(x => new { x.ActaId, x.ParticipantId }).IsUnique();
        ap.HasOne<Acta>().WithMany().HasForeignKey(x => x.ActaId).OnDelete(DeleteBehavior.Restrict);
        ap.HasOne<Participant>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);

        var status = Table<MeetingStatus>(model, "meeting_status", nameof(MeetingStatus.MeetingStatusId));
        Text(status, nameof(MeetingStatus.Name), 50);
        status.HasIndex(x => x.Name).IsUnique();
        var meeting = Table<ScheduledMeeting>(model, "meeting", nameof(ScheduledMeeting.MeetingId));
        meeting.Property(x => x.Modality).HasColumnType("enum('PRESENCIAL','VIRTUAL','HIBRIDA')");
        Text(meeting, nameof(ScheduledMeeting.TimeZone), 60);
        meeting.Property(x => x.TimeZone).HasDefaultValue("America/Bogota");
        Text(meeting, nameof(ScheduledMeeting.LocationDescription), 250);
        meeting.HasIndex(x => x.ActaId).IsUnique();
        meeting.HasIndex(x => new { x.StartAtUtc, x.EndAtUtc });
        meeting.HasOne<Acta>().WithOne().HasForeignKey<ScheduledMeeting>(x => x.ActaId).OnDelete(DeleteBehavior.Restrict);
        meeting.HasOne<MeetingStatus>().WithMany().HasForeignKey(x => x.MeetingStatusId).OnDelete(DeleteBehavior.Restrict);
        meeting.ToTable(t => t.HasCheckConstraint("chk_meetings_meeting_dates", "end_at_utc > start_at_utc"));

        var reservation = Table<MeetingReservation>(model, "meeting_reservation", nameof(MeetingReservation.MeetingReservationId));
        Text(reservation, nameof(MeetingReservation.ExternalReservationId), 100);
        Text(reservation, nameof(MeetingReservation.ExternalRoomId), 100);
        reservation.Property(x => x.ReservationStatus).HasColumnType("enum('SOLICITADA','CONFIRMADA','RECHAZADA','CANCELADA','ERROR')").HasDefaultValue("SOLICITADA");
        reservation.HasIndex(x => x.MeetingId).IsUnique();
        reservation.HasIndex(x => x.ExternalReservationId).IsUnique();
        reservation.HasOne<ScheduledMeeting>().WithOne().HasForeignKey<MeetingReservation>(x => x.MeetingId).OnDelete(DeleteBehavior.Restrict);

        var calendar = Table<MeetingCalendar>(model, "meeting_calendar", nameof(MeetingCalendar.MeetingCalendarId));
        Text(calendar, nameof(MeetingCalendar.ExternalEventId), 512);
        Text(calendar, nameof(MeetingCalendar.OrganizerEmail), 254);
        calendar.Property(x => x.TeamsJoinUrl).HasColumnType("text");
        calendar.Property(x => x.LastError).HasColumnType("text");
        calendar.Property(x => x.SyncStatus).HasColumnType("enum('PENDIENTE','SINCRONIZADO','ERROR','CANCELADO')").HasDefaultValue("PENDIENTE");
        calendar.HasIndex(x => x.MeetingId).IsUnique();
        calendar.HasOne<ScheduledMeeting>().WithOne().HasForeignKey<MeetingCalendar>(x => x.MeetingId).OnDelete(DeleteBehavior.Restrict);

        var cp = Table<ActaCenterPermission>(model, "acta_center_permission", nameof(ActaCenterPermission.ActaCenterPermissionId));
        Text(cp, nameof(ActaCenterPermission.SolutionCenterCode), 64);
        cp.Property(x => x.Permission).HasColumnType("enum('LECTURA','EDICION')");
        cp.HasIndex(x => new { x.ActaId, x.SolutionCenterCode }).IsUnique();
        cp.HasIndex(x => new { x.SolutionCenterCode, x.RevokedAt, x.ActaId });
        cp.HasOne<Acta>().WithMany().HasForeignKey(x => x.ActaId).OnDelete(DeleteBehavior.Restrict);
        var up = Table<ActaUserPermission>(model, "acta_user_permission", nameof(ActaUserPermission.ActaUserPermissionId));
        up.Property(x => x.Permission).HasColumnType("enum('LECTURA','EDICION')");
        up.HasIndex(x => new { x.ActaId, x.UserId }).IsUnique();
        up.HasIndex(x => new { x.UserId, x.RevokedAt, x.ActaId });
        up.HasOne<Acta>().WithMany().HasForeignKey(x => x.ActaId).OnDelete(DeleteBehavior.Restrict);

        var permission = Table<MeetingsPermission>(model, "permission", nameof(MeetingsPermission.PermissionId));
        Text(permission, nameof(MeetingsPermission.PermissionKey), 120);
        Text(permission, nameof(MeetingsPermission.PermissionValue), 2000);
        permission.HasIndex(x => x.PermissionKey).IsUnique();
        var log = Table<MeetingsLog>(model, "log", nameof(MeetingsLog.LogId));
        Text(log, nameof(MeetingsLog.Action), 50);
        Text(log, nameof(MeetingsLog.Module), 100);
        Text(log, nameof(MeetingsLog.Description), 500);
        Text(log, nameof(MeetingsLog.UserName), 100);
    }

    private static EntityTypeBuilder<T> Table<T>(ModelBuilder model, string table, string key) where T : class
    {
        var builder = model.Entity<T>();
        builder.ToTable("sigocalco_meetings_" + table);
        builder.HasKey(key);
        builder.Property(key).ValueGeneratedOnAdd();
        builder.HasCharSet("utf8mb4").UseCollation("utf8mb4_unicode_ci");
        foreach (var property in typeof(T).GetProperties())
        {
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (type != typeof(string) && type != typeof(int) && type != typeof(long) && type != typeof(bool) && type != typeof(DateTime) && type != typeof(DateOnly)) continue;
            var p = builder.Property(property.Name).HasColumnName(Regex.Replace(property.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
            if (type == typeof(DateTime)) p.HasColumnType("datetime(6)");
            if (type == typeof(DateOnly)) p.HasColumnType("date");
        }
        return builder;
    }
    private static void Text<T>(EntityTypeBuilder<T> builder, string property, int length) where T : class =>
        builder.Property(property).HasMaxLength(length);
}

