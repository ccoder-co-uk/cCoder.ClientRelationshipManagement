// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.ClientRelationshipManagement.Runtime.Brokers.Loggings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace cCoder.ClientRelationshipManagement.Api.Controllers;

public abstract class CrmODataController<TController>(
    ILoggingBroker<TController> loggingBroker)
    : ODataController
    where TController : class
{
    protected IActionResult Complete(IActionResult response)
    {
        int statusCode = response is IStatusCodeActionResult statusCodeResult
            ? statusCodeResult.StatusCode ?? StatusCodes.Status200OK
            : StatusCodes.Status200OK;

        if (statusCode is >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices)
        {
            loggingBroker.LogInformation(
                message: "CRM controller request completed successfully.");
        }
        else if (statusCode is >= StatusCodes.Status400BadRequest and < StatusCodes.Status500InternalServerError)
        {
            loggingBroker.LogWarning(
                message: "CRM controller request completed with status {StatusCode}.",
                args: statusCode);
        }

        return response;
    }

    protected IActionResult Reject(Exception exception)
    {
        loggingBroker.LogWarning(
            message: "CRM controller request was rejected: {FailureReason}",
            args: exception.Message);

        return BadRequest(error: exception.Message);
    }

    protected IActionResult RejectArgument(ArgumentException exception)
    {
        loggingBroker.LogWarning(
            message: "CRM controller request contained an invalid argument: {FailureReason}",
            args: exception.Message);

        return BadRequest(error: "The request is invalid.");
    }

    protected IActionResult RequireAuthentication(Exception exception)
    {
        loggingBroker.LogWarning(
            message: "CRM controller request was unauthenticated: {FailureReason}",
            args: exception.Message);

        return Unauthorized(value: "Authentication is required.");
    }

    protected IActionResult Forbid(Exception exception)
    {
        loggingBroker.LogWarning(
            message: "CRM controller request was forbidden: {FailureReason}",
            args: exception.Message);

        return StatusCode(statusCode: StatusCodes.Status403Forbidden);
    }

    protected IActionResult Fail(Exception exception)
    {
        loggingBroker.LogError(
            exception: exception,
            message: "CRM controller request failed.");

        return StatusCode(statusCode: StatusCodes.Status500InternalServerError);
    }
}
