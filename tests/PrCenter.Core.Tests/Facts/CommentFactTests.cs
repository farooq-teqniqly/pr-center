using PrCenter.Core.Facts;

namespace PrCenter.Core.Tests.Facts;

public sealed class CommentFactTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithMissingAuthorLogin_Throws(string? authorLogin)
    {
        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            new CommentFact(authorLogin!, DateTimeOffset.UtcNow)
        );
    }

    [Fact]
    public void Constructor_WithValidArguments_ExposesValues()
    {
        // Arrange
        var createdAt = DateTimeOffset.UtcNow;

        // Act
        var comment = new CommentFact("octocat", createdAt);

        // Assert
        Assert.Equal("octocat", comment.AuthorLogin);
        Assert.Equal(createdAt, comment.CreatedAt);
    }
}
