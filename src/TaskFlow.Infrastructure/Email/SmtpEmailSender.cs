using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using TaskFlow.Application.Interfaces;

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;

    public SmtpEmailSender(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        message.To.Add(new MailboxAddress(to, to));

        message.Subject = subject;

        message.Body = new BodyBuilder
        {
            HtmlBody = htmlBody
        }.ToMessageBody();

        // Port 465 = implicit TLS, các port còn lại (587 Brevo) = STARTTLS
        var secureMode = _options.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;

        using var client = new SmtpClient();

        // macOS/NAT hay để CRL/OCSP check bị treo -> toàn bộ gửi mail chết.
        // Chứng chỉ vẫn được validate đầy đủ, chỉ bỏ bước kiểm tra danh sách thu hồi.
        client.CheckCertificateRevocation = false;

        await client.ConnectAsync(_options.Host, _options.Port, secureMode);
        await client.AuthenticateAsync(_options.Username, _options.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
