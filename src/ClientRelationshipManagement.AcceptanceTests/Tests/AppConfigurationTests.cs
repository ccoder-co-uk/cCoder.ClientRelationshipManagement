// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.ClientRelationshipManagement.Platform.Models.Configuration;
using cCoder.ClientRelationshipManagement.Runtime;
using FluentAssertions;
using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed class AppConfigurationTests
{
    [Theory]
    [InlineData(
        typeof(global::ClientRelationshipManagement.Web.Program),
        "ClientRelationshipManagement.Web.Models.AppConfiguration")]
    [InlineData(
        typeof(global::ClientRelationshipManagement.HostedServices.Program),
        "ClientRelationshipManagement.HostedServices.Models.AppConfiguration")]
    public void HostsShouldExposeStandardApplicationConfiguration(
        Type hostMarker,
        string expectedTypeName)
    {
        Type configurationType = hostMarker.Assembly.GetType(expectedTypeName);

        configurationType.Should().NotBeNull();
        configurationType.GetProperty("CRMData").PropertyType
            .Should().Be(typeof(CRMDataConfiguration));
        configurationType.GetProperty("SecurityData").Should().NotBeNull();
    }

    [Fact]
    public void BusinessRegistrationShouldNotOwnConnectionStrings()
    {
        Type[] parameterTypes = typeof(IServiceCollectionExtensions)
            .GetMethod("AddCrmApplication")
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        parameterTypes.Should().NotContain(typeof(string));
    }
}