using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Meetings.Application.Interfaces;
using Meetings.Domain.Models.Actas;
using MimeKit;

namespace Meetings.Infrastructure.Persistence.Services;

public sealed class SmtpNotificationSender(SmtpOptions options) : IMeetingNotificationSender
{
    private static bool ValidAddress(string value) => value.Length <= 254 && value.Contains('@') &&
        MailboxAddress.TryParse(value, out var address) && address.Address == value;
    public static void Validate(SmtpOptions value)
    {
        if (value.Port != 587 || !value.UseStartTls || value.TimeoutSeconds != 15 || string.IsNullOrWhiteSpace(value.Host) ||
            string.IsNullOrWhiteSpace(value.Username) || string.IsNullOrWhiteSpace(value.Password) ||
            !ValidAddress(value.FromEmail) || !string.IsNullOrEmpty(value.ReplyTo) && !ValidAddress(value.ReplyTo))
            throw new NotificationFailure("Configuración SMTP inválida: requiere credencial autorizada, remitente válido, puerto 587, STARTTLS y timeout de 15 segundos.");
    }
    public static SecureSocketOptions Security => SecureSocketOptions.StartTls;
    public static MimeMessage BuildMessage(SmtpOptions options, MeetingNotification notification)
    {
        var message = new MimeMessage { Subject = notification.Subject, MessageId = notification.MessageId };
        message.From.Add(new MailboxAddress(options.FromName, options.FromEmail));
        message.To.Add(new MailboxAddress(notification.RecipientName, notification.RecipientEmail));
        if (!string.IsNullOrWhiteSpace(options.ReplyTo)) message.ReplyTo.Add(MailboxAddress.Parse(options.ReplyTo));
        message.Body = new BodyBuilder { TextBody = notification.TextBody, HtmlBody = notification.HtmlBody }.ToMessageBody();
        return message;
    }
    public async Task<NotificationReceipt> SendAsync(MeetingNotification notification, CancellationToken ct)
    {
        if (!options.Enabled) return new(true);
        Validate(options);
        if (!ValidAddress(notification.RecipientEmail)) throw new NotificationFailure("El destinatario de la notificación no es válido.");
        var sending = false;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        using var client = new SmtpClient { Timeout = options.TimeoutSeconds * 1000 };
        try
        {
            using var message = BuildMessage(options, notification);
            await client.ConnectAsync(options.Host, options.Port, Security, timeout.Token);
            await client.AuthenticateAsync(options.Username, options.Password, timeout.Token);
            sending = true;
            var result = await client.SendAsync(message, timeout.Token);
            if (!result.AcceptedRecipients.Any() || result.RejectedRecipients.Any())
                throw new NotificationFailure("SMTP rechazó al destinatario.");
            // Disconnect failure must not turn an already accepted message into a retryable error.
            try { await client.DisconnectAsync(true, timeout.Token); } catch { }
            return new(false);
        }
        catch (NotificationFailure) { throw; }
        catch (SmtpCommandException) { throw new NotificationFailure("SMTP rechazó la operación. Revise la configuración o el destinatario antes de reintentar."); }
        catch
        {
            throw new NotificationFailure(sending
                ? "Respuesta SMTP incierta durante el envío. Verifique la aceptación antes de autorizar un reenvío."
                : "No se pudo conectar o autenticar con SMTP. Revise la configuración local y la disponibilidad.", sending);
        }
    }
}
