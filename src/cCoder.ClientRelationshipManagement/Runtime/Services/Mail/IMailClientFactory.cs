namespace cCoder.ClientRelationshipManagement.Runtime.Services.Mail;

public interface IMailClientFactory
{
    IMailClient CreateClient();
}
