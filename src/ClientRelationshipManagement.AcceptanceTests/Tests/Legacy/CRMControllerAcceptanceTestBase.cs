using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using cCoder.ClientRelationshipManagement.Data;
using cCoder.ClientRelationshipManagement.Models.Entities;
using cCoder.ClientRelationshipManagement.Models.Enums;
using ClientRelationshipManagement.AcceptanceTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public abstract class CRMControllerAcceptanceTestBase(CRMAcceptanceFixture fixture)
{
    protected CRMAcceptanceFixture Fixture { get; } = fixture;
    protected HttpClient Client { get; } = fixture.Client;
    protected static JsonSerializerOptions JsonOptions { get; } = new() { PropertyNameCaseInsensitive = true };

    protected static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    protected async Task<T?> GetAsync<T>(string url)
    {
        using HttpResponseMessage response = await Client.GetAsync(url);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return default;

        string content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);

        return JsonSerializer.Deserialize<T>(content, JsonOptions);
    }

    protected async Task<IReadOnlyList<T>> GetListAsync<T>(string url)
    {
        using HttpResponseMessage response = await Client.GetAsync(url);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);

        return JsonSerializer.Deserialize<List<T>>(content, JsonOptions)
            ?? [];
    }

    protected async Task<string> GetStringAsync(string url)
        => await GetStringAsync(Client, url);

    protected static async Task<string> GetStringAsync(HttpClient client, string url)
    {
        using HttpResponseMessage response = await client.GetAsync(url);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);

        return content;
    }

    protected async Task<HttpResponseMessage> PostFormWithAntiforgeryAsync(
        string formUrl,
        string postUrl,
        IReadOnlyDictionary<string, string> formValues)
        => await PostFormWithAntiforgeryAsync(Client, formUrl, postUrl, formValues);

    protected static async Task<HttpResponseMessage> PostFormWithAntiforgeryAsync(
        HttpClient client,
        string formUrl,
        string postUrl,
        IReadOnlyDictionary<string, string> formValues)
    {
        string html = await GetStringAsync(client, formUrl);
        string token = ExtractAntiforgeryToken(html);

        Dictionary<string, string> payload = new(formValues)
        {
            ["__RequestVerificationToken"] = token
        };

        return await client.PostAsync(postUrl, new FormUrlEncodedContent(payload));
    }

    protected async Task<T> PostAsync<T>(string url, object payload)
    {
        using HttpResponseMessage response = await Client.PostAsJsonAsync(url, payload);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);

        return JsonSerializer.Deserialize<T>(content, JsonOptions)!;
    }

    protected async Task<HttpStatusCode> PutAsync(string url, object payload)
    {
        using HttpResponseMessage response = await Client.PutAsJsonAsync(url, payload);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);

        return response.StatusCode;
    }

    protected async Task<HttpStatusCode> DeleteAsync(string url)
    {
        using HttpResponseMessage response = await Client.DeleteAsync(url);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, content);

        return response.StatusCode;
    }

    protected async Task<HttpStatusCode> GetStatusCodeAsync(string url)
    {
        using HttpResponseMessage response = await Client.GetAsync(url);
        return response.StatusCode;
    }

    protected async Task ExecuteInAdminContextAsync(Func<ClientRelationshipManagementDbContext, Task> action)
    {
        using IServiceScope scope = Fixture.Factory.Services.CreateScope();
        using ClientRelationshipManagementDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ICRMContextFactory>().CreateContext(true);

        await action(dbContext);
    }

    protected async Task<TResult> QueryInAdminContextAsync<TResult>(
        Func<ClientRelationshipManagementDbContext, Task<TResult>> action)
    {
        using IServiceScope scope = Fixture.Factory.Services.CreateScope();
        using ClientRelationshipManagementDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ICRMContextFactory>().CreateContext(true);

        return await action(dbContext);
    }

    protected async Task DeleteEntitiesAsync<TEntity>(params Guid[] ids)
        where TEntity : class
    {
        if (ids.Length == 0)
            return;

        await ExecuteInAdminContextAsync(async dbContext =>
        {
            TEntity[] entities =
            [
                .. dbContext.Set<TEntity>()
                    .IgnoreQueryFilters()
                    .Where(entity => ids.Contains(EF.Property<Guid>(entity, "Id")))
            ];

            if (entities.Length == 0)
                return;

            dbContext.RemoveRange(entities);
            await dbContext.SaveChangesAsync();
        });
    }

    protected Client NewClient(Guid? id = null, string? tenantId = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId ?? AcceptanceSettings.TenantId,
            AccountOwner = Unique("owner"),
            Status = RelationshipStatus.Prospect,
            CurrentStage = PipelineStage.Researched,
            Priority = ClientPriority.Medium,
            LeadSource = Unique("lead"),
            InitialRoute = "Research",
            OpportunitySummary = Unique("summary"),
            PreferredOpeningAngle = Unique("angle"),
            NextAction = Unique("next-action"),
            NextActionDueOn = DateTimeOffset.UtcNow.AddDays(7),
            CreatedBy = Fixture.Settings.UserId,
            LastUpdatedBy = Fixture.Settings.UserId,
            CreatedOn = DateTimeOffset.UtcNow,
            LastUpdated = DateTimeOffset.UtcNow,
            IsArchived = false,
        };

    protected Company NewCompany(Guid clientId, Guid? id = null, Guid? registeredAddressId = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            ClientId = clientId,
            Name = Unique("company"),
            LegalEntityName = Unique("legal-entity"),
            TradingName = Unique("trading-name"),
            CompanyNumber = Unique("company-number"),
            VatNumber = Unique("vat-number"),
            ContactEmailAddress = $"{Unique("company")}@example.com",
            ContactPhoneNumber = "01234567890",
            WebsiteUrl = $"https://{Unique("company")}.example.com",
            RegisteredOfficeText = Unique("registered-office"),
            CreatedBy = Fixture.Settings.UserId,
            LastUpdatedBy = Fixture.Settings.UserId,
            CreatedOn = DateTimeOffset.UtcNow,
            LastUpdated = DateTimeOffset.UtcNow,
            IsActive = true,
            IsVerified = false,
            RegisteredAddressId = registeredAddressId,
        };

    protected Address NewAddress(Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            PoBox = Unique("po-box"),
            Line1 = Unique("line1"),
            Line2 = Unique("line2"),
            ZipOrPostalCode = "SO14 0AA",
            TownOrCity = "Southampton",
            StateOrProvince = "Hampshire",
            CountryId = "GB",
            IsActive = true,
            CreatedOn = DateTimeOffset.UtcNow,
            LastUpdated = DateTimeOffset.UtcNow,
        };

    protected ClientContact NewClientContact(Guid clientId, Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            ClientId = clientId,
            Name = Unique("contact"),
            Position = Unique("position"),
            EmailAddress = $"{Unique("contact")}@example.com",
            PhoneNumber = "01234567890",
            LinkedInUrl = $"https://linkedin.example.com/{Unique("contact")}",
            Source = Unique("source"),
            RelationshipRoute = Unique("route"),
            Status = ClientContactStatus.Contacted,
            IsPrimary = false,
            Notes = Unique("notes"),
            CreatedBy = Fixture.Settings.UserId,
            LastUpdatedBy = Fixture.Settings.UserId,
            CreatedOn = DateTimeOffset.UtcNow,
            LastUpdated = DateTimeOffset.UtcNow,
        };

    protected ClientOpportunity NewClientOpportunity(Guid clientId, Guid? id = null, Guid? primaryContactId = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            ClientId = clientId,
            PrimaryContactId = primaryContactId,
            Type = ClientOpportunityType.SupplierPaymentReview,
            Stage = PipelineStage.Researched,
            EstimatedAnnualValue = 12000m,
            Probability = 45m,
            PainSummary = Unique("pain"),
            ValueHypothesis = Unique("value"),
            DecisionProcess = Unique("decision"),
            NextAction = Unique("next-action"),
            NextActionDueOn = DateTimeOffset.UtcNow.AddDays(14),
            CreatedBy = Fixture.Settings.UserId,
            LastUpdatedBy = Fixture.Settings.UserId,
            CreatedOn = DateTimeOffset.UtcNow,
            LastUpdated = DateTimeOffset.UtcNow,
        };

    protected ClientActivity NewClientActivity(
        Guid clientId,
        Guid? id = null,
        Guid? clientContactId = null,
        Guid? clientOpportunityId = null,
        Guid? clientMaterialId = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            ClientId = clientId,
            ClientContactId = clientContactId,
            ClientOpportunityId = clientOpportunityId,
            ClientMaterialId = clientMaterialId,
            ActivityOn = DateTimeOffset.UtcNow,
            Type = ClientActivityType.Email,
            Direction = ClientActivityDirection.Outbound,
            Summary = Unique("summary"),
            Outcome = Unique("outcome"),
            NextAction = Unique("next-action"),
            NextActionDueOn = DateTimeOffset.UtcNow.AddDays(2),
            CreatedBy = Fixture.Settings.UserId,
            CreatedOn = DateTimeOffset.UtcNow,
        };

    protected ClientMaterial NewClientMaterial(Guid clientId, Guid? id = null, Guid? sentToContactId = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            ClientId = clientId,
            SentToContactId = sentToContactId,
            Name = Unique("material"),
            FilePath = $"/crm/{Unique("file")}.pdf",
            Type = ClientMaterialType.Email,
            Status = ClientMaterialStatus.Draft,
            SentOn = null,
            Notes = Unique("notes"),
            CreatedBy = Fixture.Settings.UserId,
            LastUpdatedBy = Fixture.Settings.UserId,
            CreatedOn = DateTimeOffset.UtcNow,
            LastUpdated = DateTimeOffset.UtcNow,
        };

    protected Email NewEmail(
        Guid clientId,
        Guid? id = null,
        Guid? clientMaterialId = null,
        Guid? sentToContactId = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            ClientId = clientId,
            ClientMaterialId = clientMaterialId,
            SentToContactId = sentToContactId,
            SenderUserId = Fixture.Settings.UserId,
            ToAddresses = sentToContactId.HasValue
                ? $"{Unique("recipient")}@example.com"
                : $"{Unique("recipient")}@example.com",
            Subject = Unique("email-subject"),
            BodyHtml = Unique("email-body"),
            BodyText = Unique("email-body"),
            IsBodyHtml = false,
            State = EmailState.Draft,
            CreatedBy = Fixture.Settings.UserId,
            LastUpdatedBy = Fixture.Settings.UserId,
            CreatedOn = DateTimeOffset.UtcNow,
            LastUpdated = DateTimeOffset.UtcNow,
        };

    protected ClientHandoffPack NewClientHandoffPack(Guid clientId, Guid clientOpportunityId, Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            ClientId = clientId,
            ClientOpportunityId = clientOpportunityId,
            SignedContractPath = $"/contracts/{Unique("contract")}.pdf",
            LegalEntity = Unique("legal-entity"),
            PrimaryCommercialContact = Unique("commercial-contact"),
            PrimaryOperationalContact = Unique("operational-contact"),
            PrimaryTechnicalContact = Unique("technical-contact"),
            AgreedScope = Unique("scope"),
            CommercialTermsSummary = Unique("terms"),
            PromisedOutcomes = Unique("outcomes"),
            KnownRisks = Unique("risks"),
            OnboardingOwner = Unique("owner"),
            Status = ClientHandoffStatus.Drafting,
            HandedOffOn = null,
            CreatedBy = Fixture.Settings.UserId,
            LastUpdatedBy = Fixture.Settings.UserId,
            CreatedOn = DateTimeOffset.UtcNow,
            LastUpdated = DateTimeOffset.UtcNow,
        };

    protected static Company ToPayload(Company company) =>
        new()
        {
            Id = company.Id,
            ClientId = company.ClientId,
            Name = company.Name,
            LegalEntityName = company.LegalEntityName,
            TradingName = company.TradingName,
            CompanyNumber = company.CompanyNumber,
            VatNumber = company.VatNumber,
            ContactEmailAddress = company.ContactEmailAddress,
            ContactPhoneNumber = company.ContactPhoneNumber,
            WebsiteUrl = company.WebsiteUrl,
            RegisteredOfficeText = company.RegisteredOfficeText,
            CreatedBy = company.CreatedBy,
            LastUpdatedBy = company.LastUpdatedBy,
            CreatedOn = company.CreatedOn,
            LastUpdated = company.LastUpdated,
            IsActive = company.IsActive,
            IsVerified = company.IsVerified,
            RegisteredAddressId = company.RegisteredAddressId,
        };

    protected static Address ToPayload(Address address, params Company[] companies) =>
        new()
        {
            Id = address.Id,
            PoBox = address.PoBox,
            Line1 = address.Line1,
            Line2 = address.Line2,
            ZipOrPostalCode = address.ZipOrPostalCode,
            TownOrCity = address.TownOrCity,
            StateOrProvince = address.StateOrProvince,
            CountryId = address.CountryId,
            IsActive = address.IsActive,
            CreatedOn = address.CreatedOn,
            LastUpdated = address.LastUpdated,
            Companies = companies.ToList(),
        };

    protected static ClientContact ToPayload(ClientContact clientContact) =>
        new()
        {
            Id = clientContact.Id,
            ClientId = clientContact.ClientId,
            Name = clientContact.Name,
            Position = clientContact.Position,
            EmailAddress = clientContact.EmailAddress,
            PhoneNumber = clientContact.PhoneNumber,
            LinkedInUrl = clientContact.LinkedInUrl,
            Source = clientContact.Source,
            RelationshipRoute = clientContact.RelationshipRoute,
            Status = clientContact.Status,
            IsPrimary = clientContact.IsPrimary,
            Notes = clientContact.Notes,
            CreatedBy = clientContact.CreatedBy,
            LastUpdatedBy = clientContact.LastUpdatedBy,
            CreatedOn = clientContact.CreatedOn,
            LastUpdated = clientContact.LastUpdated,
        };

    protected static ClientOpportunity ToPayload(ClientOpportunity clientOpportunity) =>
        new()
        {
            Id = clientOpportunity.Id,
            ClientId = clientOpportunity.ClientId,
            PrimaryContactId = clientOpportunity.PrimaryContactId,
            Type = clientOpportunity.Type,
            Stage = clientOpportunity.Stage,
            EstimatedAnnualValue = clientOpportunity.EstimatedAnnualValue,
            Probability = clientOpportunity.Probability,
            PainSummary = clientOpportunity.PainSummary,
            ValueHypothesis = clientOpportunity.ValueHypothesis,
            DecisionProcess = clientOpportunity.DecisionProcess,
            NextAction = clientOpportunity.NextAction,
            NextActionDueOn = clientOpportunity.NextActionDueOn,
            CreatedBy = clientOpportunity.CreatedBy,
            LastUpdatedBy = clientOpportunity.LastUpdatedBy,
            CreatedOn = clientOpportunity.CreatedOn,
            LastUpdated = clientOpportunity.LastUpdated,
        };

    protected static ClientActivity ToPayload(ClientActivity clientActivity) =>
        new()
        {
            Id = clientActivity.Id,
            ClientId = clientActivity.ClientId,
            ClientContactId = clientActivity.ClientContactId,
            ClientOpportunityId = clientActivity.ClientOpportunityId,
            ClientMaterialId = clientActivity.ClientMaterialId,
            ActivityOn = clientActivity.ActivityOn,
            Type = clientActivity.Type,
            Direction = clientActivity.Direction,
            Summary = clientActivity.Summary,
            Outcome = clientActivity.Outcome,
            NextAction = clientActivity.NextAction,
            NextActionDueOn = clientActivity.NextActionDueOn,
            CreatedBy = clientActivity.CreatedBy,
            CreatedOn = clientActivity.CreatedOn,
        };

    protected static ClientMaterial ToPayload(ClientMaterial clientMaterial) =>
        new()
        {
            Id = clientMaterial.Id,
            ClientId = clientMaterial.ClientId,
            SentToContactId = clientMaterial.SentToContactId,
            Name = clientMaterial.Name,
            FilePath = clientMaterial.FilePath,
            Type = clientMaterial.Type,
            Status = clientMaterial.Status,
            SentOn = clientMaterial.SentOn,
            Notes = clientMaterial.Notes,
            CreatedBy = clientMaterial.CreatedBy,
            LastUpdatedBy = clientMaterial.LastUpdatedBy,
            CreatedOn = clientMaterial.CreatedOn,
            LastUpdated = clientMaterial.LastUpdated,
        };

    protected static Email ToPayload(Email email) =>
        new()
        {
            Id = email.Id,
            ClientId = email.ClientId,
            ClientMaterialId = email.ClientMaterialId,
            SentToContactId = email.SentToContactId,
            SenderUserId = email.SenderUserId,
            FromDisplayName = email.FromDisplayName,
            FromEmailAddress = email.FromEmailAddress,
            ReplyToAddresses = email.ReplyToAddresses,
            ToAddresses = email.ToAddresses,
            CcAddresses = email.CcAddresses,
            BccAddresses = email.BccAddresses,
            Subject = email.Subject,
            BodyHtml = email.BodyHtml,
            BodyText = email.BodyText,
            IsBodyHtml = email.IsBodyHtml,
            State = email.State,
            ApprovedOn = email.ApprovedOn,
            ApprovedBy = email.ApprovedBy,
            ScheduledSendTimeUtc = email.ScheduledSendTimeUtc,
            LastSendAttemptOn = email.LastSendAttemptOn,
            SentOn = email.SentOn,
            ExternalMessageId = email.ExternalMessageId,
            LastError = email.LastError,
            SendFailureCount = email.SendFailureCount,
            CreatedBy = email.CreatedBy,
            LastUpdatedBy = email.LastUpdatedBy,
            CreatedOn = email.CreatedOn,
            LastUpdated = email.LastUpdated,
        };

    protected static ClientHandoffPack ToPayload(ClientHandoffPack clientHandoffPack) =>
        new()
        {
            Id = clientHandoffPack.Id,
            ClientId = clientHandoffPack.ClientId,
            ClientOpportunityId = clientHandoffPack.ClientOpportunityId,
            SignedContractPath = clientHandoffPack.SignedContractPath,
            LegalEntity = clientHandoffPack.LegalEntity,
            PrimaryCommercialContact = clientHandoffPack.PrimaryCommercialContact,
            PrimaryOperationalContact = clientHandoffPack.PrimaryOperationalContact,
            PrimaryTechnicalContact = clientHandoffPack.PrimaryTechnicalContact,
            AgreedScope = clientHandoffPack.AgreedScope,
            CommercialTermsSummary = clientHandoffPack.CommercialTermsSummary,
            PromisedOutcomes = clientHandoffPack.PromisedOutcomes,
            KnownRisks = clientHandoffPack.KnownRisks,
            OnboardingOwner = clientHandoffPack.OnboardingOwner,
            Status = clientHandoffPack.Status,
            HandedOffOn = clientHandoffPack.HandedOffOn,
            CreatedBy = clientHandoffPack.CreatedBy,
            LastUpdatedBy = clientHandoffPack.LastUpdatedBy,
            CreatedOn = clientHandoffPack.CreatedOn,
            LastUpdated = clientHandoffPack.LastUpdated,
        };

    static string ExtractAntiforgeryToken(string html)
    {
        Match match = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        match.Success.Should().BeTrue("the MVC form should include an antiforgery token");
        return match.Groups[1].Value;
    }
}
