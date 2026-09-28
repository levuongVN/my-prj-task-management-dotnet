using System.Net;

namespace TaskFlow.Application.Emails;

public static class NotificationEmailTemplates
{
    public static string Subject(string notificationTitle)
    {
        return $"TaskFlow: {notificationTitle}";
    }

    public static string Html(
        string fullName,
        string title,
        string message,
        string frontendUrl
    )
    {
        var name = WebUtility.HtmlEncode(fullName);
        var safeTitle = WebUtility.HtmlEncode(title);
        var safeMessage = WebUtility.HtmlEncode(message);

        return $@"
<!DOCTYPE html>
<html>
  <body style=""margin:0;padding:0;background-color:#f4f5f7;font-family:Arial,Helvetica,sans-serif;"">
    <div style=""max-width:560px;margin:0 auto;padding:32px 24px;"">
      <h2 style=""color:#1a2233;margin:0 0 24px;"">TaskFlow</h2>
      <div style=""background-color:#ffffff;border-radius:8px;padding:32px;"">
        <h3 style=""margin:0 0 16px;color:#1a2233;"">Hi {name},</h3>
        <p style=""color:#8a93a2;font-size:13px;text-transform:uppercase;letter-spacing:1px;margin:0 0 8px;"">
          {safeTitle}
        </p>
        <p style=""color:#555;line-height:1.6;margin:0 0 32px;font-size:15px;"">
          {safeMessage}
        </p>
        <div style=""text-align:center;margin:0 0 24px;"">
          <a href=""{frontendUrl}""
             style=""background-color:#2563eb;color:#ffffff;text-decoration:none;padding:12px 28px;border-radius:6px;display:inline-block;font-weight:bold;"">
             Open TaskFlow
          </a>
        </div>
      </div>
      <p style=""color:#b5bcc8;font-size:12px;text-align:center;margin:24px 0 0;"">
        &copy; TaskFlow - This is an automated email, please do not reply.
      </p>
    </div>
  </body>
</html>";
    }
}
