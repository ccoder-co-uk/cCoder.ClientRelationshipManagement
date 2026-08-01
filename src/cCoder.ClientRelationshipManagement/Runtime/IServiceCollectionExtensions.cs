using cCoder.AI;
using cCoder.AI.Models.Configurations;
using cCoder.ClientRelationshipManagement.Platform;
using cCoder.ClientRelationshipManagement.Platform.Models.Configuration;
using cCoder.ClientRelationshipManagement.Api;
using cCoder.Eventing;
using cCoder.Security;
using cCoder.ClientRelationshipManagement.Runtime.Brokers.Loggings;
using cCoder.ClientRelationshipManagement.Runtime.Brokers.Storages;
using cCoder.ClientRelationshipManagement.Runtime.Configuration;
using cCoder.ClientRelationshipManagement.Runtime.Services.Agents;
using cCoder.ClientRelationshipManagement.Runtime.Services.Execution;
using cCoder.ClientRelationshipManagement.Runtime.Services.Imports;
using cCoder.ClientRelationshipManagement.Runtime.Services.Leads;
using cCoder.ClientRelationshipManagement.Runtime.Services.Mail;
using cCoder.ClientRelationshipManagement.Runtime.Services.Migration;
using cCoder.ClientRelationshipManagement.Runtime.Services.Processes;

