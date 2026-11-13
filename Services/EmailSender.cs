
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace DemoMVC.Services;

public class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;

    public EmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(
        string email,
        string subject,
        string htmlMessage)
    {
        var fromEmail = _configuration["Email:From"]
            ?? throw new InvalidOperationException("Chưa cấu hình Email:From.");

        var fromName = _configuration["Email:FromName"] ?? "DemoMVC";

        var username = _configuration["Email:UserName"]
            ?? throw new InvalidOperationException("Chưa cấu hình Email:UserName.");

        var password = _configuration["Email:AppPassword"]
            ?? throw new InvalidOperationException("Chưa cấu hình Email:AppPassword.");

        var host = _configuration["Email:SmtpHost"] ?? "smtp.gmail.com";

        var port = int.Parse(_configuration["Email:Port"] ?? "587");

        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;

        message.Body = new BodyBuilder
        {
            HtmlBody = htmlMessage
        }.ToMessageBody();

        using var smtp = new SmtpClient();

        await smtp.ConnectAsync(
            host,
            port,
            SecureSocketOptions.StartTls
        );

        await smtp.AuthenticateAsync(username, password);

        await smtp.SendAsync(message);

        await smtp.DisconnectAsync(true);
    }
}
