using System.Net;

namespace TaskFlow.Application.Emails;

public static class AuthEmailTemplates
{
    public static string ForgotPasswordSubject()
    {
        return "Reset your TaskFlow password";
    }

    public static string ForgotPasswordHtml(
        string fullName,
        string resetLink,
        int expiryMinutes
    )
    {
        var name = WebUtility.HtmlEncode(fullName);

        return $@"
<!DOCTYPE html>
<html>
  <body style=""margin:0;padding:0;background-color:#f4f5f7;font-family:Arial,Helvetica,sans-serif;"">
    <div style=""max-width:560px;margin:0 auto;padding:32px 24px;"">
      <h2 style=""color:#1a2233;margin:0 0 24px;"">TaskFlow</h2>
      <div style=""background-color:#ffffff;border-radius:8px;padding:32px;"">
        <h3 style=""margin:0 0 16px;color:#1a2233;"">Hi {name},</h3>
        <p style=""color:#555;line-height:1.6;margin:0 0 16px;"">
          We received a request to reset your password. Click the button below to choose a new one.
          This link will expire in <strong>{expiryMinutes} minutes</strong>.
        </p>
        <div style=""text-align:center;margin:32px 0;"">
          <a href=""{resetLink}""
             style=""background-color:#2563eb;color:#ffffff;text-decoration:none;padding:12px 28px;border-radius:6px;display:inline-block;font-weight:bold;"">
             Reset Password
          </a>
        </div>
        <p style=""color:#8a93a2;font-size:12px;line-height:1.6;margin:0;"">
          If the button does not work, paste this link into your browser:<br/>
          <span style=""word-break:break-all;"">{resetLink}</span>
        </p>
        <p style=""color:#8a93a2;font-size:12px;line-height:1.6;margin:24px 0 0;"">
          If you didn't request this, you can safely ignore this email. Your password won't change until
          you create a new one.
        </p>
      </div>
      <p style=""color:#b5bcc8;font-size:12px;text-align:center;margin:24px 0 0;"">
        &copy; TaskFlow - This is an automated email, please do not reply.
      </p>
    </div>
  </body>
</html>";
    }
}
