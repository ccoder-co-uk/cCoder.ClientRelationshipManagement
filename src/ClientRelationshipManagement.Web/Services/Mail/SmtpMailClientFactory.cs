using ClientRelationshipManagement.Web.Configuration;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Services.Mail;

public sealed class SmtpMailClientFactory(
    IOptions<MailOptions> options,
    IHttpClientFactory httpClientFactory,
    IMicrosoftGraphMailboxClient microsoftGraphMailboxClient) : IMailClientFactory
{
    public IMailClient CreateClient()
    {
        MailOptions mailOptions = options.Value;

        if (string.Equals(mailOptions.Provider, "MicrosoftGraph", StringComparison.OrdinalIgnoreCase))
            return microsoftGraphMailboxClient;

        if (string.Equals(mailOptions.Provider, "SendGrid", StringComparison.OrdinalIgnoreCase))
        {
            HttpClient httpClient = httpClientFactory.CreateClient(nameof(SendGridMailClient));
            httpClient.BaseAddress = new Uri(mailOptions.BaseUrl.TrimEnd('/') + "/");
            return new SendGridMailClient(httpClient, mailOptions);
        }

        return new SmtpMailClient(mailOptions);
    }
}
