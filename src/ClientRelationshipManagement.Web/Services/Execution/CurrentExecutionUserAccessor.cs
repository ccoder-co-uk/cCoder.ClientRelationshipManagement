namespace ClientRelationshipManagement.Web.Services.Execution;

public sealed class CurrentExecutionUserAccessor : ICurrentExecutionUserAccessor
{
    public string UserId { get; set; }
}
