using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace ClientRelationshipManagement.Web.Services.Agents;

public static class AgentConversationTurnPolicy
{
    public static bool IsAgentTurn(AgentMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        AgentMessageEntry latestEntry = message.Entries
            .OrderByDescending(entry => entry.CreatedOn)
            .ThenByDescending(entry => entry.Id)
            .FirstOrDefault();

        if (latestEntry is null)
            return false;

        if (string.Equals(latestEntry.Role, "Agent", StringComparison.OrdinalIgnoreCase))
            return RequiresProposalRecovery(message, latestEntry.Body);

        if (string.Equals(latestEntry.Role, "User", StringComparison.OrdinalIgnoreCase))
            return true;

        return message.Kind != AgentMessageKind.ApprovalRequest;
    }

    static bool RequiresProposalRecovery(AgentMessage message, string latestAgentReply)
    {
        if (message.State != AgentMessageState.Pending
            || !message.EmailId.HasValue
            || message.ProposedProcessDefinitionId.HasValue)
            return false;

        string evidence = string.Join("\n", message.Entries.Select(entry => entry.Body));
        bool templateDefect = evidence.Contains("Lead with:", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("Avoid leading with:", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("AI instruction", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("system rules", StringComparison.OrdinalIgnoreCase);
        string[] failedOutcomeSignals = ["404", "409", "could not", "couldn't", "cannot", "unable", "failed", "limitation", "not found", "canceled", "cancelled"];
        return templateDefect && failedOutcomeSignals.Any(signal =>
            latestAgentReply.Contains(signal, StringComparison.OrdinalIgnoreCase));
    }
}
