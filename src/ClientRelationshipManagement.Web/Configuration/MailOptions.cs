namespace ClientRelationshipManagement.Web.Configuration;

public sealed class MailOptions
{
    public bool EmailSendingEnabled { get; set; }
    public string Provider { get; set; } = "SendGrid";
    public string Host { get; set; }
    public int Port { get; set; } = 587;
    public string UserName { get; set; }
    public string Password { get; set; }
    public bool UseSsl { get; set; } = true;
    public string ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.sendgrid.com/v3";
    public string FallbackFromAddress { get; set; }
    public string SafeRecipientOverrideAddress { get; set; }
    public int RetryLimit { get; set; } = 3;
    public int PollIntervalSeconds { get; set; } = 60;
    public int BatchSize { get; set; } = 10;
}
