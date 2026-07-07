$items = @(
    [pscustomobject]@{
        ClientId = 'A32CDDC7-28C4-4E40-ACCD-54BC9EB0E3E5'
        OpportunityId = 'DF132FE8-A78F-4155-AC67-383DA9814F28'
        Subject = 'Possible supplier payment visibility and working capital review at Balfour Beatty'
        NextAction = 'Review and approve whether to send procurement-led first outreach to Evan Sutherland or use a softer referral route via Jim Ryan'
        DueOn = [DateTimeOffset]'2026-06-16T00:00:00+00:00'
        Body = @'
To: Evan Sutherland, Chief Procurement Officer
Email: evan.sutherland@balfourbeatty.com
Status: Ready for review

Good afternoon Mr Sutherland,

Given Balfour Beatty's scale, record order book, and the size of its subcontractor and supplier estate, there may be value in a short review focused on supplier payment visibility, finance-process friction, and any working capital opportunity linked to that.

Corporate LinX typically helps larger organisations improve visibility over invoice and payment status, reduce avoidable supplier and subcontractor chasing, and identify practical process or cash benefits without forcing major systems change.

Would it be worth a short conversation to see whether that would be relevant for Balfour Beatty, or to rule it out quickly?

Best regards,

Paul Ward
Corporate LinX
+44 (0)7450 233 683
'@
    }
    [pscustomobject]@{
        ClientId = 'A0D74181-4CC8-4E97-826D-883A3D1D7323'
        OpportunityId = 'E7504A88-3B2A-4101-9E5F-6CBEB8395957'
        Subject = 'Possible supplier payment visibility and working capital review at Mitie'
        NextAction = 'Review and approve first outreach to Simon Kirkpatrick, then decide whether to loop in procurement if finance redirects'
        DueOn = [DateTimeOffset]'2026-06-16T00:00:00+00:00'
        Body = @'
To: Simon Kirkpatrick, Chief Financial Officer
Email: simon.kirkpatrick@mitie.com
Status: Ready for review

Good afternoon Mr Kirkpatrick,

Given Mitie's scale and the volume of supplier and subcontractor payments moving through the business, there may be value in a short review focused on supplier payment visibility, finance-process friction, and any working capital opportunity linked to that.

Corporate LinX typically helps larger organisations improve visibility over invoice and payment status, reduce avoidable supplier-query and process cost, and identify practical working capital or bottom-line improvement opportunities without forcing major systems change.

Would it be worth a short conversation to see whether that would be relevant for Mitie, or to rule it out quickly?

Best regards,

Paul Ward
Corporate LinX
+44 (0)7450 233 683
'@
    }
    [pscustomobject]@{
        ClientId = '50372D65-1010-436C-A209-71E524507F64'
        OpportunityId = '40E5D354-59F2-4D0A-AD74-E255F9248DA4'
        Subject = 'Possible supplier payment visibility and working capital review at Southern Water'
        NextAction = 'Review and approve first outreach to Stuart Ledger, keeping the message operational and low-hype'
        DueOn = [DateTimeOffset]'2026-06-16T00:00:00+00:00'
        Body = @'
To: Stuart Ledger, Chief Financial Officer
Email: stuart.ledger@southernwater.co.uk
Status: Ready for review

Good afternoon Mr Ledger,

Given Southern Water's scale, contractor base, and stepped-up investment cycle, there may be value in a short review focused on supplier payment visibility, finance-process friction, and any working capital opportunity linked to that.

Corporate LinX typically helps larger organisations improve visibility over invoice and payment status, reduce avoidable contractor and supplier chasing, and identify practical process or cash benefits without forcing major systems change.

Would it be worth a short conversation to see whether that would be relevant for Southern Water, or to rule it out quickly?

Best regards,

Paul Ward
Corporate LinX
+44 (0)7450 233 683
'@
    }
    [pscustomobject]@{
        ClientId = 'DF4D1C7D-4915-4C20-8D0D-85C4603F7E03'
        OpportunityId = 'D157C10B-904B-4370-B7E3-854315F27305'
        Subject = 'Possible supplier payment visibility and working capital review at Thames Water'
        NextAction = 'Review and approve whether Thames Water is worth a direct CFO approach tomorrow or should be held as a higher-risk but high-value target'
        DueOn = [DateTimeOffset]'2026-06-16T00:00:00+00:00'
        Body = @'
To: Steve Buck, Chief Financial Officer
Email: steve.buck@thameswater.co.uk
Status: Ready for review

Good afternoon Mr Buck,

Given Thames Water's scale, supplier complexity, and ongoing capital-delivery pressure, there may be value in a short review focused on supplier payment visibility, finance-process friction, and any working capital opportunity linked to that.

Corporate LinX typically helps larger organisations improve visibility over invoice and payment status, reduce avoidable contractor and supplier chasing, and identify practical process or cash benefits without forcing major systems change.

Would it be worth a short conversation to see whether that would be relevant for Thames Water, or to rule it out quickly?

Best regards,

Paul Ward
Corporate LinX
+44 (0)7450 233 683
'@
    }
    [pscustomobject]@{
        ClientId = '296EDF40-918D-4BA4-8D5E-E17BD0AFDDE9'
        OpportunityId = 'F9181796-1C76-40E9-A8A9-AFB2101A521F'
        Subject = 'Possible supplier payment visibility and working capital review at Wates'
        NextAction = 'Review and approve first outreach to Philip Wainwright, with Rosie Toogood held as the supply-chain route if finance redirects'
        DueOn = [DateTimeOffset]'2026-06-16T00:00:00+00:00'
        Body = @'
To: Philip Wainwright, Chief Financial Officer
Email: philip.wainwright@wates.co.uk
Status: Ready for review

Good afternoon Mr Wainwright,

Given Wates' scale, forward order book, and the size of its subcontractor and supplier base, there may be value in a short review focused on supplier payment visibility, finance-process friction, and any working capital opportunity linked to that.

Corporate LinX typically helps larger organisations improve visibility over invoice and payment status, reduce avoidable supplier chasing across finance and supply-chain teams, and identify practical process or cash benefits without forcing major systems change.

Would it be worth a short conversation to see whether that would be relevant for Wates, or to rule it out quickly?

Best regards,

Paul Ward
Corporate LinX
+44 (0)7450 233 683
'@
    }
)

