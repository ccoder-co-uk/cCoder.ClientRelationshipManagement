using System.Net;
using System.Net.Mail;
using ClientRelationshipManagement.Web.Configuration;

namespace ClientRelationshipManagement.Web.Services.Mail;

public sealed class SmtpMailClient(MailOptions options) : IMailClient
{
    public async Task<MailSendResult> SendAsync(MailSendRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
            return MailSendResult.Failed("SMTP host is not configured.");

        try
        {
            using MailMessage message = BuildMessage(request);
            using SmtpClient smtpClient = new(options.Host, options.Port)
            {
                EnableSsl = options.UseSsl,
            };

            if (!string.IsNullOrWhiteSpace(options.UserName))
                smtpClient.Credentials = new NetworkCredential(options.UserName, options.Password);

            cancellationToken.ThrowIfCancellationRequested();
            await smtpClient.SendMailAsync(message, cancellationToken);
            return MailSendResult.Sent();
        }
        catch (Exception ex)
        {
            return MailSendResult.Failed(ex.Message);
        }
    }

    static MailMessage BuildMessage(MailSendRequest request)
    {
        MailMessage message = new()
        {
            From = new MailAddress(request.FromEmailAddress, request.FromDisplayName),
            Subject = request.Subject ?? string.Empty,
            Body = request.IsBodyHtml
                ? request.BodyHtml ?? string.Empty
                : request.BodyText ?? request.BodyHtml ?? string.Empty,
            IsBodyHtml = request.IsBodyHtml,
        };

        AddAddresses(message.To, request.ToAddresses);
        AddAddresses(message.CC, request.CcAddresses);
        AddAddresses(message.Bcc, request.BccAddresses);
        AddAddresses(message.ReplyToList, request.ReplyToAddresses);

        return message;
    }

    static void AddAddresses(MailAddressCollection collection, string addresses)
    {
        foreach (string address in SplitAddresses(addresses))
            collection.Add(address);
    }

    static IEnumerable<string> SplitAddresses(string addresses) =>
        string.IsNullOrWhiteSpace(addresses)
            ? []
            : addresses
                .Split([';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase);
}