namespace cCoder.ClientRelationshipManagement.Runtime;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddCrmApplication(
        this IServiceCollection services,
        IConfiguration rootConfiguration,
        string crmConnection,
        string crmAdminConnection,
        string ssoConnection,
        string decryptionKey,
        Action<CrmApplicationRegistrationOptions> configure = null)
    {
        CrmApplicationRegistrationOptions options = new();
        configure?.Invoke(options);
        IConfigurationSection configuration = rootConfiguration.GetSection(CRMConfiguration.SectionName);

        if (options.IncludeMvc)
        {
            services.AddControllersWithViews().AddClientRelationshipManagementApi();

            if (options.IncludeApiDocumentation)
                services.AddClientRelationshipManagementApiDocumentation();

            services.AddCors();
            services.AddSession();
        }

        services.AddEventing();
        services.AddHttpClient();
        services.Configure<MailOptions>(configuration.GetSection(MailOptions.SectionName));
        services.Configure<AgentWorkflowOptions>(configuration.GetSection(AgentWorkflowOptions.SectionName));
        services.Configure<AiRoutingOptions>(configuration.GetSection(AiRoutingOptions.SectionName));
        services.Configure<AuthorityDataOptions>(configuration.GetSection(AuthorityDataOptions.SectionName));
        services.Configure<ImportWorkflowOptions>(configuration.GetSection(ImportWorkflowOptions.SectionName));
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
            ApplyBool(configuration, "Mail:MailboxSyncEnabled", value => mailOptions.MailboxSyncEnabled = value);
            ApplyInt(configuration, "Mail:MailboxSyncIntervalSeconds", value => mailOptions.MailboxSyncIntervalSeconds = value);
            ApplyInt(configuration, "Mail:MailboxSyncBatchSize", value => mailOptions.MailboxSyncBatchSize = value);
            ApplyFirstString(configuration, value => mailOptions.MicrosoftGraphTenantId = value,
                "Mail:MicrosoftGraph:TenantId");
            ApplyFirstString(configuration, value => mailOptions.MicrosoftGraphClientId = value,
                "Mail:MicrosoftGraph:ClientId");
            ApplyFirstString(configuration, value => mailOptions.MicrosoftGraphClientSecret = value,
                "Mail:MicrosoftGraph:ClientSecret");
            ApplyFirstString(configuration, value => mailOptions.MicrosoftGraphMailboxUser = value,
                "Mail:MicrosoftGraph:MailboxUser");
            ApplyFirstString(configuration, value => mailOptions.MicrosoftGraphBaseUrl = value,
                "Mail:MicrosoftGraph:GraphBaseUrl");
            ApplyFirstString(configuration, value => mailOptions.MicrosoftGraphLoginBaseUrl = value,
                "Mail:MicrosoftGraph:LoginBaseUrl");
        });
        services.PostConfigure<AgentWorkflowOptions>(agentWorkflowOptions =>
        {
            ApplyBool(configuration, "AgentWorkflows:Enabled", value => agentWorkflowOptions.Enabled = value);
            ApplyBool(configuration, "AgentWorkflows:TaskAgentEnabled", value => agentWorkflowOptions.TaskAgentEnabled = value);
            ApplyBool(configuration, "AgentWorkflows:ProcessOptimiserEnabled", value => agentWorkflowOptions.ProcessOptimiserEnabled = value);
            ApplyBool(configuration, "AgentWorkflows:EmailApprovalAgentEnabled", value => agentWorkflowOptions.EmailApprovalAgentEnabled = value);
            ApplyInt(configuration, "AgentWorkflows:TaskAgentIntervalMinutes", value => agentWorkflowOptions.TaskAgentIntervalMinutes = value);
            ApplyInt(configuration, "AgentWorkflows:TaskAgentRunTimeoutMinutes", value => agentWorkflowOptions.TaskAgentRunTimeoutMinutes = value);
            ApplyInt(configuration, "AgentWorkflows:ProcessOptimiserIntervalMinutes", value => agentWorkflowOptions.ProcessOptimiserIntervalMinutes = value);
            ApplyInt(configuration, "AgentWorkflows:ProcessHealthReviewIntervalHours", value => agentWorkflowOptions.ProcessHealthReviewIntervalHours = value);
            ApplyInt(configuration, "AgentWorkflows:EmailApprovalAgentIntervalMinutes", value => agentWorkflowOptions.EmailApprovalAgentIntervalMinutes = value);
            ApplyString(configuration, "AgentWorkflows:ExecutionUserId", value => agentWorkflowOptions.ExecutionUserId = value);
            ApplyString(configuration, "AgentWorkflows:AgentWorkspacePath", value => agentWorkflowOptions.AgentWorkspacePath = value);
            ApplyString(configuration, "AgentWorkflows:CrmApiBaseUrl", value => agentWorkflowOptions.CrmApiBaseUrl = value);
            ApplyString(configuration, "AgentWorkflows:TaskAgentProvider", value => agentWorkflowOptions.TaskAgentProvider = value);
            ApplyString(configuration, "AgentWorkflows:TaskAgentModel", value => agentWorkflowOptions.TaskAgentModel = value);
            ApplyString(configuration, "AgentWorkflows:ProcessOptimiserProvider", value => agentWorkflowOptions.ProcessOptimiserProvider = value);
            ApplyString(configuration, "AgentWorkflows:ProcessOptimiserModel", value => agentWorkflowOptions.ProcessOptimiserModel = value);
            ApplyString(configuration, "AgentWorkflows:EmailApprovalAgentProvider", value => agentWorkflowOptions.EmailApprovalAgentProvider = value);
            ApplyString(configuration, "AgentWorkflows:EmailApprovalAgentModel", value => agentWorkflowOptions.EmailApprovalAgentModel = value);
            ApplyInt(configuration, "AgentWorkflows:EmailApprovalAgentBatchSize", value => agentWorkflowOptions.EmailApprovalAgentBatchSize = value);
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
            ApplyString(configuration, "AuthorityData:SourceCountryCode", value => authorityDataOptions.SourceCountryCode = value);
            ApplyString(configuration, "AuthorityData:SourceNotes", value => authorityDataOptions.SourceNotes = value);
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
        if (options.IncludeAI)
        {
            services.AddAIWeb(aiConfiguration =>
            {
                rootConfiguration.GetSection(AIConfiguration.SectionName).Bind(aiConfiguration);
                RegisterNamedAiProviders(rootConfiguration, configuration, aiConfiguration);
            });
        }

        if (options.IncludeSecurity)
        {
            services.AddSecurityWeb(security =>
            {
                security.ConnectionString = ssoConnection;
                security.DecryptionKey = decryptionKey;
                security.RootPath = string.Empty;
            });
        }
        services.AddSingleton(typeof(ILoggingBroker<>), typeof(LoggingBroker<>));
        services.AddScoped<IEmailWorkflowBroker, EmailWorkflowBroker>();
        services.AddScoped<IWorkflowBroker, WorkflowBroker>();
        services.AddScoped<ICurrentExecutionUserAccessor, CurrentExecutionUserAccessor>();
        services.AddScoped<IAgentWorkspaceService, AgentWorkspaceService>();
        services.AddScoped<IAgentSessionArchiveService, AgentSessionArchiveService>();
        services.AddScoped<IAgentRunJournalService, AgentRunJournalService>();
        services.AddScoped<IAgentMessageService, AgentMessageService>();
        services.AddScoped<IAgentAutomationSettingsService, AgentAutomationSettingsService>();
        services.AddScoped<IAiProviderSelectionService, AiProviderSelectionService>();
        services.AddScoped<IEmailApprovalAgent, EmailApprovalAgent>();
        services.AddScoped<IAgentExecutionTokenService, AgentExecutionTokenService>();
        services.AddScoped<IProcessDraftService, ProcessDraftService>();
        services.AddScoped<IProcessHealthReviewService, ProcessHealthReviewService>();
        services.AddScoped<IAgentWorkflowRunner, AgentWorkflowRunner>();
        services.AddScoped<ICurrentUserMailProfileProvider, CurrentUserMailProfileProvider>();
        services.AddScoped<IEmailDraftWorkflowService, EmailDraftWorkflowService>();
        services.AddScoped<IMailClientFactory, SmtpMailClientFactory>();
        services.AddScoped<IMicrosoftGraphMailboxClient, MicrosoftGraphMailboxClient>();
        services.AddScoped<IEmailTaskEvidenceService, EmailTaskEvidenceService>();
        services.AddScoped<IEmailDispatchProcessor, EmailDispatchProcessor>();
        services.AddSingleton<IMailboxSyncLockBroker>(new MailboxSyncLockBroker(crmConnection));
        services.AddScoped<IMailboxSyncProcessor, MailboxSyncProcessor>();
        services.AddScoped<ILeadIngestionService, LeadIngestionService>();
        services.AddScoped<IAuthorityDataImportCoordinationService, AuthorityDataImportCoordinationService>();
        services.AddScoped<ILeadWorkIntakeService, LeadWorkIntakeService>();
        services.AddScoped<IHostedImportClient, HostedImportClient>();
        services.AddScoped<IImportFileWorkspaceService, ImportFileWorkspaceService>();
        services.AddScoped<IImportProcessingService, ImportProcessingService>();
        services.AddScoped<ICrmPlatformBootstrapService, CrmPlatformBootstrapService>();
        services.AddScoped<ICrmDatabaseInitialiser, CrmDatabaseInitialiser>();
        services.AddScoped<IWorkflowAutomationService, WorkflowAutomationService>();
        services.AddScoped<IProcessValidationService, ProcessValidationService>();

        if (options.IncludeMailHostedServices)
        {
            services.AddHostedService<ScheduledEmailSenderHostedService>();
            services.AddHostedService<ScheduledMailboxSyncHostedService>();
        }

        if (options.IncludeAgentHostedServices)
        {
            services.AddHostedService<ScheduledEmailApprovalAgentHostedService>();
            services.AddHostedService<ScheduledTaskAgentHostedService>();
            services.AddHostedService<ScheduledProcessOptimiserHostedService>();
            services.AddHostedService<ScheduledProcessHealthReviewHostedService>();
            services.AddHostedService<ScheduledApprovalConversationHostedService>();
        }

        if (options.IncludeLeadHostedServices)
        {
            services.AddHostedService<ScheduledAuthorityDataIngestHostedService>();
            services.AddHostedService<ScheduledLeadWorkIntakeHostedService>();
        }

        if (options.IncludeImportHostedServices)
        {
            services.AddHostedService<ScheduledImportProcessingHostedService>();
        }

        services.AddCrmPlatform(platformConfiguration =>
        {
            platformConfiguration.ConnectionString = crmConnection;
            platformConfiguration.AdminConnectionString = crmAdminConnection;
        });

        return services;
    }

    static void RegisterNamedAiProviders(
        IConfiguration configuration,
        IConfiguration crmConfiguration,
        AIConfiguration aiConfiguration)
    {
        AiRoutingOptions routing = new();
        crmConfiguration.GetSection(AiRoutingOptions.SectionName).Bind(routing);

        foreach ((string key, AiRoutingProfileOptions profile) in routing.Profiles)
        {
            string providerType = profile.BaseProvider?.Trim() ?? string.Empty;
            if (providerType.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
            {
                aiConfiguration.AddOllama(key, provider =>
                {
                    provider.Endpoint = profile.CompletionEndpoint;
                    provider.ModelEndpoint = profile.ModelEndpoint;
                    provider.Model = profile.Model;
                    provider.MaxConcurrency = profile.MaxConcurrency;
                });
                continue;
            }

            if (providerType.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                aiConfiguration.AddOpenAI(key, provider =>
                {
                    if (!string.IsNullOrWhiteSpace(profile.CompletionEndpoint))
                        provider.Endpoint = profile.CompletionEndpoint;
                    provider.ModelEndpoint = profile.ModelEndpoint;
                    provider.Model = profile.Model;
                    provider.ApiKey = FirstConfiguredValue(
                        profile.ApiKey,
                        configuration[$"AI:Providers:{key}:CompletionProvider:ApiKey"]);
                    provider.MaxConcurrency = profile.MaxConcurrency;
                });
                continue;
            }

            if (providerType.Equals("Codex", StringComparison.OrdinalIgnoreCase)
                || providerType.Equals("CodexCli", StringComparison.OrdinalIgnoreCase))
            {
                aiConfiguration.AddCodex(key, provider =>
                {
                    provider.ExecutablePath = FirstConfiguredValue(
                        profile.ExecutablePath,
                        configuration[$"AI:Providers:{key}:CodexCli:ExecutablePath"],
                        "codex");
                    provider.WorkingDirectory = profile.WorkingDirectory;
                    provider.Model = profile.Model;
                    provider.ReasoningEffort = profile.ReasoningEffort;
                    provider.UseOss = profile.UseOss;
                    provider.LocalProvider = profile.LocalProvider;
                    provider.MaxConcurrency = profile.MaxConcurrency;
                });
                continue;
            }

            if (providerType.Equals("AzureFoundry", StringComparison.OrdinalIgnoreCase)
                || providerType.Equals("Foundry", StringComparison.OrdinalIgnoreCase))
            {
                aiConfiguration.AddFoundry(key, provider =>
                {
                    provider.Endpoint = FirstConfiguredValue(
                        profile.CompletionEndpoint,
                        configuration[$"AI:Providers:{key}:CompletionProvider:Endpoint"]);
                    provider.ModelEndpoint = profile.ModelEndpoint;
                    provider.Model = FirstConfiguredValue(
                        profile.Model,
                        configuration[$"AI:Providers:{key}:CompletionProvider:DefaultModel"]);
                    provider.ApiKey = FirstConfiguredValue(
                        profile.ApiKey,
                        configuration[$"AI:Providers:{key}:CompletionProvider:ApiKey"]);
                    provider.MaxConcurrency = profile.MaxConcurrency;
                });
            }
        }
    }

    static string FirstConfiguredValue(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    static void ApplyString(IConfiguration configuration, string key, Action<string> apply)
    {
        string value = ConfigurationValueResolver.GetOptional(configuration, key);
        if (!string.IsNullOrWhiteSpace(value))
            apply(value);
    }

    static void ApplyFirstString(IConfiguration configuration, Action<string> apply, params string[] keys)
    {
        string value = ConfigurationValueResolver.GetOptional(configuration, keys);
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
