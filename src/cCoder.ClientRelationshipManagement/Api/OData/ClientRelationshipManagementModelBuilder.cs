using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;
using cCoder.ClientRelationshipManagement.Platform.Models.Enums;

namespace cCoder.ClientRelationshipManagement.Api.OData;

public sealed class ClientRelationshipManagementModelBuilder
{
    readonly ODataConventionModelBuilder builder;

    public ClientRelationshipManagementModelBuilder(ODataConventionModelBuilder builder = null)
    {
        this.builder = builder ?? new ODataConventionModelBuilder();
        this.builder.Namespace = "CRM";
    }

    public IEdmModel Build()
    {
        Add<Address>("Addresses");
        Add<Source>("Sources");
        Add<Import>("Imports");
        Add<ImportLink>("ImportLinks");
        Add<Company>("Companies");
        Add<CompanyHistoryItem>("CompanyHistory");
        Add<CompanyContact>("CompanyContacts");
        Add<Lead>("Leads");
        Add<LeadContact>("LeadContacts");
        Add<TenantCompanyRelationship>("TenantCompanyRelationships");
        Add<RelationshipContact>("RelationshipContacts");
        Add<Opportunity>("Opportunities");
        Add<ClientAccount>("ClientAccounts");
        Add<HandoffPack>("HandoffPacks");
        Add<Activity>("Activities");
        Add<Material>("Materials");
        Add<Email>("Emails");
        Add<EmailRecipient>("EmailRecipients");
        Add<ProcessDefinition>("ProcessDefinitions");
        Add<ProcessStep>("ProcessSteps");
        Add<ProcessTransition>("ProcessTransitions");
        Add<ProcessInstance>("ProcessInstances");
        Add<ProcessTask>("ProcessTasks");
        Add<AgentRun>("AgentRuns");
        Add<AgentMessage>("AgentMessages");
        Add<AgentMessageEntry>("AgentMessageEntries");
        Add<AgentAutomationSetting>("AgentAutomationSettings");
        Add<MailboxMessageRecord>("MailboxMessageRecords");
        builder.EntityType<AgentMessage>().Action("Reply")
            .Parameter<string>("body");
        var respond = builder.EntityType<AgentMessage>().Action("Respond");
        respond.Parameter<AgentMessageState>("state");
        respond.Parameter<string>("responseNotes");
        var changeState = builder.EntityType<AgentMessage>().Action("ChangeState");
        changeState.Parameter<AgentMessageState>("state");
        changeState.Parameter<string>("auditNote");
        return builder.GetEdmModel();
    }

    void Add<TEntity>(string name) where TEntity : class => builder.EntitySet<TEntity>(name);
}
