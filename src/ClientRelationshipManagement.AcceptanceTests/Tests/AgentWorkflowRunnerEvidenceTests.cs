using System.Collections;
using System.Reflection;
using ClientRelationshipManagement.Web.Services.Agents;
using FluentAssertions;
using Xunit;

namespace ClientRelationshipManagement.AcceptanceTests.Tests;

public sealed class AgentWorkflowRunnerEvidenceTests
{
    [Theory]
    [InlineData("bsw.co.uk", "BSWCOUK")]
    [InlineData("www.cmassets.co.uk", "WWWCMASSETSCOUK")]
    public void CompactHostIdentity_PreservesLowercaseDomainLetters(
        string host,
        string expected)
    {
        MethodInfo method = typeof(AgentWorkflowRunner).GetMethod(
            "CompactHostIdentity",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        method.Invoke(null, [host]).Should().Be(expected);
    }

    [Theory]
    [InlineData("J SAINSBURY PLC", "https://corporate.sainsburys.co.uk/", "investor.relations2@sainsburys.co.uk", true)]
    [InlineData("TESCO PLC", "https://www.tescoplc.com/", "investor.relations@tesco.com", true)]
    [InlineData("BARRATT REDROW PLC", "https://www.barrattredrow.co.uk/", "procurement@barrattplc.co.uk", true)]
    [InlineData("BABCOCK INTERNATIONAL GROUP PLC", "https://www.babcockinternational.com/", "BabcockIR@babcockinternational.com", true)]
    [InlineData("BABCOCK INTERNATIONAL GROUP PLC", "https://www.babcockinternational.com/", "shareholderenquiries@cm.mpms.mufg.com", false)]
    public void EmailDomainBelongsToCompany_RejectsThirdPartyMailboxes(
        string companyName,
        string websiteUrl,
        string email,
        bool expected)
    {
        MethodInfo method = typeof(AgentWorkflowRunner).GetMethod(
            "EmailDomainBelongsToCompany",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        method.Invoke(null, [companyName, websiteUrl, email]).Should().Be(expected);
    }

    [Fact]
    public void InferPublishedContactRole_UsesFirstPartyPageContext()
    {
        Type runnerType = typeof(AgentWorkflowRunner);
        Type pageType = runnerType.GetNestedType(
            "FirstPartyQualificationPage",
            BindingFlags.NonPublic);
        object page = Activator.CreateInstance(pageType);
        pageType.GetProperty("Url")!.SetValue(
            page,
            "https://www.babcockinternational.com/investors/ir-contacts/");
        pageType.GetProperty("Title")!.SetValue(
            page,
            "IR Contacts - Babcock International Group");

        MethodInfo method = runnerType.GetMethod(
            "InferPublishedContactRole",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        method.Invoke(null, ["BabcockIR@babcockinternational.com", page])
            .Should().Be("Investor relations");
    }

    [Fact]
    public void FindPublishedContactPhone_RequiresVisibleProximityToEmail()
    {
        Type runnerType = typeof(AgentWorkflowRunner);
        Type pageType = runnerType.GetNestedType(
            "FirstPartyQualificationPage",
            BindingFlags.NonPublic);
        object page = Activator.CreateInstance(pageType);
        pageType.GetProperty("Excerpt")!.SetValue(
            page,
            "Procurement enquiries: procurement@example-company.test. Telephone: 0330 057 6000.");
        pageType.GetProperty("Phones")!.SetValue(
            page,
            new[] { "0403062438", "0330 057 6000" });

        MethodInfo method = runnerType.GetMethod(
            "FindPublishedContactPhone",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        method.Invoke(null, [page, "procurement@example-company.test"])
            .Should().Be("0330 057 6000");

        pageType.GetProperty("Excerpt")!.SetValue(
            page,
            "Investor enquiries: investor@example-company.test.");
        method.Invoke(null, [page, "investor@example-company.test"])
            .Should().Be(string.Empty);
    }

    [Fact]
    public void PageReferencesCompany_AcceptsOfficialEmailDomainWithDistinctLegalToken()
    {
        Type runnerType = typeof(AgentWorkflowRunner);
        Type evidenceType = runnerType.GetNestedType(
            "RelevantContactEvidence",
            BindingFlags.NonPublic);
        Type pageType = runnerType.GetNestedType(
            "RelevantContactPage",
            BindingFlags.NonPublic);
        object evidence = Activator.CreateInstance(evidenceType);
        object page = Activator.CreateInstance(pageType);

        evidenceType.GetProperty("CompanyName")!.SetValue(
            evidence,
            "JOHN MCQUILLAN (CONTRACTS) LIMITED");
        evidenceType.GetProperty("WebsiteUrl")!.SetValue(
            evidence,
            "https://mcqcos.com/");
        pageType.GetProperty("Url")!.SetValue(
            page,
            "https://www.rics.org/research/q3-2025.pdf");
        pageType.GetProperty("Title")!.SetValue(page, "UK Construction Monitor");
        pageType.GetProperty("Excerpt")!.SetValue(
            page,
            "Paul Brogan, McQuillan Companies Limited, Managing Director");
        pageType.GetProperty("Emails")!.SetValue(
            page,
            new[] { "paul.brogan@mcqcos.com" });

        IList pages = (IList)Activator.CreateInstance(evidenceType.GetProperty("Pages")!.PropertyType);
        pages.Add(page);
        evidenceType.GetProperty("Pages")!.SetValue(evidence, pages);

        MethodInfo method = runnerType.GetMethod(
            "PageReferencesCompany",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        method.Invoke(null, [evidence, page]).Should().Be(true);
    }

    [Theory]
    [InlineData("We employ more than 12,000 people across our operations.", 12000)]
    [InlineData("Our team of 850 employees serves customers nationwide.", 850)]
    public void FindEmployeeCountEvidence_ExtractsExplicitFirstPartyStatements(
        string excerpt,
        decimal expected)
    {
        object result = InvokeNumericEvidenceFinder("FindEmployeeCountEvidence", excerpt);

        result.Should().NotBeNull();
        result.GetType().GetProperty("Value")!.GetValue(result).Should().Be(expected);
        result.GetType().GetProperty("SourceUrl")!.GetValue(result).Should().Be("https://example.test/about");
    }

    [Theory]
    [InlineData("Annual revenue of £12.4 million was reported this year.", 12400000, "GBP")]
    [InlineData("Turnover: USD 2.1 billion.", 2100000000, "USD")]
    public void FindAnnualRevenueEvidence_ExtractsExplicitFirstPartyStatements(
        string excerpt,
        decimal expected,
        string currency)
    {
        object result = InvokeNumericEvidenceFinder("FindAnnualRevenueEvidence", excerpt);

        result.Should().NotBeNull();
        result.GetType().GetProperty("Value")!.GetValue(result).Should().Be(expected);
        result.GetType().GetProperty("Currency")!.GetValue(result).Should().Be(currency);
    }

    static object InvokeNumericEvidenceFinder(string methodName, string excerpt)
    {
        Type runnerType = typeof(AgentWorkflowRunner);
        Type pageType = runnerType.GetNestedType("FirstPartyQualificationPage", BindingFlags.NonPublic)!;
        object page = Activator.CreateInstance(pageType)!;
        pageType.GetProperty("Url")!.SetValue(page, "https://example.test/about");
        pageType.GetProperty("Excerpt")!.SetValue(page, excerpt);
        Type listType = typeof(List<>).MakeGenericType(pageType);
        IList pages = (IList)Activator.CreateInstance(listType)!;
        pages.Add(page);
        MethodInfo method = runnerType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)!;
        return method.Invoke(null, [pages]);
    }
}
