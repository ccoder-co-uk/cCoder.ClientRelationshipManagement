// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security;
using System.Security.Authentication;
using cCoder.ClientRelationshipManagement.Api.Controllers;
using cCoder.ClientRelationshipManagement.Platform.Models.Entities;
using cCoder.ClientRelationshipManagement.Runtime.Brokers.Loggings;
using cCoder.ClientRelationshipManagement.Services.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.OData.Results;
using Moq;
using Xunit;

namespace cCoder.ClientRelationshipManagement.Tests.Api;

public sealed partial class ProcessDefinitionsControllerContractTests
{
    readonly Mock<IProcessDefinitionOrchestrationService> orchestration = new();
    readonly Mock<ILoggingBroker<ProcessDefinitionsController>> logging = new();

    [Fact]
    public async Task Post_WhenCreateSucceeds_ShouldReturnCreatedAndLogInformation()
    {
        // Given
        ProcessDefinition entity = new() { Id = Guid.NewGuid() };
        orchestration
            .Setup(service => service.AddAsync(entity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        ProcessDefinitionsController controller = CreateController();

        // When
        IActionResult result = await controller.Post(entity, CancellationToken.None);

        // Then
        CreatedODataResult<ProcessDefinition> response =
            Assert.IsType<CreatedODataResult<ProcessDefinition>>(result);

        Assert.Same(entity, response.Entity);
        logging.Verify(
            broker => broker.LogInformation(
                It.IsAny<string>(),
                It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task Post_WhenValidationFails_ShouldReturnReasonAndLogWarning()
    {
        // Given
        ValidationException failure = new("Process name is required.");
        orchestration
            .Setup(service => service.AddAsync(
                It.IsAny<ProcessDefinition>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromException<ProcessDefinition>(failure));
        ProcessDefinitionsController controller = CreateController();

        // When
        IActionResult result = await controller.Post(
            new ProcessDefinition(),
            CancellationToken.None);

        // Then
        BadRequestObjectResult response = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(failure.Message, response.Value);
        logging.Verify(
            broker => broker.LogWarning(
                It.IsAny<string>(),
                It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task Post_WhenWriteIsForbidden_ShouldReturnForbiddenAndLogWarning()
    {
        // Given
        SecurityException failure = new("Tenant write access is required.");
        orchestration
            .Setup(service => service.AddAsync(
                It.IsAny<ProcessDefinition>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromException<ProcessDefinition>(failure));
        ProcessDefinitionsController controller = CreateController();

        // When
        IActionResult result = await controller.Post(
            new ProcessDefinition(),
            CancellationToken.None);

        // Then
        Assert.Equal(
            StatusCodes.Status403Forbidden,
            Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
        logging.Verify(
            broker => broker.LogWarning(
                It.IsAny<string>(),
                It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task Post_WhenAuthenticationFails_ShouldReturnUnauthorizedAndLogWarning()
    {
        // Given
        AuthenticationException failure = new("Authentication failed.");
        orchestration
            .Setup(service => service.AddAsync(
                It.IsAny<ProcessDefinition>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromException<ProcessDefinition>(failure));
        ProcessDefinitionsController controller = CreateController();

        // When
        IActionResult result = await controller.Post(
            new ProcessDefinition(),
            CancellationToken.None);

        // Then
        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
        logging.Verify(
            broker => broker.LogWarning(
                It.IsAny<string>(),
                It.IsAny<object[]>()),
            Times.Once);
    }

    [Fact]
    public async Task Post_WhenCreateFailsUnexpectedly_ShouldReturnServerErrorAndLogException()
    {
        // Given
        InvalidOperationException failure = new("Storage unavailable.");
        orchestration
            .Setup(service => service.AddAsync(
                It.IsAny<ProcessDefinition>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromException<ProcessDefinition>(failure));
        ProcessDefinitionsController controller = CreateController();

        // When
        IActionResult result = await controller.Post(
            new ProcessDefinition(),
            CancellationToken.None);

        // Then
        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
        logging.Verify(
            broker => broker.LogError(
                failure,
                It.IsAny<string>(),
                It.IsAny<object[]>()),
            Times.Once);
    }

    ProcessDefinitionsController CreateController()
    {
        ConstructorInfo constructor = typeof(ProcessDefinitionsController)
            .GetConstructors()
            .Single();
        object[] arguments = constructor
            .GetParameters()
            .Select(parameter => (object)(parameter.ParameterType ==
                typeof(IProcessDefinitionOrchestrationService)
                    ? orchestration.Object
                    : parameter.ParameterType ==
                        typeof(ILoggingBroker<ProcessDefinitionsController>)
                        ? logging.Object
                        : throw new InvalidOperationException(
                            $"Unexpected dependency '{parameter.ParameterType}'.")))
            .ToArray();

        return (ProcessDefinitionsController)constructor.Invoke(arguments);
    }
}
