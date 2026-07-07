using ClientRelationshipManagement.AcceptanceTests.Infrastructure;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

[Collection(CRMAcceptanceCollection.Name)]
public sealed partial class ClientsControllerTests(CRMAcceptanceFixture fixture)
    : CRMControllerAcceptanceTestBase(fixture);
