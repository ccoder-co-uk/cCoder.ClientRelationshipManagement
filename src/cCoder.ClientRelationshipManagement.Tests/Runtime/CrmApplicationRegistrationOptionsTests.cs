using cCoder.ClientRelationshipManagement.Runtime;
using cCoder.ClientRelationshipManagement.Runtime.Configuration;
using cCoder.ClientRelationshipManagement.Runtime.Services.Agents;
using cCoder.ClientRelationshipManagement.Runtime.Services.Imports;
using cCoder.ClientRelationshipManagement.Runtime.Services.Leads;
using cCoder.ClientRelationshipManagement.Runtime.Services.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Runtime;

public sealed class CrmApplicationRegistrationOptionsTests
{
    [Fact]
    public void ShouldRegisterOnlySelectedHostedServiceFamilies()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();
        ServiceCollection services = new();

        services.AddCrmApplication(
            configuration,
            "Server=(localdb)\\MSSQLLocalDB;Database=crm-runtime-tests;",
            "Server=(localdb)\\MSSQLLocalDB;Database=crm-runtime-tests;",
            "Server=(localdb)\\MSSQLLocalDB;Database=sso-runtime-tests;",
            "runtime-test-key",
            options =>
            {
                options.IncludeMvc = false;
                options.IncludeAI = false;
                options.IncludeApiDocumentation = false;
                options.IncludeAgentHostedServices = false;
                options.IncludeImportHostedServices = true;
                options.IncludeLeadHostedServices = false;
                options.IncludeMailHostedServices = true;
                options.IncludeSecurity = false;
            });

        Type[] hostedServiceTypes = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(descriptor => descriptor.ImplementationType)
            .ToArray();

        Assert.Contains(typeof(ScheduledEmailSenderHostedService), hostedServiceTypes);
        Assert.Contains(typeof(ScheduledMailboxSyncHostedService), hostedServiceTypes);
        Assert.Contains(typeof(ScheduledImportProcessingHostedService), hostedServiceTypes);
        Assert.DoesNotContain(typeof(ScheduledTaskAgentHostedService), hostedServiceTypes);
        Assert.DoesNotContain(typeof(ScheduledLeadWorkIntakeHostedService), hostedServiceTypes);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName?.StartsWith("cCoder.AI.") == true);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.FullName?.StartsWith("cCoder.Security.") == true);
    }

    [Fact]
    public void ShouldDisableEveryHostedServiceFamily()
    {
        CrmApplicationRegistrationOptions options = new();

        options.IncludeHostedServices = false;

        Assert.False(options.IncludeAgentHostedServices);
        Assert.False(options.IncludeImportHostedServices);
        Assert.False(options.IncludeLeadHostedServices);
        Assert.False(options.IncludeMailHostedServices);
    }

    [Fact]
    public void ShouldAllowIndependentHostedServiceFamilies()
    {
        CrmApplicationRegistrationOptions options = new()
        {
            IncludeAgentHostedServices = false,
            IncludeImportHostedServices = true,
            IncludeLeadHostedServices = false,
            IncludeMailHostedServices = true
        };

        Assert.False(options.IncludeHostedServices);
        Assert.True(options.IncludeImportHostedServices);
        Assert.True(options.IncludeMailHostedServices);
    }
}
