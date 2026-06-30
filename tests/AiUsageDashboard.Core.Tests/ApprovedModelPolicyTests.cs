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
    public void Filter_AllowsOnlyTwoJarvis1BedrockModels()
    {
        var sut = new ApprovedModelPolicy([
            new ApprovedModel("aws-bedrock", "us-gov-west-1", "openai.gpt-oss-120b-1:0", "Jarvis Chat", true, true, "Jarvis1"),
            new ApprovedModel("aws-bedrock", "us-gov-west-1", "meta.llama3-70b-instruct-v1:0", "Llama 3 70B", true, true, "Jarvis1")
        ]);

        var filtered = sut.Filter([
            Record("aws-bedrock", "us-gov-west-1", "openai.gpt-oss-120b-1:0"),
            Record("aws-bedrock", "us-gov-west-1", "meta.llama3-70b-instruct-v1:0"),
            Record("aws-bedrock", "us-gov-west-1", "unapproved"),
            Record("azure-openai", "usgovarizona", "deployment"),
            Record("google-vertex", "us-central1", "gemini")
        ]).ToArray();

        Assert.Collection(filtered,
            first => Assert.Equal("openai.gpt-oss-120b-1:0", first.ModelId),
            second => Assert.Equal("meta.llama3-70b-instruct-v1:0", second.ModelId));
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

    private static AiUsageRecord Record(string provider, string region, string modelId) =>
        new(provider, region, modelId, modelId, DateTimeOffset.UtcNow.AddMinutes(-15), DateTimeOffset.UtcNow, 1, 1, 0, 1, 0.01m);
}
