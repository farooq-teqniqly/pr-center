using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Facts;

public sealed class PullRequestIdentityTests
{
    [Theory]
    [InlineData("id")]
    [InlineData("owner")]
    [InlineData("repository")]
    [InlineData("title")]
    [InlineData("url")]
    public void Constructor_WithMissingRequiredArgument_Throws(string nullArgument)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() => ConstructWithNull(nullArgument));
    }

    private static PullRequestIdentity ConstructWithNull(string nullArgument)
    {
        var id = nullArgument == "id" ? null : "owner/repo#1";
        var owner = nullArgument == "owner" ? null : "owner";
        var repository = nullArgument == "repository" ? null : "repo";
        var title = nullArgument == "title" ? null : "Add feature";
        var url = nullArgument == "url" ? null : "https://github.com/owner/repo/pull/1";

        return new PullRequestIdentity(id!, owner!, repository!, number: 1, title!, url!);
    }
}
