using cCoder.ClientRelationshipManagement.Api.OData;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace cCoder.ClientRelationshipManagement.Api;

public static class ClientRelationshipManagementApiExtensions
{
    public static IMvcBuilder AddClientRelationshipManagementApi(this IMvcBuilder mvc)
    {
        mvc.AddApplicationPart(typeof(ClientRelationshipManagementApiExtensions).Assembly)
            .AddOData(options => options
                .Expand().Count().Filter().Select().OrderBy().SetMaxTop(1000)
                .AddRouteComponents("Api/ClientRelationshipManagement",
                    new ClientRelationshipManagementModelBuilder().Build()));
        return mvc;
    }

    public static IServiceCollection AddClientRelationshipManagementApiDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name);
            options.ResolveConflictingActions(descriptions => descriptions.First());
            options.DocInclusionPredicate((documentName, description) =>
                string.Equals(documentName, "ClientRelationshipManagement", StringComparison.OrdinalIgnoreCase)
                && description.RelativePath?.StartsWith(
                    "Api/ClientRelationshipManagement",
                    StringComparison.OrdinalIgnoreCase) == true);
            options.SwaggerDoc("ClientRelationshipManagement", new OpenApiInfo
            {
                Title = "Client Relationship Management API",
                Version = "ClientRelationshipManagement",
                Description = "User-authorised CRM OData context. Requests execute with the access of the bearer-token user."
            });
        });
        return services;
    }
}
