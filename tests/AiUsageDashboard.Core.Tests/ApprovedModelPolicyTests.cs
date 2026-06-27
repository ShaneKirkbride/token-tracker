using AiUsageDashboard.Contracts;
using AiUsageDashboard.Core;

namespace AiUsageDashboard.Core.Tests;

public sealed class ApprovedModelPolicyTests
{
    [Fact]
    public void IsApproved_ReturnsTrueForApprovedModelIgnoringCase()
    {
        var sut = new ApprovedModelPolicy([
            new ApprovedModel("aws-bedrock", "us-gov-west-1", "model-a", "A", true, true, "GovCloud")
        ]);

        Assert.True(sut.IsApproved("AWS-BEDROCK", "US-GOV-WEST-1", "MODEL-A"));
    }

    [Fact]
    public void IsApproved_ReturnsFalseForUnapprovedOrUnknownModel()
    {
        var sut = new ApprovedModelPolicy([
            new ApprovedModel("aws-bedrock", "us-gov-west-1", "model-a", "A", false, true, "GovCloud")
        ]);

        Assert.False(sut.IsApproved("aws-bedrock", "us-gov-west-1", "model-a"));
        Assert.False(sut.IsApproved("aws-bedrock", "us-gov-west-1", "model-b"));
    }

    [Theory]
    [InlineData("", "region", "model")]
    [InlineData("provider", "", "model")]
    [InlineData("provider", "region", "")]
    public void IsApproved_RejectsBlankArguments(string provider, string region, string model)
    {
        var sut = new ApprovedModelPolicy([]);

        Assert.Throws<ArgumentException>(() => sut.IsApproved(provider, region, model));
    }
}
