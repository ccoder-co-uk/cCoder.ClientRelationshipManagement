using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClientRelationshipManagement.Web.Configuration;
using Microsoft.Extensions.Options;

namespace ClientRelationshipManagement.Web.Services.Mail;

public sealed class MicrosoftGraphMailboxClient(
    IHttpClientFactory httpClientFactory,
    IOptions<MailOptions> options)
    : IMicrosoftGraphMailboxClient
{
    readonly MailOptions mailOptions = options.Value;

    public async Task<MailSendResult> SendAsync(
        MailSendRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
            return MailSendResult.Failed("Microsoft Graph mailbox configuration is incomplete.");

        if (string.IsNullOrWhiteSpace(request.ToAddresses))
            return MailSendResult.Failed("At least one recipient is required.");

        string sender = string.IsNullOrWhiteSpace(request.FromEmailAddress)
            ? mailOptions.MicrosoftGraphMailboxUser
            : request.FromEmailAddress;
        string accessToken = await GetAccessTokenAsync(cancellationToken);
        using HttpRequestMessage message = new(
            HttpMethod.Post,
            $"{GraphBaseUrl}/users/{Uri.EscapeDataString(sender)}/sendMail");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        message.Content = JsonContent.Create(new
        {
            message = new
            {
                subject = request.Subject ?? string.Empty,
                body = new
                {
                    contentType = request.IsBodyHtml ? "HTML" : "Text",
                    content = request.IsBodyHtml
                        ? request.BodyHtml ?? request.BodyText ?? string.Empty
                        : request.BodyText ?? request.BodyHtml ?? string.Empty
                },
                toRecipients = Recipients(request.ToAddresses),
                ccRecipients = Recipients(request.CcAddresses),
                bccRecipients = Recipients(request.BccAddresses),
                replyTo = Recipients(request.ReplyToAddresses)
            },
            saveToSentItems = true
        });

        using HttpResponseMessage response = await Client.SendAsync(message, cancellationToken);
        if (response.IsSuccessStatusCode)
            return MailSendResult.Sent();

        string error = await response.Content.ReadAsStringAsync(cancellationToken);
        return MailSendResult.Failed($"{(int)response.StatusCode} {response.ReasonPhrase}: {error}");
    }

    public async Task<IReadOnlyList<MailboxMessage>> ReceiveAsync(
        DateTimeOffset receivedSince,
        int maximumMessages,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
            return [];

        return await QueryMailboxAsync("inbox", "receivedDateTime", receivedSince, null, maximumMessages, cancellationToken);
    }

    public async Task<IReadOnlyList<MailboxMessage>> RetrieveSentAsync(
        DateTimeOffset sentSince,
        int maximumMessages,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured()) return [];
        return await QueryMailboxAsync("sentitems", "sentDateTime", sentSince, null, maximumMessages, cancellationToken);
    }

    public async Task<IReadOnlyList<MailboxMessage>> RetrieveSentAsync(
        DateTimeOffset sentSince,
        DateTimeOffset sentUntil,
        int maximumMessages,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured()) return [];
        return await QueryMailboxAsync("sentitems", "sentDateTime", sentSince, sentUntil, maximumMessages, cancellationToken);
    }

    async Task<IReadOnlyList<MailboxMessage>> QueryMailboxAsync(
        string folder,
        string dateProperty,
        DateTimeOffset from,
        DateTimeOffset? to,
        int maximumMessages,
        CancellationToken cancellationToken)
    {
        int remaining = Math.Clamp(maximumMessages, 1, 1000);
        string accessToken = await GetAccessTokenAsync(cancellationToken);
        string select = string.Equals(folder, "sentitems", StringComparison.OrdinalIgnoreCase)
            ? "id,internetMessageId,conversationId,subject,sentDateTime,from,toRecipients,ccRecipients"
            : "id,internetMessageId,conversationId,internetMessageHeaders,subject,body,receivedDateTime,from,toRecipients,ccRecipients";
        string filterExpression = $"{dateProperty} ge {from.UtcDateTime:O}";
        if (to.HasValue)
            filterExpression += $" and {dateProperty} le {to.Value.UtcDateTime:O}";
        string filter = Uri.EscapeDataString(filterExpression);
        string url = $"{GraphBaseUrl}/users/{Uri.EscapeDataString(mailOptions.MicrosoftGraphMailboxUser)}/mailFolders/{folder}/messages?$select={select}&$top={Math.Min(remaining, 100)}&$orderby={dateProperty} desc&$filter={filter}";
        List<MailboxMessage> results = [];

        while (!string.IsNullOrWhiteSpace(url) && remaining > 0)
        {
            using HttpResponseMessage response = await SendWithTransientRetryAsync(() =>
            {
                HttpRequestMessage request = new(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                return request;
            }, cancellationToken);
            string content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Microsoft Graph mailbox query failed: {(int)response.StatusCode} {content}");

            IReadOnlyList<MailboxMessage> page = ParseMessages(content, dateProperty);
            results.AddRange(page.Take(remaining));
            remaining -= page.Count;
            url = ReadNextLink(content);
        }

        return results;
    }

    async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        string tokenUrl = $"{mailOptions.MicrosoftGraphLoginBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(mailOptions.MicrosoftGraphTenantId)}/oauth2/v2.0/token";
        using HttpResponseMessage response = await SendWithTransientRetryAsync(() =>
            new HttpRequestMessage(HttpMethod.Post, tokenUrl)
            {
                Content = new FormUrlEncodedContent(
                [
                    new("client_id", mailOptions.MicrosoftGraphClientId),
                    new("client_secret", mailOptions.MicrosoftGraphClientSecret),
                    new("scope", "https://graph.microsoft.com/.default"),
                    new("grant_type", "client_credentials")
                ])
            }, cancellationToken);
        string content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Microsoft Graph token request failed: {(int)response.StatusCode} {content}");

        using JsonDocument document = JsonDocument.Parse(content);
        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Microsoft Graph token response did not include an access token.");
    }

    async Task<HttpResponseMessage> SendWithTransientRetryAsync(
        Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            using HttpRequestMessage request = createRequest();
            try
            {
                return await Client.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException) when (attempt < 3 && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
            }
        }
    }

    bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(mailOptions.MicrosoftGraphTenantId)
        && !string.IsNullOrWhiteSpace(mailOptions.MicrosoftGraphClientId)
        && !string.IsNullOrWhiteSpace(mailOptions.MicrosoftGraphClientSecret)
        && !string.IsNullOrWhiteSpace(mailOptions.MicrosoftGraphMailboxUser);

    HttpClient Client => httpClientFactory.CreateClient(nameof(MicrosoftGraphMailboxClient));

    string GraphBaseUrl => mailOptions.MicrosoftGraphBaseUrl.TrimEnd('/');

    static object[] Recipients(string addresses) =>
        string.IsNullOrWhiteSpace(addresses)
            ? []
            : addresses
                .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(address => new { emailAddress = new { address } })
                .ToArray();

    static IReadOnlyList<MailboxMessage> ParseMessages(string content, string dateProperty)
    {
        using JsonDocument document = JsonDocument.Parse(content);
        if (!document.RootElement.TryGetProperty("value", out JsonElement messages))
            return [];

        return messages.EnumerateArray().Select(message => new MailboxMessage
        {
            ExternalId = ReadString(message, "id"),
            InternetMessageId = ReadString(message, "internetMessageId"),
            ConversationId = ReadString(message, "conversationId"),
            InReplyTo = ReadHeader(message, "In-Reply-To"),
            References = ReadHeader(message, "References"),
            FromAddress = ReadSender(message),
            ToAddresses = ReadRecipients(message, "toRecipients"),
            CcAddresses = ReadRecipients(message, "ccRecipients"),
            Subject = ReadString(message, "subject"),
            Body = message.TryGetProperty("body", out JsonElement body) ? ReadString(body, "content") : null,
            IsBodyHtml = message.TryGetProperty("body", out body)
                && string.Equals(ReadString(body, "contentType"), "html", StringComparison.OrdinalIgnoreCase),
            ReceivedOn = DateTimeOffset.TryParse(ReadString(message, dateProperty), out DateTimeOffset receivedOn)
                ? receivedOn
                : DateTimeOffset.UtcNow
        }).ToList();
    }

    static string ReadNextLink(string content)
    {
        using JsonDocument document = JsonDocument.Parse(content);
        return ReadString(document.RootElement, "@odata.nextLink");
    }

    static string ReadSender(JsonElement message)
    {
        if (!message.TryGetProperty("from", out JsonElement from)
            || !from.TryGetProperty("emailAddress", out JsonElement address))
        {
            return null;
        }

        return ReadString(address, "address");
    }

    static string ReadRecipients(JsonElement message, string propertyName)
    {
        if (!message.TryGetProperty(propertyName, out JsonElement recipients))
            return null;

        return string.Join(", ", recipients.EnumerateArray()
            .Select(recipient => recipient.TryGetProperty("emailAddress", out JsonElement address)
                ? ReadString(address, "address")
                : null)
            .Where(address => !string.IsNullOrWhiteSpace(address)));
    }

    static string ReadHeader(JsonElement message, string headerName)
    {
        if (!message.TryGetProperty("internetMessageHeaders", out JsonElement headers)
            || headers.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement header in headers.EnumerateArray())
        {
            if (string.Equals(ReadString(header, "name"), headerName, StringComparison.OrdinalIgnoreCase))
                return ReadString(header, "value");
        }

        return null;
    }

    static string ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
