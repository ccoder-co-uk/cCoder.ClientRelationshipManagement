using cCoder.ClientRelationshipManagement.Platform.Models.Entities;

namespace cCoder.ClientRelationshipManagement.Services.Foundations.Platform;

public interface ISalesCoordinationService
{
    IQueryable<Opportunity> RetrieveOpportunities();
    IQueryable<ProcessTask> RetrieveProcessTasks();
    IQueryable<Lead> RetrieveLeads();
    IQueryable<Activity> RetrieveActivities();
    IQueryable<CompanyHistoryItem> RetrieveCompanyHistory();
    IQueryable<Company> RetrieveCompanies();
    IQueryable<CompanyContact> RetrieveCompanyContacts();
    IQueryable<TenantCompanyRelationship> RetrieveRelationships();
    IQueryable<ClientAccount> RetrieveClientAccounts();
    IQueryable<LeadContact> RetrieveLeadContacts();
    IQueryable<RelationshipContact> RetrieveRelationshipContacts();
    IQueryable<Material> RetrieveMaterials();
    IQueryable<ProcessTransition> RetrieveProcessTransitions();
    IQueryable<ProcessTask> RetrieveRunnableProcessTasks(DateTimeOffset now);
    IQueryable<Address> RetrieveAddresses();
    ValueTask<TenantCompanyRelationship> UpdateClientAsync(UpdateClientCommand command, CancellationToken cancellationToken = default);
    ValueTask<Activity> AddActivityAsync(AddActivityCommand command, CancellationToken cancellationToken = default);
    ValueTask<Opportunity> AddOpportunityAsync(AddOpportunityCommand command, CancellationToken cancellationToken = default);
    void Add(Address entity);
    void Add(LeadContact entity);
    void Add(Lead entity);
    void Add(Activity entity);
    void Add(Company entity);
    void Add(CompanyContact entity);
    ValueTask SaveAsync(CancellationToken cancellationToken = default);
}

public sealed record UpdateClientCommand(Guid Id, string CompanyName, string TradingName,
    string ContactEmailAddress, string ContactPhoneNumber, string WebsiteUrl, string ResearchSummary,
    string DataQualityNotes, bool IsVerified, string PrimaryContactName, string PrimaryContactPosition,
    cCoder.ClientRelationshipManagement.Platform.Models.Enums.ClientAccountStatus ClientAccountStatus,
    string AccountReference, DateTimeOffset? ContractSignedOn, DateTimeOffset? GoLiveOn,
    string AccountOwner, cCoder.ClientRelationshipManagement.Platform.Models.Enums.RelationshipStatus Status,
    cCoder.ClientRelationshipManagement.Platform.Models.Enums.SalesPipelineStage CurrentStage,
    cCoder.ClientRelationshipManagement.Platform.Models.Enums.RelationshipPriority Priority,
    string LeadSource, string InitialRoute, decimal? FitScore, string OpportunitySummary,
    string PreferredOpeningAngle, bool IsArchived);

public sealed record AddActivityCommand(Guid RelationshipId, Guid? OpportunityId, Guid? ClientAccountId,
    DateTimeOffset ActivityOn, cCoder.ClientRelationshipManagement.Platform.Models.Enums.ActivityType Type,
    cCoder.ClientRelationshipManagement.Platform.Models.Enums.ActivityDirection Direction,
    string Summary, string Outcome, string NextAction, DateTimeOffset? NextActionDueOn);

public sealed record AddOpportunityCommand(Guid RelationshipId,
    cCoder.ClientRelationshipManagement.Platform.Models.Enums.OpportunityType Type,
    cCoder.ClientRelationshipManagement.Platform.Models.Enums.SalesPipelineStage Stage,
    decimal? EstimatedAnnualValue, decimal? Probability, string PainSummary,
    string ValueHypothesis, string DecisionProcess);
