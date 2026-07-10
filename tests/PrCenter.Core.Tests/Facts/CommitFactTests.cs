using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Facts;

public sealed class CommitFactTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithMissingAuthorLogin_Throws(string? authorLogin)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            new CommitFact(authorLogin!, DateTimeOffset.UtcNow)
        );
    }

    [Fact]
    public void Constructor_WithValidArguments_ExposesValues()
    {
        // Arrange
        var landedAt = DateTimeOffset.UtcNow;

        // Act
        var commit = new CommitFact("octocat", landedAt);

        // Assert
        Assert.Equal("octocat", commit.AuthorLogin);
        Assert.Equal(landedAt, commit.LandedAt);
    }
}
