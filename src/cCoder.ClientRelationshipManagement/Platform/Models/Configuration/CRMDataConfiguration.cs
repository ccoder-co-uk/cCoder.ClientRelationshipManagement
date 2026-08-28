// ---------------------------------------------------------------
// Copyright (c) Paul.Ward@ccoder.co.uk
// ---------------------------------------------------------------

namespace cCoder.ClientRelationshipManagement.Platform.Models.Configuration;

public sealed class CRMDataConfiguration
{
    public const string SectionName = "CRMData";

    public string ConnectionString { get; set; } = string.Empty;
    public string AdminConnectionString { get; set; } = string.Empty;
}