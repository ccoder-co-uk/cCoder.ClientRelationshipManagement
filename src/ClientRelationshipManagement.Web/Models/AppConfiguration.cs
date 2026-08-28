// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

using cCoder.AI.Models.Configurations;
using cCoder.ClientRelationshipManagement.Platform.Models.Configuration;
using cCoder.ClientRelationshipManagement.Runtime.Configuration;
using cCoder.Security.Models;

namespace ClientRelationshipManagement.Web.Models;

public sealed class AppConfiguration
{
    public AIConfiguration AI { get; set; } = new();
    public CRMConfiguration CRM { get; set; } = new();
    public CRMDataConfiguration CRMData { get; set; } = new();
    public SecurityConfiguration Security { get; set; } = new();
    public SecurityDataConfiguration SecurityData { get; set; } = new();
}