using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClientRelationshipManagement.Web.Configuration;

namespace ClientRelationshipManagement.Web.Services.Mail;

public sealed class SendGridMailClient(
    HttpClient httpClient,
    MailOptions options)
    : IMailClient
{
    public async Task<MailSendResult> SendAsync(MailSendRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            return MailSendResult.Failed("SendGrid API key is not configured.");

        if (string.IsNullOrWhiteSpace(request.FromEmailAddress))
            return MailSendResult.Failed("A from email address is required.");

        if (string.IsNullOrWhiteSpace(request.ToAddresses))
            return MailSendResult.Failed("At least one recipient is required.");

        using HttpRequestMessage message = new(HttpMethod.Post, "mail/send")
        {
            Content = JsonContent.Create(BuildPayload(request))
        };

        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            string externalMessageId = response.Headers.TryGetValues("X-Message-Id", out IEnumerable<string> values)
                ? values.FirstOrDefault()
                : null;

            return MailSendResult.Sent(externalMessageId);
        }

        string error = await response.Content.ReadAsStringAsync(cancellationToken);
        return MailSendResult.Failed($"{(int)response.StatusCode} {response.ReasonPhrase}: {error}");
    }

    static SendGridMailPayload BuildPayload(MailSendRequest request)
    {
        List<SendGridPersonalization> personalizations =
        [
            new()
            {
                To = BuildAddresses(request.ToAddresses),
                Cc = BuildAddressesOrNull(request.CcAddresses),
                Bcc = BuildAddressesOrNull(request.BccAddresses)
            }
        ];

        List<SendGridContent> contents =
        [
            new()
            {
                Type = request.IsBodyHtml ? "text/html" : "text/plain",
                Value = request.IsBodyHtml
                    ? request.BodyHtml ?? request.BodyText ?? string.Empty
                    : request.BodyText ?? request.BodyHtml ?? string.Empty
            }
        ];

        return new SendGridMailPayload
        {
            Personalizations = personalizations,
            From = new SendGridEmailAddress
            {
                Email = request.FromEmailAddress,
                Name = request.FromDisplayName
            },
            ReplyToList = BuildAddressesOrNull(request.ReplyToAddresses),
            Subject = request.Subject ?? string.Empty,
            Content = contents
        };
    }

    static List<SendGridEmailAddress> BuildAddressesOrNull(string addresses)
    {
        List<SendGridEmailAddress> resolvedAddresses = BuildAddresses(addresses);
        return resolvedAddresses.Count == 0 ? null : resolvedAddresses;
    }

    static List<SendGridEmailAddress> BuildAddresses(string addresses) =>
        string.IsNullOrWhiteSpace(addresses)
            ? []
            : addresses
                .Split([';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(address => new SendGridEmailAddress { Email = address })
                .ToList();

    sealed class SendGridMailPayload
    {
        [JsonPropertyName("personalizations")]
        public List<SendGridPersonalization> Personalizations { get; init; } = [];

        [JsonPropertyName("from")]
        public SendGridEmailAddress From { get; init; } = new();

        [JsonPropertyName("reply_to_list")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SendGridEmailAddress> ReplyToList { get; init; }

        [JsonPropertyName("subject")]
        public string Subject { get; init; } = string.Empty;

        [JsonPropertyName("content")]
        public List<SendGridContent> Content { get; init; } = [];
    }

    sealed class SendGridPersonalization
    {
        [JsonPropertyName("to")]
        public List<SendGridEmailAddress> To { get; init; } = [];

        [JsonPropertyName("cc")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SendGridEmailAddress> Cc { get; init; }

        [JsonPropertyName("bcc")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SendGridEmailAddress> Bcc { get; init; }
    }

    sealed class SendGridEmailAddress
    {
        [JsonPropertyName("email")]
        public string Email { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Name { get; init; }
    }

    sealed class SendGridContent
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; init; } = string.Empty;
    }
}
