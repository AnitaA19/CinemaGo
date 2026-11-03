using System.Net;
using System.Net.Mail;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Domain.Intefraces;
using Microsoft.Extensions.Configuration;

namespace CinemaGo.Infrastructure.Email;

public sealed class SmtpSessionCancellationEmailSender : ISessionCancellationEmailSender
{
    private readonly IConfiguration _configuration;

    public SmtpSessionCancellationEmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendAsync(
        IEnumerable<string> recipients,
        int sessionId,
        string movieTitle,
        DateTime startsAt,
        CancellationToken cancellationToken)
    {
        var recipientList = recipients
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipientList.Count == 0)
        {
            return;
        }

        var host = _configuration["Email:Smtp:Host"];
        var from = _configuration["Email:Smtp:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        {
            throw new NotificationDeliveryException(
                "SMTP is not configured. Set Email:Smtp:Host and Email:Smtp:From before cancelling sessions with active bookings.");
        }

        var configuredPort = _configuration["Email:Smtp:Port"];
        var port = string.IsNullOrWhiteSpace(configuredPort)
            ? 587
            : int.TryParse(configuredPort, out var parsedPort) && parsedPort is > 0 and <= 65535
                ? parsedPort
                : throw new NotificationDeliveryException("Email:Smtp:Port must be between 1 and 65535.");

        var configuredSsl = _configuration["Email:Smtp:EnableSsl"];
        var enableSsl = string.IsNullOrWhiteSpace(configuredSsl)
            ? true
            : bool.TryParse(configuredSsl, out var parsedSsl)
                ? parsedSsl
                : throw new NotificationDeliveryException("Email:Smtp:EnableSsl must be true or false.");

        var username = _configuration["Email:Smtp:Username"];
        var password = _configuration["Email:Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(username) && string.IsNullOrEmpty(password))
        {
            throw new NotificationDeliveryException(
                "SMTP password is required when Email:Smtp:Username is configured.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(from),
            Subject = $"CinemaGo screening cancelled: {movieTitle}",
            Body = EmailTemplates.SessionCancelledText(sessionId, movieTitle, startsAt),
            IsBodyHtml = false
        };
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            EmailTemplates.SessionCancelledHtml(sessionId, movieTitle, startsAt),
            System.Text.Encoding.UTF8,
            "text/html"));

        foreach (var recipient in recipientList)
        {
            message.Bcc.Add(new MailAddress(recipient));
        }

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        try
        {
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (SmtpException exception)
        {
            throw new NotificationDeliveryException(
                $"SMTP failed to deliver the session {sessionId} cancellation notification.",
                exception);
        }
    }
}
