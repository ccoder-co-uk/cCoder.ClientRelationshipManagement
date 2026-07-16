using ClientRelationshipManagement.Web.Services.Mail;
using FluentAssertions;
using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed class RecipientEmailContentValidatorTests
{
    [Fact]
    public void Validate_AcceptsCompleteRecipientReadyEmail()
    {
        IReadOnlyList<string> errors = RecipientEmailContentValidator.Validate(
            "alex@example.com",
            "A short conversation",
            "Hello Alex,\n\nWould you be open to a short conversation next week?\n\nKind regards,\nPaul");

        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, "Subject", "Hello Alex,\n\nBody", "recipient email address")]
    [InlineData("alex@example.com", "Subject", "Hello ,\n\nBody", "salutation")]
    [InlineData("alex@example.com", "Subject", "Lead with:\n- internal notes", "drafting guidance")]
    [InlineData("alex@example.com", "Subject", "Hello {{Contact.FirstName}},\n\nBody", "template token")]
    public void Validate_RejectsRecipientInvalidEmail(
        string recipients,
        string subject,
        string body,
        string expectedError)
    {
        IReadOnlyList<string> errors = RecipientEmailContentValidator.Validate(recipients, subject, body);

        errors.Should().Contain(error => error.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }
}
