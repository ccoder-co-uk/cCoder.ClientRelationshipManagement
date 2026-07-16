using cCoder.ClientRelationshipManagement.Brokers.Transactions;
using cCoder.ClientRelationshipManagement.Services.Entities;
using cCoder.ClientRelationshipManagement.Models.Security;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Services.Foundations.Platform;

internal sealed class SalesCoordinationService(
    IOpportunityOrchestrationService opportunities,
    IProcessTaskOrchestrationService processTasks,
    ILeadOrchestrationService leads,
    IActivityOrchestrationService activities,
    ICompanyHistoryItemOrchestrationService companyHistory,
    ICompanyOrchestrationService companies,
    ICompanyContactOrchestrationService companyContacts,
    ITenantCompanyRelationshipOrchestrationService relationships,
    IClientAccountOrchestrationService clientAccounts,
    ILeadContactOrchestrationService leadContacts,
    IRelationshipContactOrchestrationService relationshipContacts,
    IMaterialOrchestrationService materials,
    IProcessTransitionOrchestrationService processTransitions,
    IAddressOrchestrationService addresses,
    IAgentMessageOrchestrationService agentMessages,
    ICRMAuthInfo auth, ICRMTransactionBroker transaction) : ISalesCoordinationService
{
    string[] Tenants => auth.ReadableTenants.Length > 0 ? auth.ReadableTenants : auth.WriteableTenants;
    public IQueryable<Opportunity> RetrieveOpportunities() => opportunities.RetrieveAll()
        .Where(item => Tenants.Contains(item.TenantCompanyRelationship.TenantId));
    public IQueryable<Lead> RetrieveLeads() => leads.RetrieveAll().Where(item => Tenants.Contains(item.TenantId));
    public IQueryable<Activity> RetrieveActivities() => activities.RetrieveAll()
        .Where(item => Tenants.Contains(item.TenantCompanyRelationship.TenantId));
    public IQueryable<CompanyHistoryItem> RetrieveCompanyHistory() => companyHistory.RetrieveAll()
        .Where(item => Tenants.Contains(item.TenantId));
    public IQueryable<Company> RetrieveCompanies() => companies.RetrieveAll().Where(company =>
        company.SourceSystem == "CompaniesHouse"
        || company.Relationships.Any(relationship => Tenants.Contains(relationship.TenantId))
        || leads.RetrieveAll().Any(lead => lead.CompanyId == company.Id && Tenants.Contains(lead.TenantId))
        || companyHistory.RetrieveAll().Any(history => history.CompanyId == company.Id && Tenants.Contains(history.TenantId)));
    public IQueryable<CompanyContact> RetrieveCompanyContacts() => companyContacts.RetrieveAll().Where(contact =>
        RetrieveCompanies().Any(company => company.Id == contact.CompanyId));
    public IQueryable<TenantCompanyRelationship> RetrieveRelationships() => relationships.RetrieveAll()
        .Where(item => Tenants.Contains(item.TenantId));
    public IQueryable<ClientAccount> RetrieveClientAccounts() => clientAccounts.RetrieveAll()
        .Where(item => Tenants.Contains(item.TenantCompanyRelationship.TenantId));
    public IQueryable<LeadContact> RetrieveLeadContacts() => leadContacts.RetrieveAll()
        .Where(item => Tenants.Contains(item.Lead.TenantId));
    public IQueryable<RelationshipContact> RetrieveRelationshipContacts() => relationshipContacts.RetrieveAll()
        .Where(item => Tenants.Contains(item.TenantCompanyRelationship.TenantId));
    public IQueryable<Material> RetrieveMaterials() => materials.RetrieveAll()
        .Where(item => Tenants.Contains(item.TenantCompanyRelationship.TenantId));
    public IQueryable<ProcessTransition> RetrieveProcessTransitions() => processTransitions.RetrieveAll().Where(item =>
        Tenants.Contains(item.ProcessStep.ProcessDefinition.TenantId));
    public IQueryable<ProcessTask> RetrieveRunnableProcessTasks(DateTimeOffset now) => RetrieveProcessTasks().Where(task =>
        task.State == ProcessTaskState.Pending && task.DueOn <= now
        && (!task.AgentClaimExpiresOn.HasValue || task.AgentClaimExpiresOn <= now)
        && task.ActionType != ProcessActionType.Approval
        && !agentMessages.RetrieveAll().Any(message => message.ProcessTaskId == task.Id
            && message.State == AgentMessageState.Pending
            && (message.Kind == AgentMessageKind.ApprovalRequest || message.Kind == AgentMessageKind.FeedbackRequest)
            && !((task.ActionType == ProcessActionType.Call || task.ActionType == ProcessActionType.Meeting)
                && processTransitions.RetrieveAll().Any(transition => transition.ProcessStepId == task.ProcessStepId
                    && transition.OutcomeKey == "await-response")))
        && !(task.ActionType == ProcessActionType.Email && task.EmailId.HasValue
            && (task.Email.State == EmailState.Draft || task.Email.State == EmailState.Approved
                || task.Email.State == EmailState.Sending || task.Email.State == EmailState.Sent)));
    public IQueryable<Address> RetrieveAddresses() => addresses.RetrieveAll().Where(address =>
        RetrieveCompanies().Any(company => company.RegisteredAddressId == address.Id));

    public async ValueTask<TenantCompanyRelationship> UpdateClientAsync(UpdateClientCommand c, CancellationToken token = default)
    {
        TenantCompanyRelationship relationship = await relationships.RetrieveAll().Include(item => item.Company)
            .FirstOrDefaultAsync(item => item.Id == c.Id, token);
        if (relationship is null || !auth.WriteableTenants.Contains(relationship.TenantId)) return null;
        DateTimeOffset now = DateTimeOffset.UtcNow; string user = auth.SSOUserId;
        Company company = relationship.Company;
        company.OfficialName = c.CompanyName.Trim(); company.TradingName = Normalize(c.TradingName);
        company.ContactEmailAddress = Normalize(c.ContactEmailAddress); company.ContactPhoneNumber = Normalize(c.ContactPhoneNumber);
        company.WebsiteUrl = Normalize(c.WebsiteUrl); company.ResearchSummary = Normalize(c.ResearchSummary);
        company.VerificationNotes = Normalize(c.DataQualityNotes); company.IsVerified = c.IsVerified; Touch(company, user, now);
        RelationshipContact contact = await relationshipContacts.RetrieveAll().Include(item => item.CompanyContact)
            .Where(item => item.TenantCompanyRelationshipId == c.Id && item.Status == RelationshipContactStatus.Active)
            .OrderByDescending(item => item.IsPrimary).FirstOrDefaultAsync(token);
        if (contact is not null) { contact.IsPrimary = true; contact.CompanyContact.Name = c.PrimaryContactName.Trim();
            contact.CompanyContact.Position = Normalize(c.PrimaryContactPosition); contact.CompanyContact.EmailAddress = Normalize(c.ContactEmailAddress);
            contact.CompanyContact.PhoneNumber = Normalize(c.ContactPhoneNumber); contact.CompanyContact.IsPrimary = true;
            Touch(contact.CompanyContact, user, now); Touch(contact, user, now); }
        ClientAccount account = await clientAccounts.RetrieveAll().Where(item => item.TenantCompanyRelationshipId == c.Id && item.Status != ClientAccountStatus.Closed)
            .OrderByDescending(item => item.CreatedOn).FirstOrDefaultAsync(token);
        if (account is not null) { account.Status = c.ClientAccountStatus; account.AccountReference = Normalize(c.AccountReference);
            account.ContractSignedOn = c.ContractSignedOn; account.GoLiveOn = c.GoLiveOn; Touch(account, user, now); }
        relationship.AccountOwnerDisplayName = Normalize(c.AccountOwner); relationship.AccountOwnerUserId ??= user;
        relationship.Status = c.Status; relationship.CurrentStage = c.CurrentStage; relationship.Priority = c.Priority;
        relationship.LeadSource = Normalize(c.LeadSource); relationship.InitialRoute = Normalize(c.InitialRoute); relationship.FitScore = c.FitScore;
        relationship.OpportunitySummary = Normalize(c.OpportunitySummary); relationship.PreferredOpeningAngle = Normalize(c.PreferredOpeningAngle);
        relationship.ResearchSummary = Normalize(c.ResearchSummary); relationship.DataQualityNotes = Normalize(c.DataQualityNotes);
        relationship.IsArchived = c.IsArchived; Touch(relationship, user, now); await SaveAsync(token); return relationship;
    }
    public async ValueTask<Activity> AddActivityAsync(AddActivityCommand c, CancellationToken token = default)
    {
        if (!await CanWriteAsync(c.RelationshipId, token)) return null; DateTimeOffset now = DateTimeOffset.UtcNow;
        Activity item = new() { Id = Guid.NewGuid(), TenantCompanyRelationshipId = c.RelationshipId, OpportunityId = c.OpportunityId,
            ClientAccountId = c.ClientAccountId, ActivityOn = c.ActivityOn, Type = c.Type, Direction = c.Direction,
            Summary = c.Summary.Trim(), Outcome = c.Outcome.Trim(), NextAction = Normalize(c.NextAction), NextActionDueOn = c.NextActionDueOn,
            CreatedBy = auth.SSOUserId, LastUpdatedBy = auth.SSOUserId, CreatedOn = now, LastUpdated = now };
        await activities.AddAsync(item, token); return item;
    }
    public async ValueTask<Opportunity> AddOpportunityAsync(AddOpportunityCommand c, CancellationToken token = default)
    {
        if (!await CanWriteAsync(c.RelationshipId, token)) return null; DateTimeOffset now = DateTimeOffset.UtcNow;
        Opportunity item = new() { Id = Guid.NewGuid(), TenantCompanyRelationshipId = c.RelationshipId, Type = c.Type, Stage = c.Stage,
            EstimatedAnnualValue = c.EstimatedAnnualValue, Probability = c.Probability, PainSummary = c.PainSummary.Trim(),
            ValueHypothesis = Normalize(c.ValueHypothesis), DecisionProcess = Normalize(c.DecisionProcess), CreatedBy = auth.SSOUserId,
            LastUpdatedBy = auth.SSOUserId, CreatedOn = now, LastUpdated = now };
        await opportunities.AddAsync(item, token); return item;
    }
    async ValueTask<bool> CanWriteAsync(Guid id, CancellationToken token) => await relationships.RetrieveAll()
        .AnyAsync(item => item.Id == id && auth.WriteableTenants.Contains(item.TenantId), token);
    static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    static void Touch(ICrmEntity item, string user, DateTimeOffset now) { item.LastUpdatedBy = user; item.LastUpdated = now; }
    public IQueryable<ProcessTask> RetrieveProcessTasks() => processTasks.RetrieveAll().Where(item =>
        (item.LeadId.HasValue && Tenants.Contains(item.Lead.TenantId))
        || (item.TenantCompanyRelationshipId.HasValue && Tenants.Contains(item.TenantCompanyRelationship.TenantId))
        || (item.OpportunityId.HasValue && Tenants.Contains(item.Opportunity.TenantCompanyRelationship.TenantId))
        || (item.ClientAccountId.HasValue && Tenants.Contains(item.ClientAccount.TenantCompanyRelationship.TenantId)));
    readonly List<Address> pendingAddresses = [];
    readonly List<LeadContact> pendingLeadContacts = [];
    readonly List<Lead> pendingLeads = [];
    readonly List<Activity> pendingActivities = [];
    readonly List<Company> pendingCompanies = [];
    readonly List<CompanyContact> pendingCompanyContacts = [];
    public void Add(Address entity) => pendingAddresses.Add(entity);
    public void Add(LeadContact entity) => pendingLeadContacts.Add(entity);
    public void Add(Lead entity)
    {
        if (!auth.WriteableTenants.Contains(entity.TenantId))
            entity.TenantId = auth.WriteableTenants.FirstOrDefault()
                ?? throw new UnauthorizedAccessException("The user has no writable CRM tenant.");
        pendingLeads.Add(entity);
    }
    public void Add(Activity entity) => pendingActivities.Add(entity);
    public void Add(Company entity) => pendingCompanies.Add(entity);
    public void Add(CompanyContact entity) => pendingCompanyContacts.Add(entity);
    public async ValueTask SaveAsync(CancellationToken cancellationToken = default)
    {
        foreach (Address entity in pendingAddresses) await addresses.AddAsync(entity, cancellationToken);
        foreach (Company entity in pendingCompanies) await companies.AddAsync(entity, cancellationToken);
        foreach (CompanyContact entity in pendingCompanyContacts) await companyContacts.AddAsync(entity, cancellationToken);
        foreach (Lead entity in pendingLeads) await leads.AddAsync(entity, cancellationToken);
        foreach (LeadContact entity in pendingLeadContacts) await leadContacts.AddAsync(entity, cancellationToken);
        foreach (Activity entity in pendingActivities) await activities.AddAsync(entity, cancellationToken);
        pendingAddresses.Clear(); pendingCompanies.Clear(); pendingCompanyContacts.Clear();
        pendingLeads.Clear(); pendingLeadContacts.Clear(); pendingActivities.Clear();
        await transaction.CommitAsync(cancellationToken);
    }
}
