using System.Net;

namespace CinemaGo.Infrastructure.Email;

// Inline-styled HTML (email clients ignore external CSS). Colors mirror the CinemaGo frontend dark/gold theme.
public static class EmailTemplates
{
    private const string Background = "#1b1814";
    private const string Card = "#27221d";
    private const string Border = "#3a342c";
    private const string Foreground = "#f6f1e4";
    private const string Muted = "#b9b1a0";
    private const string Gold = "#e6c36a";
    private const string Danger = "#e5735a";

    public static string SessionCancelledHtml(int sessionId, string movieTitle, DateTime startsAt)
    {
        var title = WebUtility.HtmlEncode(movieTitle);
        var when = WebUtility.HtmlEncode(startsAt.ToString("dddd, d MMMM yyyy 'at' HH:mm") + " UTC");

        return $"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="color-scheme" content="dark">
<title>Screening cancelled</title>
</head>
<body style="margin:0;padding:0;background-color:{Background};">
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:{Background};padding:32px 12px;">
<tr><td align="center">
<table role="presentation" width="560" cellpadding="0" cellspacing="0" style="max-width:560px;width:100%;background-color:{Card};border:1px solid {Border};border-radius:14px;font-family:'Segoe UI',Helvetica,Arial,sans-serif;color:{Foreground};">
<tr><td style="padding:28px 32px 8px 32px;font-family:Georgia,'Times New Roman',serif;font-size:26px;font-weight:bold;color:{Gold};letter-spacing:0.5px;">
CinemaGo
</td></tr>
<tr><td style="padding:0 32px;"><div style="height:1px;background-color:{Border};line-height:1px;font-size:1px;">&nbsp;</div></td></tr>
<tr><td style="padding:24px 32px 0 32px;">
<span style="display:inline-block;padding:4px 12px;border:1px solid {Danger};border-radius:999px;font-size:12px;font-weight:bold;letter-spacing:1px;color:{Danger};">SCREENING CANCELLED</span>
<h1 style="margin:16px 0 8px 0;font-family:Georgia,'Times New Roman',serif;font-size:24px;line-height:1.3;color:{Foreground};">{title}</h1>
<p style="margin:0;font-size:15px;line-height:1.6;color:{Muted};">We're sorry, but the screening you booked has been cancelled.</p>
</td></tr>
<tr><td style="padding:20px 32px 0 32px;">
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:{Background};border:1px solid {Border};border-radius:10px;">
<tr><td style="padding:14px 18px;font-size:13px;color:{Muted};">Scheduled for</td><td align="right" style="padding:14px 18px;font-size:14px;font-weight:bold;color:{Foreground};">{when}</td></tr>
<tr><td style="padding:0 18px 14px 18px;font-size:13px;color:{Muted};">Session</td><td align="right" style="padding:0 18px 14px 18px;font-size:14px;font-weight:bold;color:{Gold};">#{sessionId}</td></tr>
</table>
</td></tr>
<tr><td style="padding:20px 32px 28px 32px;font-size:15px;line-height:1.6;color:{Muted};">
Your booking for this session has also been cancelled. Please contact the cinema if you need assistance, or pick another session on CinemaGo.
</td></tr>
</table>
<p style="margin:16px 0 0 0;font-family:'Segoe UI',Helvetica,Arial,sans-serif;font-size:12px;color:{Muted};">&copy; CinemaGo &middot; Choose a film, pick a seat.</p>
</td></tr>
</table>
</body>
</html>
""";
    }

    public static string SessionCancelledText(int sessionId, string movieTitle, DateTime startsAt) =>
        $"The screening of {movieTitle} (session {sessionId}) scheduled for {startsAt:O} has been cancelled. " +
        "Your active booking has also been cancelled. Please contact the cinema if you need assistance.";
}
