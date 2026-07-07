namespace ClientRelationshipManagement.Web.Services.Mail;

public interface IMailClientFactory
{
    IMailClient CreateClient();
}
