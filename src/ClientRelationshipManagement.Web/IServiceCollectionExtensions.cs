using cCoder.AI;
using cCoder.AI.Models.Configurations;
using cCoder.ClientRelationshipManagement.Platform;
using cCoder.Eventing;
using cCoder.Security;
using cCoder.Security.Data.EF;
using cCoder.Security.Data.EF.Interfaces;
using cCoder.Security.Objects;
using ClientRelationshipManagement.Web.Brokers.Loggings;
using ClientRelationshipManagement.Web.Configuration;
using ClientRelationshipManagement.Web.Services.Agents;
using ClientRelationshipManagement.Web.Services.Execution;
using ClientRelationshipManagement.Web.Services.Imports;
using ClientRelationshipManagement.Web.Services.Leads;
using ClientRelationshipManagement.Web.Services.Mail;
using ClientRelationshipManagement.Web.Services.Migration;
using ClientRelationshipManagement.Web.Services.Processes;

namespace ClientRelationshipManagement.Web;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddCrmApplication(
        this IServiceCollection services,
        IConfiguration configuration,
        string crmConnection,
        string crmAdminConnection,
        string ssoConnection,
        string decryptionKey,
        Action<CrmApplicationRegistrationOptions> configure = null)
    {
        CrmApplicationRegistrationOptions options = new();
        configure?.Invoke(options);

        if (options.IncludeMvc)
        {
            services.AddControllersWithViews();
            services.AddCors();
            services.AddSession();
        }

        services.AddEventing();
        services.AddHttpClient();
        services.Configure<MailOptions>(configuration.GetSection("Mail"));
        services.Configure<AgentWorkflowOptions>(configuration.GetSection("AgentWorkflows"));
        services.Configure<AuthorityDataOptions>(configuration.GetSection("AuthorityData"));
        services.Configure<ImportWorkflowOptions>(configuration.GetSection("ImportWorkflow"));
        services.PostConfigure<MailOptions>(mailOptions =>
        {
            ApplyBool(configuration, "Mail:EmailSendingEnabled", value => mailOptions.EmailSendingEnabled = value);
            ApplyString(configuration, "Mail:Provider", value => mailOptions.Provider = value);
            ApplyString(configuration, "Mail:Host", value => mailOptions.Host = value);
            ApplyInt(configuration, "Mail:Port", value => mailOptions.Port = value);
            ApplyString(configuration, "Mail:UserName", value => mailOptions.UserName = value);
            ApplyString(configuration, "Mail:Password", value => mailOptions.Password = value);
            ApplyBool(configuration, "Mail:UseSsl", value => mailOptions.UseSsl = value);
            ApplyString(configuration, "Mail:ApiKey", value => mailOptions.ApiKey = value);
            ApplyString(configuration, "Mail:BaseUrl", value => mailOptions.BaseUrl = value);
            ApplyString(configuration, "Mail:FallbackFromAddress", value => mailOptions.FallbackFromAddress = value);
            ApplyString(configuration, "Mail:SafeRecipientOverrideAddress", value => mailOptions.SafeRecipientOverrideAddress = value);
            ApplyInt(configuration, "Mail:RetryLimit", value => mailOptions.RetryLimit = value);
            ApplyInt(configuration, "Mail:PollIntervalSeconds", value => mailOptions.PollIntervalSeconds = value);
            ApplyInt(configuration, "Mail:BatchSize", value => mailOptions.BatchSize = value);
        });
        services.PostConfigure<AgentWorkflowOptions>(agentWorkflowOptions =>
        {
            ApplyBool(configuration, "AgentWorkflows:Enabled", value => agentWorkflowOptions.Enabled = value);
            ApplyBool(configuration, "AgentWorkflows:TaskAgentEnabled", value => agentWorkflowOptions.TaskAgentEnabled = value);
            ApplyBool(configuration, "AgentWorkflows:ProcessOptimiserEnabled", value => agentWorkflowOptions.ProcessOptimiserEnabled = value);
            ApplyInt(configuration, "AgentWorkflows:TaskAgentIntervalMinutes", value => agentWorkflowOptions.TaskAgentIntervalMinutes = value);
            ApplyInt(configuration, "AgentWorkflows:ProcessOptimiserIntervalMinutes", value => agentWorkflowOptions.ProcessOptimiserIntervalMinutes = value);
            ApplyString(configuration, "AgentWorkflows:ExecutionUserId", value => agentWorkflowOptions.ExecutionUserId = value);
            ApplyString(configuration, "AgentWorkflows:AgentWorkspacePath", value => agentWorkflowOptions.AgentWorkspacePath = value);
            ApplyString(configuration, "AgentWorkflows:CrmApiBaseUrl", value => agentWorkflowOptions.CrmApiBaseUrl = value);
            ApplyString(configuration, "AgentWorkflows:TaskAgentProvider", value => agentWorkflowOptions.TaskAgentProvider = value);
            ApplyString(configuration, "AgentWorkflows:TaskAgentModel", value => agentWorkflowOptions.TaskAgentModel = value);
            ApplyString(configuration, "AgentWorkflows:ProcessOptimiserProvider", value => agentWorkflowOptions.ProcessOptimiserProvider = value);
            ApplyString(configuration, "AgentWorkflows:ProcessOptimiserModel", value => agentWorkflowOptions.ProcessOptimiserModel = value);
            ApplyInt(configuration, "AgentWorkflows:MaxIterations", value => agentWorkflowOptions.MaxIterations = value);
            ApplyInt(configuration, "AgentWorkflows:SessionArchiveLimit", value => agentWorkflowOptions.SessionArchiveLimit = value);
        });
        services.PostConfigure<AuthorityDataOptions>(authorityDataOptions =>
        {
            ApplyBool(configuration, "AuthorityData:Enabled", value => authorityDataOptions.Enabled = value);
            ApplyInt(configuration, "AuthorityData:IntervalHours", value => authorityDataOptions.IntervalHours = value);
            ApplyString(configuration, "AuthorityData:DropPath", value => authorityDataOptions.DropPath = value);
            ApplyString(configuration, "AuthorityData:ArchivePath", value => authorityDataOptions.ArchivePath = value);
            ApplyString(configuration, "AuthorityData:FailedPath", value => authorityDataOptions.FailedPath = value);
            ApplyString(configuration, "AuthorityData:SourceSystem", value => authorityDataOptions.SourceSystem = value);
            ApplyString(configuration, "AuthorityData:DefaultTenantId", value => authorityDataOptions.DefaultTenantId = value);
            ApplyInt(configuration, "AuthorityData:BatchSize", value => authorityDataOptions.BatchSize = value);
            ApplyInt(configuration, "AuthorityData:MergeBatchSize", value => authorityDataOptions.MergeBatchSize = value);
            ApplyInt(configuration, "AuthorityData:MaxMergeChunksPerRun", value => authorityDataOptions.MaxMergeChunksPerRun = value);
            ApplyInt(configuration, "AuthorityData:MaxRunMinutes", value => authorityDataOptions.MaxRunMinutes = value);
        });
        services.PostConfigure<ImportWorkflowOptions>(importWorkflowOptions =>
        {
            ApplyString(configuration, "ImportWorkflow:HostedServicesBaseUrl", value => importWorkflowOptions.HostedServicesBaseUrl = value);
            ApplyString(configuration, "ImportWorkflow:AgentWorkspacePath", value => importWorkflowOptions.AgentWorkspacePath = value);
            ApplyInt(configuration, "ImportWorkflow:UploadSessionExpiryMinutes", value => importWorkflowOptions.UploadSessionExpiryMinutes = value);
            ApplyInt(configuration, "ImportWorkflow:ChunkSizeBytes", value => importWorkflowOptions.ChunkSizeBytes = value);
            ApplyInt(configuration, "ImportWorkflow:ProcessingIntervalMinutes", value => importWorkflowOptions.ProcessingIntervalMinutes = value);
            ApplyInt(configuration, "ImportWorkflow:ProcessingBatchSize", value => importWorkflowOptions.ProcessingBatchSize = value);
            ApplyInt(configuration, "ImportWorkflow:OpportunityScoreThreshold", value => importWorkflowOptions.OpportunityScoreThreshold = value);
        });
        services.AddAI((_, aiConfiguration) =>
            configuration.GetSection(AIConfiguration.SectionName).Bind(aiConfiguration));

        services.AddScoped<ISecurityDbContextFactory>(provider =>
            new MSSQLSecurityDbContextFactory(ssoConnection)
            {
                GetAuthInfo = ignoreAuthInfo => ignoreAuthInfo
                    ? new SSOAuthInfo { SSOUserId = "Guest" }
                    : provider.GetRequiredService<ISSOAuthInfo>(),
            });

        services.AddSecurity((securityServices, security) =>
            security.UseAESHMMACPasswordEncryption(securityServices, decryptionKey));
        services.AddSingleton(typeof(ILoggingBroker<>), typeof(LoggingBroker<>));
        services.AddScoped<ICurrentExecutionUserAccessor, CurrentExecutionUserAccessor>();
        services.AddScoped<IAgentWorkspaceService, AgentWorkspaceService>();
        services.AddScoped<IAgentSessionArchiveService, AgentSessionArchiveService>();
        services.AddScoped<IAgentRunJournalService, AgentRunJournalService>();
        services.AddScoped<IAgentMessageService, AgentMessageService>();
        services.AddScoped<IAgentExecutionTokenService, AgentExecutionTokenService>();
        services.AddScoped<IProcessDraftService, ProcessDraftService>();
        services.AddScoped<IAgentWorkflowRunner, AgentWorkflowRunner>();
        services.AddScoped<ICurrentUserMailProfileProvider, CurrentUserMailProfileProvider>();
        services.AddScoped<IEmailDraftWorkflowService, EmailDraftWorkflowService>();
        services.AddScoped<IMailClientFactory, SmtpMailClientFactory>();
        services.AddScoped<IEmailDispatchProcessor, EmailDispatchProcessor>();
        services.AddScoped<ILeadIngestionService, LeadIngestionService>();
        services.AddScoped<IAuthorityDataImportService, AuthorityDataImportService>();
        services.AddScoped<IHostedImportClient, HostedImportClient>();
        services.AddScoped<IImportFileWorkspaceService, ImportFileWorkspaceService>();
        services.AddScoped<IImportProcessingService, ImportProcessingService>();
        services.AddScoped<ICrmPlatformBootstrapService, CrmPlatformBootstrapService>();
        services.AddScoped<ICrmDatabaseInitialiser, CrmDatabaseInitialiser>();
        services.AddScoped<IWorkflowAutomationService, WorkflowAutomationService>();

        if (options.IncludeHostedServices)
        {
            services.AddHostedService<ScheduledEmailSenderHostedService>();
            services.AddHostedService<ScheduledTaskAgentHostedService>();
            services.AddHostedService<ScheduledProcessOptimiserHostedService>();
            services.AddHostedService<ScheduledAuthorityDataIngestHostedService>();
            services.AddHostedService<ScheduledImportProcessingHostedService>();
        }

        services.AddCrmPlatform(platformConfiguration =>
        {
            platformConfiguration.ConnectionString = crmConnection;
            platformConfiguration.AdminConnectionString = crmAdminConnection;
        });

        return services;
    }

    static void ApplyString(IConfiguration configuration, string key, Action<string> apply)
    {
        string value = ConfigurationValueResolver.GetOptional(configuration, key);
        if (!string.IsNullOrWhiteSpace(value))
            apply(value);
    }

    static void ApplyInt(IConfiguration configuration, string key, Action<int> apply)
    {
        string rawValue = ConfigurationValueResolver.GetOptional(configuration, key);
        if (int.TryParse(rawValue, out int value))
            apply(value);
    }

    static void ApplyBool(IConfiguration configuration, string key, Action<bool> apply)
    {
        string rawValue = ConfigurationValueResolver.GetOptional(configuration, key);
        if (bool.TryParse(rawValue, out bool value))
            apply(value);
    }
}