$connection = New-Object System.Data.SqlClient.SqlConnection 'Server=.;Database=CRM;Integrated Security=True;TrustServerCertificate=True;'
$connection.Open()

try {
    foreach ($item in $items) {
        $now = [DateTimeOffset]::UtcNow

        $select = $connection.CreateCommand()
        $select.CommandText = @"
SELECT TOP 1 m.Id AS MaterialId, a.Id AS ActivityId
FROM ClientRelationshipManagement.Web.ClientMaterials m
JOIN ClientRelationshipManagement.Web.ClientActivities a ON a.ClientMaterialId = m.Id
WHERE m.ClientId = @ClientId
  AND a.ClientOpportunityId = @OpportunityId
  AND a.NextAction = @NextAction
  AND a.NextActionDueOn = @DueOn
  AND m.Type = 'Email'
  AND m.Status IN ('Draft', 'Ready');
"@
        [void]$select.Parameters.AddWithValue('@ClientId', [Guid]$item.ClientId)
        [void]$select.Parameters.AddWithValue('@OpportunityId', [Guid]$item.OpportunityId)
        [void]$select.Parameters.AddWithValue('@NextAction', $item.NextAction)
        [void]$select.Parameters.AddWithValue('@DueOn', $item.DueOn)

        $reader = $select.ExecuteReader()
        $existingMaterialId = $null
        $existingActivityId = $null

        if ($reader.Read()) {
            $existingMaterialId = [Guid]$reader['MaterialId']
            $existingActivityId = [Guid]$reader['ActivityId']
        }

        $reader.Close()

        if ($existingMaterialId) {
            $updateMaterial = $connection.CreateCommand()
            $updateMaterial.CommandText = @"
UPDATE ClientRelationshipManagement.Web.ClientMaterials
SET Name = @Subject,
    Type = 'Email',
    Status = 'Ready',
    SentOn = NULL,
    Notes = @Body,
    LastUpdatedBy = 'Paul',
    LastUpdated = @Now
WHERE Id = @MaterialId;
"@
            [void]$updateMaterial.Parameters.AddWithValue('@Subject', $item.Subject)
            [void]$updateMaterial.Parameters.AddWithValue('@Body', $item.Body)
            [void]$updateMaterial.Parameters.AddWithValue('@Now', $now)
            [void]$updateMaterial.Parameters.AddWithValue('@MaterialId', $existingMaterialId)
            [void]$updateMaterial.ExecuteNonQuery()

            $updateActivity = $connection.CreateCommand()
            $updateActivity.CommandText = @"
UPDATE ClientRelationshipManagement.Web.ClientActivities
SET ActivityOn = @Now,
    Type = 'Email',
    Direction = 'Outbound',
    Summary = @Subject,
    Outcome = @Body,
    NextAction = @NextAction,
    NextActionDueOn = @DueOn
WHERE Id = @ActivityId;
"@
            [void]$updateActivity.Parameters.AddWithValue('@Now', $now)
            [void]$updateActivity.Parameters.AddWithValue('@Subject', $item.Subject)
            [void]$updateActivity.Parameters.AddWithValue('@Body', $item.Body)
            [void]$updateActivity.Parameters.AddWithValue('@NextAction', $item.NextAction)
            [void]$updateActivity.Parameters.AddWithValue('@DueOn', $item.DueOn)
            [void]$updateActivity.Parameters.AddWithValue('@ActivityId', $existingActivityId)
            [void]$updateActivity.ExecuteNonQuery()

            Write-Output "Updated draft for $($item.Subject)"
            continue
        }

        $materialId = [Guid]::NewGuid()
        $activityId = [Guid]::NewGuid()

        $insertMaterial = $connection.CreateCommand()
        $insertMaterial.CommandText = @"
INSERT INTO ClientRelationshipManagement.Web.ClientMaterials
    (Id, ClientId, SentToContactId, Name, FilePath, Type, Status, SentOn, Notes, CreatedBy, LastUpdatedBy, CreatedOn, LastUpdated)
VALUES
    (@MaterialId, @ClientId, NULL, @Subject, '', 'Email', 'Ready', NULL, @Body, 'Paul', 'Paul', @Now, @Now);
"@
        [void]$insertMaterial.Parameters.AddWithValue('@MaterialId', $materialId)
        [void]$insertMaterial.Parameters.AddWithValue('@ClientId', [Guid]$item.ClientId)
        [void]$insertMaterial.Parameters.AddWithValue('@Subject', $item.Subject)
        [void]$insertMaterial.Parameters.AddWithValue('@Body', $item.Body)
        [void]$insertMaterial.Parameters.AddWithValue('@Now', $now)
        [void]$insertMaterial.ExecuteNonQuery()

        $insertActivity = $connection.CreateCommand()
        $insertActivity.CommandText = @"
INSERT INTO ClientRelationshipManagement.Web.ClientActivities
    (Id, ClientId, ClientContactId, ClientOpportunityId, ClientMaterialId, ActivityOn, Type, Direction, Summary, Outcome, NextAction, NextActionDueOn, CreatedBy, CreatedOn)
VALUES
    (@ActivityId, @ClientId, NULL, @OpportunityId, @MaterialId, @Now, 'Email', 'Outbound', @Subject, @Body, @NextAction, @DueOn, 'Paul', @Now);
"@
        [void]$insertActivity.Parameters.AddWithValue('@ActivityId', $activityId)
        [void]$insertActivity.Parameters.AddWithValue('@ClientId', [Guid]$item.ClientId)
        [void]$insertActivity.Parameters.AddWithValue('@OpportunityId', [Guid]$item.OpportunityId)
        [void]$insertActivity.Parameters.AddWithValue('@MaterialId', $materialId)
        [void]$insertActivity.Parameters.AddWithValue('@Now', $now)
        [void]$insertActivity.Parameters.AddWithValue('@Subject', $item.Subject)
        [void]$insertActivity.Parameters.AddWithValue('@Body', $item.Body)
        [void]$insertActivity.Parameters.AddWithValue('@NextAction', $item.NextAction)
        [void]$insertActivity.Parameters.AddWithValue('@DueOn', $item.DueOn)
        [void]$insertActivity.ExecuteNonQuery()

        Write-Output "Inserted draft for $($item.Subject)"
    }
}
finally {
    $connection.Close()
}
