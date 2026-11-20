using System.Net;
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Application.Services;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Meetings.Infrastructure.Persistence.Services;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Meetings.Test;

public sealed class NotificationTests
{
    private sealed class RecordingSender : IMeetingNotificationSender
    {
        public int Calls;
        public bool Fail;
        public bool Uncertain;
        public readonly List<MeetingNotification> Sent = [];
        public Func<MeetingNotification, Task>? VerifyCommit;
        public async Task<NotificationReceipt> SendAsync(MeetingNotification notification, CancellationToken ct)
        {
            Calls++; Sent.Add(notification);
            if (VerifyCommit is not null) await VerifyCommit(notification);
            await Task.Yield();
            if (Fail) throw new NotificationFailure("Fallo SMTP de prueba.", Uncertain);
            return new(false); // Fake transport acceptance; no socket or external service is used.
        }
    }
    [Theory]
    [InlineData("PRESENCIAL")]
    [InlineData("VIRTUAL")]
    public async Task Notifications_use_only_explicit_unique_participants_after_commit(string modality)
    {
        var sender = new RecordingSender();
        using var factory = WithSender(sender); using var client = factory.Client();
        var input = (await ActaApiTests.PrepareAsync(factory, client)) with { Meeting = ActaApiTests.Meeting(modality),
            Participants = [new("Organizador repetido", "ORGANIZER@EXAMPLE.INVALID"), new("Invitado", "invitee@example.invalid")] };
        sender.VerifyCommit = async notification =>
        {
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
            Assert.Null(db.Database.CurrentTransaction);
            Assert.True(await db.Set<Acta>().AnyAsync()); Assert.True(await db.Set<ScheduledMeeting>().AnyAsync(m => m.MeetingId == notification.MeetingId));
            Assert.Equal("PROCESANDO", (await db.Set<MeetingNotification>().AsNoTracking().SingleAsync(n => n.MeetingNotificationId == notification.MeetingNotificationId)).Status);
        };
        var key = Guid.NewGuid().ToString(); using var response = await ActaApiTests.SendAsync(client, input, key);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadResultAsync<CreatedActaDto>())!;
        Assert.Equal("ENVIADO", result.Notifications!.State); Assert.True(result.Notifications.SmtpAccepted); Assert.False(result.Notifications.Simulated);
        Assert.Equal(2, sender.Calls); Assert.Equal(2, sender.Sent.Select(n => n.RecipientEmail).Distinct().Count());
        Assert.Contains(sender.Sent, n => n.RecipientEmail == "organizer@example.invalid");
        Assert.All(sender.Sent, n => { Assert.Contains("2026-12-01 10:00", n.TextBody); Assert.Contains(modality, n.TextBody); Assert.Contains(result.ActaCode, n.TextBody); });
        await ActaApiTests.AssertFailure(await ActaApiTests.SendAsync(client, input, key), HttpStatusCode.Conflict);
        using var scope2 = factory.Services.CreateScope(); var service = scope2.ServiceProvider.GetRequiredService<IMeetingNotificationService>();
        foreach (var item in result.Notifications.Items) Assert.Equal("ENVIADO", (await service.RetryAsync(item.NotificationId, default)).State);
        Assert.Equal(2, sender.Calls);
        var db2 = scope2.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        Assert.All(await db2.Set<MeetingNotification>().ToArrayAsync(), n => { Assert.NotNull(n.AcceptedAt); Assert.Equal(1, n.Attempts); });
        Assert.Empty(await db2.Set<MeetingCalendar>().ToArrayAsync());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_or_uncertain_SMTP_keeps_acta_and_controls_retries(bool uncertain)
    {
        var sender = new RecordingSender { Fail = true, Uncertain = uncertain };
        using var factory = WithSender(sender); using var client = factory.Client();
        var input = (await ActaApiTests.PrepareAsync(factory, client)) with { Meeting = ActaApiTests.Meeting("VIRTUAL") };
        using var response = await ActaApiTests.SendAsync(client, input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadResultAsync<CreatedActaDto>())!;
        Assert.Equal(uncertain ? "INCIERTO" : "ERROR", result.Notifications!.State); Assert.False(result.Notifications.SmtpAccepted);
        var id = Assert.Single(result.Notifications.Items).NotificationId;
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        Assert.Single(await db.Set<Acta>().ToArrayAsync()); Assert.Single(await db.Set<ScheduledMeeting>().ToArrayAsync());
        sender.Fail = false;
        var service = scope.ServiceProvider.GetRequiredService<IMeetingNotificationService>();
        using var secondScope = factory.Services.CreateScope(); var second = secondScope.ServiceProvider.GetRequiredService<IMeetingNotificationService>();
        await Task.WhenAll(service.RetryAsync(id, default), second.RetryAsync(id, default));
        var completed = await service.RetryAsync(id, default);
        Assert.Equal(uncertain ? "INCIERTO" : "ENVIADO", completed.State);
        Assert.Equal(uncertain ? 1 : 2, sender.Calls);
        Assert.Equal(uncertain ? 1 : 2, completed.Attempts);
    }
    [Fact]
    public async Task Interrupted_processing_and_simulated_notifications_are_never_automatically_resent()
    {
        var sender = new RecordingSender { Fail = true };
        using var factory = WithSender(sender); using var client = factory.Client();
        var input = (await ActaApiTests.PrepareAsync(factory, client)) with { Meeting = ActaApiTests.Meeting("VIRTUAL") };
        using var response = await ActaApiTests.SendAsync(client, input);
        var id = (await response.Content.ReadResultAsync<CreatedActaDto>())!.Notifications!.Items.Single().NotificationId;
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IMeetingNotificationService>();
        foreach (var status in new[] { "PROCESANDO", "SIMULADO" })
        {
            await db.Set<MeetingNotification>().Where(n => n.MeetingNotificationId == id).ExecuteUpdateAsync(s => s.SetProperty(n => n.Status, status));
            Assert.Equal(status, (await service.RetryAsync(id, default)).State);
        }
        Assert.Equal(1, sender.Calls);
    }
    [Fact]
    public async Task Without_meeting_notifies_only_invited_participants()
    {
        var sender = new RecordingSender(); using var factory = WithSender(sender); using var client = factory.Client();
        var input = await ActaApiTests.PrepareAsync(factory, client);
        using var response = await ActaApiTests.SendAsync(client, input);
        var result = (await response.Content.ReadResultAsync<CreatedActaDto>())!;
        Assert.Equal("ENVIADO", result.Notifications!.State); Assert.Equal(1, sender.Calls);
        var sent = Assert.Single(sender.Sent);
        Assert.Null(sent.MeetingId); Assert.Equal(result.ActaId, sent.ActaId);
        Assert.Equal("person@example.invalid", sent.RecipientEmail);
        Assert.Contains("Se ha creado un acta", sent.TextBody); Assert.Contains("2026-11-11", sent.TextBody);
    }
    [Fact]
    public void Composer_validates_recipients_encodes_HTML_and_builds_multipart_email()
    {
        var acta = new Acta { ActaCode = "ACT-test", Title = "<script>alert('x')</script>" };
        var meeting = new ScheduledMeeting { Modality = "PRESENCIAL", LocationDescription = "Sala & café", TimeZone = "America/Bogota",
            StartAtUtc = new(2026, 12, 1, 15, 0, 0), EndAtUtc = new(2026, 12, 1, 16, 0, 0) };
        var drafts = new MeetingNotificationComposer().Compose(acta, meeting,
            [new() { Email = "USER@example.invalid", UserName = "Duplicado" }, new() { Email = "bad-address", UserName = "Inválido" }],
            new(1, [], [], "Organizador", "user@example.invalid"));
        var draft = Assert.Single(drafts); Assert.Equal("user@example.invalid", draft.RecipientEmail);
        Assert.Contains("<script>", draft.TextBody); Assert.DoesNotContain("<script>", draft.HtmlBody); Assert.Contains("&lt;script&gt;", draft.HtmlBody);
        Assert.Contains("Sala & café", draft.TextBody); Assert.Contains("10:00", draft.TextBody); Assert.Contains("11:00", draft.TextBody);
        using var mime = SmtpNotificationSender.BuildMessage(new SmtpOptions(), draft);
        var plainText = mime.TextBody; Assert.NotNull(plainText);
        Assert.Equal(draft.TextBody, plainText.Replace("\r\n", "\n")); Assert.Equal(draft.HtmlBody, mime.HtmlBody); Assert.Equal(draft.MessageId, mime.MessageId);
        Assert.Single(mime.To); Assert.Empty(mime.Cc); Assert.Empty(mime.Bcc); Assert.Empty(mime.ReplyTo);
    }
    [Fact]
    public async Task Disabled_SMTP_needs_no_credentials_and_cannot_open_a_connection()
    {
        var options = new SmtpOptions { Enabled = false, Host = "not-a-real-smtp-host.invalid", Password = "" };
        var receipt = await new SmtpNotificationSender(options).SendAsync(new MeetingNotification(), default);
        Assert.True(receipt.Simulated);
    }
    [Theory]
    [InlineData("tls")]
    [InlineData("port")]
    [InlineData("timeout")]
    [InlineData("password")]
    [InlineData("from")]
    [InlineData("reply")]
    public async Task Invalid_SMTP_configuration_fails_before_network_access(string fault)
    {
        var options = new SmtpOptions { Enabled = true, Password = "test-placeholder-only" };
        switch (fault)
        {
            case "tls": options.UseStartTls = false; break;
            case "port": options.Port = 465; break;
            case "timeout": options.TimeoutSeconds = 0; break;
            case "password": options.Password = ""; break;
            case "from": options.FromEmail = "invalid"; break;
            case "reply": options.ReplyTo = "invalid"; break;
        }
        Assert.Equal(SecureSocketOptions.StartTls, SmtpNotificationSender.Security);
        var error = await Assert.ThrowsAsync<NotificationFailure>(() => new SmtpNotificationSender(options).SendAsync(new(), default));
        Assert.False(error.Uncertain); Assert.DoesNotContain("test-placeholder", error.Message);
    }
    [Fact]
    public async Task Actas_read_permission_does_not_allow_creation()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        var input = await ActaApiTests.PrepareAsync(factory, client, false);
        await TemplateManagementTests.SetPermissions(factory, MeetingsPermissions.ActasRead);
        await ActaApiTests.AssertFailure(await ActaApiTests.SendAsync(client, input), HttpStatusCode.Forbidden);
        await TemplateManagementTests.SetPermissions(factory, MeetingsPermissions.ActasManage, MeetingsPermissions.GlobalRead);
        using var created = await ActaApiTests.SendAsync(client, input);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }
    private static MeetingsApiFactory WithSender(RecordingSender sender) => new() { Customize = services =>
    {
        services.RemoveAll<IMeetingNotificationSender>(); services.AddSingleton<IMeetingNotificationSender>(sender);
    } };
}
