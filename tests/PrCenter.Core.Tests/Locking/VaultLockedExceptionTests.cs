using PrCenter.Core.Locking;

namespace PrCenter.Core.Tests.Locking;

public sealed class VaultLockedExceptionTests
{
    [Fact]
    public void Constructor_Default_SetsExplanatoryMessage()
    {
        // Arrange / Act
        var exception = new VaultLockedException();

        // Assert
        Assert.Contains("locked", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_WithMessage_UsesThatMessage()
    {
        // Arrange / Act
        var exception = new VaultLockedException("custom message");

        // Assert
        Assert.Equal("custom message", exception.Message);
    }

    [Fact]
    public void Constructor_WithMessageAndInner_UsesBoth()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var exception = new VaultLockedException("outer", inner);

        // Assert
        Assert.Equal("outer", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }
}
