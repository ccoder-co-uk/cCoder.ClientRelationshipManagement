using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using PlatformEntities = cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace ClientRelationshipManagement.Web.Services.Mail;

public interface IEmailDraftWorkflowService
{
    ValueTask<PlatformEntities.Email> SaveDraftAsync(EmailDraftUpsertCommand command, CancellationToken cancellationToken = default);
    ValueTask<PlatformEntities.Email> ApproveAsync(Guid clientId, Guid emailId, DateTimeOffset? scheduledSendTimeUtc, CancellationToken cancellationToken = default);
    ValueTask<PlatformEntities.Email> MarkSentAsync(Guid clientId, Guid emailId, CancellationToken cancellationToken = default);
    ValueTask<PlatformEntities.Email> RejectAsync(Guid clientId, Guid emailId, string reason, CancellationToken cancellationToken = default);
}

public sealed class EmailDraftUpsertCommand
{
    public Guid ClientId { get; init; }
    public Guid? EmailId { get; init; }
    public Guid? ClientMaterialId { get; init; }
    public Guid? ClientOpportunityId { get; init; }
    public Guid? ClientAccountId { get; init; }
    public DateTimeOffset? ActivityOn { get; init; }
    public ActivityDirection Direction { get; init; } = ActivityDirection.Outbound;
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string NextAction { get; init; }
    public DateTimeOffset? NextActionDueOn { get; init; }
    public string ToAddresses { get; init; }
    public string CcAddresses { get; init; }
    public string BccAddresses { get; init; }
    public DateTimeOffset? ScheduledSendTimeUtc { get; init; }
}
