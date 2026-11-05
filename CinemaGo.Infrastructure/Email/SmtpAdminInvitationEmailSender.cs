using System.Net;
using System.Net.Mail;
using CinemaGo.Domain.Exceptions;
using CinemaGo.Domain.Intefraces;
using Microsoft.Extensions.Configuration;

namespace CinemaGo.Infrastructure.Email;

public sealed class SmtpAdminInvitationEmailSender : IAdminInvitationEmailSender
{
    private readonly IConfiguration _configuration;

    public SmtpAdminInvitationEmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendAsync(
        string recipient,
        string code,
        string acceptLink,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var host = _configuration["Email:Smtp:Host"];
        var from = _configuration["Email:Smtp:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        {
            throw new NotificationDeliveryException(
                "SMTP is not configured. Set Email:Smtp:Host and Email:Smtp:From before inviting administrators.");
        }

        var configuredPort = _configuration["Email:Smtp:Port"];
        var port = string.IsNullOrWhiteSpace(configuredPort)
            ? 587
            : int.TryParse(configuredPort, out var parsedPort) && parsedPort is > 0 and <= 65535
                ? parsedPort
                : throw new NotificationDeliveryException("Email:Smtp:Port must be between 1 and 65535.");

        var configuredSsl = _configuration["Email:Smtp:EnableSsl"];
        var enableSsl = string.IsNullOrWhiteSpace(configuredSsl)
            || (bool.TryParse(configuredSsl, out var parsedSsl)
                ? parsedSsl
                : throw new NotificationDeliveryException("Email:Smtp:EnableSsl must be true or false."));

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
            Subject = "CinemaGo administrator invitation",
            Body = EmailTemplates.AdminInvitationText(code, acceptLink, expiresAtUtc),
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(recipient));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            EmailTemplates.AdminInvitationHtml(code, acceptLink, expiresAtUtc),
            System.Text.Encoding.UTF8,
            "text/html"));

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
                "SMTP failed to deliver the administrator invitation.",
                exception);
        }
    }
}
