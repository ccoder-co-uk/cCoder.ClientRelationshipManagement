using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using ClientRelationshipManagement.Web.Services.Leads;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

[Collection(CRMAcceptanceCollection.Name)]
public sealed class LeadWorkIntakeServiceTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture)
{
    [CRMAcceptanceFact]
    public async Task EnsureCapacity_ImmediatelyAvailableAuthorityWorkDoesNotOverfillQueue()
    {
        List<Guid> leadIds = [];
        for (int index = 0; index < 10; index++)
        {
            (Guid leadId, _) = await SeedLeadAsync();
            leadIds.Add(leadId);
        }

        await ExecuteInAdminContextAsync(async db =>
        {
            Lead[] leads = await db.Leads.Where(item => leadIds.Contains(item.Id)).ToArrayAsync();
            foreach (Lead lead in leads)
            {
                lead.SourceSystem = "CompaniesHouse";
                lead.RankingScore = 100;
            }

            await db.SaveChangesAsync();
        });

        foreach (Guid leadId in leadIds)
        {
            await ExecuteWorkflowAsync(service => service
                .EnsureCoverageAsync(leadId: leadId, forceCreate: true)
                .AsTask());
        }

        Guid candidateCompanyId = Guid.NewGuid();
        await ExecuteInAdminContextAsync(async db =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            db.Companies.Add(new Company
            {
                Id = candidateCompanyId,
                SourceSystem = "CompaniesHouse",
                SourceRecordId = Unique("bounded-authority"),
                OfficialName = Unique("Bounded Intake Ltd"),
                LegalEntityName = "Bounded Intake Ltd",
                CompanyNumber = Unique("BI"),
                CompanyStatus = "active",
                CountryOfOrigin = "GB",
                RankingScore = 99,
                IsProspectingSuppressed = false,
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });
            await db.SaveChangesAsync();
        });

        LeadWorkIntakeResult result;
        using (IServiceScope scope = Fixture.Factory.Services.CreateScope())
        {
            result = await scope.ServiceProvider
                .GetRequiredService<ILeadWorkIntakeService>()
                .EnsureCapacityAsync();
        }

        result.RunnableWorkItems.Should().BeGreaterThanOrEqualTo(10);
        result.PromotedCompanyCount.Should().Be(0);
        bool promoted = await QueryInAdminContextAsync(db =>
            db.Leads.AnyAsync(item => item.CompanyId == candidateCompanyId));
        promoted.Should().BeFalse();
    }

    [CRMAcceptanceFact]
    public async Task EnsureCapacity_AllPendingTasksAwaitingApprovalDoesNotPromoteNewLeadWork()
    {
        (_, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        await ExecuteWorkflowAsync(service => service
            .EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true)
            .AsTask());

        Guid candidateCompanyId = Guid.NewGuid();
        await ExecuteInAdminContextAsync(async db =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            ProcessTask[] pendingTasks = await db.ProcessTasks
                .Where(item => item.State == ProcessTaskState.Pending)
                .ToArrayAsync();

            foreach (ProcessTask task in pendingTasks)
            {
                task.ActionType = ProcessActionType.Approval;
                task.DueOn = now.AddMinutes(-1);
            }

            db.Companies.Add(new Company
            {
                Id = candidateCompanyId,
                SourceSystem = "CompaniesHouse",
                SourceRecordId = Unique("approval-authority"),
                OfficialName = Unique("Approval Pause Ltd"),
                LegalEntityName = "Approval Pause Ltd",
                CompanyNumber = Unique("AP"),
                CompanyStatus = "active",
                CountryOfOrigin = "GB",
                RankingScore = 100,
                IsProspectingSuppressed = false,
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });

            await db.SaveChangesAsync();
        });

        LeadWorkIntakeResult result;
        using (IServiceScope scope = Fixture.Factory.Services.CreateScope())
        {
            result = await scope.ServiceProvider
                .GetRequiredService<ILeadWorkIntakeService>()
                .EnsureCapacityAsync();
        }

        result.PromotedCompanyCount.Should().Be(0);
        bool promoted = await QueryInAdminContextAsync(db =>
            db.Leads.AnyAsync(item => item.CompanyId == candidateCompanyId));
        promoted.Should().BeFalse();
    }

    [CRMAcceptanceFact]
    public async Task EnsureCapacity_FuturePendingTasksDoNotBlockNewRunnableLeadWork()
    {
        (Guid relationshipId, Guid opportunityId, _) = await SeedOpportunityWorkspaceAsync();
        await ExecuteWorkflowAsync(service => service
            .EnsureCoverageAsync(opportunityId: opportunityId, forceCreate: true)
            .AsTask());

        Guid candidateCompanyId = Guid.NewGuid();
        await ExecuteInAdminContextAsync(async db =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            ProcessTask template = await db.ProcessTasks
                .FirstAsync(item => item.OpportunityId == opportunityId && item.State == ProcessTaskState.Pending);
            template.DueOn = now.AddDays(3);

            for (int index = 1; index < 25; index++)
            {
                db.ProcessTasks.Add(new ProcessTask
                {
                    Id = Guid.NewGuid(),
                    ProcessInstanceId = template.ProcessInstanceId,
                    ProcessStepId = template.ProcessStepId,
                    TenantCompanyRelationshipId = relationshipId,
                    OpportunityId = opportunityId,
                    ActionType = ProcessActionType.ManualTask,
                    State = ProcessTaskState.Pending,
                    DueOn = now.AddDays(3).AddMinutes(index),
                    RenderedTitle = $"Future acceptance task {index}",
                    RenderedInstructions = "Future work must not consume today's runnable capacity.",
                    CreatedBy = Fixture.Settings.UserId,
                    LastUpdatedBy = Fixture.Settings.UserId,
                    CreatedOn = now,
                    LastUpdated = now
                });
            }

            db.Companies.Add(new Company
            {
                Id = candidateCompanyId,
                SourceSystem = "CompaniesHouse",
                SourceRecordId = Unique("authority"),
                OfficialName = Unique("Runnable Intake Ltd"),
                LegalEntityName = "Runnable Intake Ltd",
                CompanyNumber = Unique("CH"),
                CompanyStatus = "active",
                CountryOfOrigin = "GB",
                RankingScore = 100,
                IsProspectingSuppressed = false,
                CreatedBy = Fixture.Settings.UserId,
                LastUpdatedBy = Fixture.Settings.UserId,
                CreatedOn = now,
                LastUpdated = now
            });

            await db.SaveChangesAsync();
        });

        LeadWorkIntakeResult result;
        using (IServiceScope scope = Fixture.Factory.Services.CreateScope())
        {
            result = await scope.ServiceProvider
                .GetRequiredService<ILeadWorkIntakeService>()
                .EnsureCapacityAsync();
        }

        result.RunnableWorkItems.Should().BeGreaterThan(0);
        result.PromotedCompanyCount.Should().BeGreaterThan(0);
        bool promoted = await QueryInAdminContextAsync(db =>
            db.Leads.AnyAsync(item => item.CompanyId == candidateCompanyId));
        promoted.Should().BeTrue();
    }
}
