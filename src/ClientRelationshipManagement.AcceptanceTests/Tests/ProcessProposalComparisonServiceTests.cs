using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.Web.Models.Admin;
using ClientRelationshipManagement.Web.Services.Processes;
using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed class ProcessProposalComparisonServiceTests
{
    [Fact]
    public void Build_IdentifiesExactChangedStepAndProperty()
    {
        ProcessDefinition current = Process("Current recipient body");
        ProcessDefinition proposed = Process("Recipient-ready proposed body");
        proposed.Id = Guid.NewGuid(); proposed.VersionNumber = 3; proposed.SupersedesProcessDefinitionId = current.Id;

        ProcessProposalReviewViewModel comparison = ProcessProposalComparisonService.Build(current, proposed);

        comparison.Changes.Should().ContainSingle();
        comparison.Changes[0].StepKey.Should().Be("intro-email");
        comparison.Changes[0].Property.Should().Be("Email body");
        comparison.Changes[0].CurrentValue.Should().Be("Current recipient body");
        comparison.Changes[0].ProposedValue.Should().Be("Recipient-ready proposed body");
        comparison.Steps.Single().ChangeState.Should().Be("modified");
        comparison.HasRoutingChanges.Should().BeFalse();
    }

    static ProcessDefinition Process(string body)
    {
        ProcessDefinition process = new() { Id = Guid.NewGuid(), TenantId = "default", Name = "Opportunity Conversion",
            Description = "Description", ScopeType = ProcessScopeType.Opportunity, VersionNumber = 2, CreatedOn = DateTimeOffset.UtcNow };
        process.Steps.Add(new ProcessStep { Id = Guid.NewGuid(), ProcessDefinitionId = process.Id, Key = "intro-email",
            Name = "Send Intro Email", Sequence = 20, ActionType = ProcessActionType.Email, EmailBodyTemplate = body });
        return process;
    }
}
