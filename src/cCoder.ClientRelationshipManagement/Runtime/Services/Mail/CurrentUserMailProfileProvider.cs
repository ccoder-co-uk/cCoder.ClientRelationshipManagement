using cCoder.Security.Data.EF.Interfaces;
using cCoder.Security.Models;
using cCoder.Security.Models.Configurations;
using cCoder.Security.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace cCoder.ClientRelationshipManagement.Runtime.Services.Mail;

public sealed class CurrentUserMailProfileProvider(
    ISecurityDbContextFactory securityDbContextFactory,
    ISSOAuthInfo authInfo)
    : ICurrentUserMailProfileProvider
{
    public ValueTask<MailSenderProfile> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        GetByUserIdAsync(authInfo?.SSOUserId, cancellationToken);

    public async ValueTask<MailSenderProfile> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        using var dbContext = securityDbContextFactory.CreateDbContext(ignoreAuthInfo: true);
        SSOUser user = await dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == userId, cancellationToken);

        if (user is null)
            return null;

        return new MailSenderProfile
        {
            UserId = user.Id,
            DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Id : user.DisplayName,
            EmailAddress = user.Email,
        };
    }
}
