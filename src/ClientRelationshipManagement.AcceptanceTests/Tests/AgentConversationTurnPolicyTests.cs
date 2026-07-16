using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.Web.Services.Agents;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed class AgentConversationTurnPolicyTests
{
    [Fact]
    public void ApprovalRequest_WithInitialSystemEvidence_WaitsForUser()
    {
        AgentMessage message = Message(AgentMessageKind.ApprovalRequest, Entry("System", 1));

        AgentConversationTurnPolicy.IsAgentTurn(message).Should().BeFalse();
    }

    [Fact]
    public void ApprovalRequest_WithLatestUserReply_IsGivenToAgent()
    {
        AgentMessage message = Message(AgentMessageKind.ApprovalRequest,
            Entry("System", 1), Entry("User", 2));

        AgentConversationTurnPolicy.IsAgentTurn(message).Should().BeTrue();
    }

    [Fact]
    public void FeedbackRequest_WithSystemPrompt_IsGivenToAgent()
    {
        AgentMessage message = Message(AgentMessageKind.FeedbackRequest, Entry("System", 1));

        AgentConversationTurnPolicy.IsAgentTurn(message).Should().BeTrue();
    }

    [Fact]
    public void Conversation_WithLatestAgentReply_WaitsForUser()
    {
        AgentMessage message = Message(AgentMessageKind.FeedbackRequest,
            Entry("User", 1), Entry("Agent", 2));

        AgentConversationTurnPolicy.IsAgentTurn(message).Should().BeFalse();
    }

    static AgentMessage Message(AgentMessageKind kind, params AgentMessageEntry[] entries)
    {
        AgentMessage message = new() { Id = Guid.NewGuid(), Kind = kind };
        foreach (AgentMessageEntry entry in entries) message.Entries.Add(entry);
        return message;
    }

    static AgentMessageEntry Entry(string role, int minute) => new()
    {
        Id = Guid.NewGuid(), Role = role,
        CreatedOn = new DateTimeOffset(2026, 7, 16, 12, minute, 0, TimeSpan.Zero)
    };
}
