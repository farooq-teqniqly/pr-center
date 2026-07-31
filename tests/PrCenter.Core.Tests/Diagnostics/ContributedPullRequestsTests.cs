using PrCenter.Core.Diagnostics;
using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Diagnostics;

public sealed class ContributedPullRequestsTests
{
    [Fact]
    public void Constructor_WithNullIds_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new ContributedPullRequests(null!, 0));
    }

    [Fact]
    public void Constructor_DoesNotObserveLaterMutationOfSourceList()
    {
        // Arrange
        var ids = new List<string> { "acme/api#1" };
        var contributed = new ContributedPullRequests(ids, 0);

        // Act
        ids.Add("acme/api#2");

        // Assert
        Assert.Single(contributed.Ids);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void For_WithMissingOwner_Throws(string? owner)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() => ContributedPullRequests.For(owner!, []));
    }

    [Fact]
    public void For_WithNullIdentities_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => ContributedPullRequests.For("acme", null!));
    }

    [Fact]
    public void For_FormatsEachIdentityAsOwnerRepositoryAndNumber()
    {
        // Act
        var contributed = ContributedPullRequests.For(
            "acme",
            [Identity("acme", "api", 12), Identity("acme", "web", 7)]
        );

        // Assert
        Assert.Equal(["acme/api#12", "acme/web#7"], contributed.Ids);
    }

    [Fact]
    public void For_CountsIdentitiesBelongingToAnotherOwnerAsForeign()
    {
        // Act
        var contributed = ContributedPullRequests.For(
            "acme",
            [Identity("acme", "api", 12), Identity("ps-unite", "tools", 3)]
        );

        // Assert
        Assert.Equal(1, contributed.ForeignCount);
    }

    [Fact]
    public void For_TreatsAnOwnerDifferingOnlyInCaseAsItsOwn()
    {
        // Act -- GitHub owner logins are case-insensitive identifiers
        var contributed = ContributedPullRequests.For("ACME", [Identity("acme", "api", 12)]);

        // Assert
        Assert.Equal(0, contributed.ForeignCount);
    }

    [Fact]
    public void For_WithNoIdentities_ReportsNoIdsAndNoForeignItems()
    {
        // Act
        var contributed = ContributedPullRequests.For("acme", []);

        // Assert
        Assert.Empty(contributed.Ids);
        Assert.Equal(0, contributed.ForeignCount);
    }

    [Fact]
    public void None_ReportsNoIdsAndNoForeignItems()
    {
        // Act
        var contributed = ContributedPullRequests.None;

        // Assert
        Assert.Empty(contributed.Ids);
        Assert.Equal(0, contributed.ForeignCount);
    }

    private static PullRequestIdentity Identity(string owner, string repository, int number) =>
        new(
            id: $"{owner}/{repository}#{number}",
            owner: owner,
            repository: repository,
            number: number,
            title: "Add feature",
            url: $"https://github.com/{owner}/{repository}/pull/{number}",
            authorLogin: "author"
        );
}
