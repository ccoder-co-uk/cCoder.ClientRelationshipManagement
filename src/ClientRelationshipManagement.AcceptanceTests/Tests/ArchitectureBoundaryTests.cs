using FluentAssertions;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed class ArchitectureBoundaryTests
{
    static readonly string[] ExistingControllerExceptions = [];

    static readonly string[] ExistingServiceExceptions = [];

    [Fact]
    public void MvcControllers_DoNotAddNewDirectPlatformDatabaseDependencies()
    {
        string directory = FindRepositoryDirectory("src", "ClientRelationshipManagement.Web", "Controllers");
        string[] offenders = Directory.GetFiles(directory, "*.cs")
            .Where(ContainsPlatformDatabaseDependency)
            .Select(Path.GetFileName)
            .OrderBy(name => name)
            .ToArray();

        offenders.Should().Equal(ExistingControllerExceptions.OrderBy(name => name),
            "the exception list is a migration ratchet: remove a filename as its controller moves to shared domain services");
    }

    [Fact]
    public void CanonicalApiExposers_NeverAccessPlatformDatabaseDirectly()
    {
        string directory = FindRepositoryDirectory(
            "src", "cCoder.ClientRelationshipManagement", "Api", "Controllers");
        string[] offenders = Directory.GetFiles(directory, "*.cs")
            .Where(ContainsPlatformDatabaseDependency)
            .Select(Path.GetFileName)
            .ToArray();

        offenders.Should().BeEmpty("OData exposers may depend only on shared domain services");
    }

    [Fact]
    public void WebServices_DoNotAddNewDirectPlatformDatabaseDependencies()
    {
        string directory = FindRepositoryDirectory("src", "ClientRelationshipManagement.Web", "Services");
        string[] offenders = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Migration{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(ContainsPlatformDatabaseDependency)
            .Select(Path.GetFileName)
            .OrderBy(name => name)
            .ToArray();

        offenders.Should().Equal(ExistingServiceExceptions.OrderBy(name => name),
            "the exception list is a migration ratchet: remove services as persistence moves behind domain brokers");
    }

    static bool ContainsPlatformDatabaseDependency(string path)
    {
        string source = File.ReadAllText(path);
        return source.Contains("ClientRelationshipDbContext", StringComparison.Ordinal)
            || source.Contains("IClientRelationshipDbContextFactory", StringComparison.Ordinal);
    }

    [Fact]
    public void TransitionalPersistenceTypes_AreAbsent()
    {
        string root = FindRepositoryDirectory("src", "cCoder.ClientRelationshipManagement");
        string[] forbidden =
        [
            "EntityBroker<", "EntityService<", "EntityODataController<", "AuditableEntity",
            "PlatformDbContext", "PlatformConfiguration", "SalesWorkspaceBroker",
            "ProcessWorkspaceBroker", "OperationsBroker", "ImportBroker"
        ];

        string[] offenders = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => forbidden.Any(token => File.ReadAllText(path).Contains(token, StringComparison.Ordinal)))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void EveryOperationalEntity_HasConcreteVerticalStackAndCrudEvents()
    {
        string root = FindRepositoryDirectory("src", "cCoder.ClientRelationshipManagement");
        string entityDirectory = Path.Combine(root, "Platform", "Models", "Entities");
        string stackDirectory = Path.Combine(root, "Services", "Entities");
        string controllerDirectory = Path.Combine(root, "Api", "Controllers");

        string[] entityNames = Directory.GetFiles(entityDirectory, "*.cs")
            .Where(path => Path.GetFileName(path) != "ICrmEntity.cs")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .ToArray();

        foreach (string entity in entityNames)
        {
            string stack = File.ReadAllText(Path.Combine(stackDirectory, $"{entity}.cs"));
            stack.Should().Contain($"I{entity}StorageBroker");
            stack.Should().Contain($"I{entity}FoundationService");
            stack.Should().Contain($"I{entity}ProcessingService");
            stack.Should().Contain($"I{entity}OrchestrationService");
            stack.Should().Contain($"I{entity}EventBroker");
            stack.Should().Contain("_add");
            stack.Should().Contain("_update");
            stack.Should().Contain("_delete");

            string controller = Directory.GetFiles(controllerDirectory, "*Controller.cs")
                .Select(File.ReadAllText)
                .Single(source => source.Contains($"I{entity}OrchestrationService", StringComparison.Ordinal));
            controller.Should().Contain("public IActionResult Get()");
            controller.Should().Contain("Post(");
            controller.Should().Contain("Put(");
            controller.Should().Contain("Patch(");
            controller.Should().Contain("Delete(");
        }
    }

    static string FindRepositoryDirectory(params string[] segments)
    {
        DirectoryInfo current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string candidate = Path.Combine([current.FullName, .. segments]);
            if (Directory.Exists(candidate)) return candidate;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate repository path '{Path.Combine(segments)}'.");
    }
}
