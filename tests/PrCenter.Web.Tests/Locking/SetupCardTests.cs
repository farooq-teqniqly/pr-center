using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Core.Settings;
using PrCenter.Web.Components.Locking;

namespace PrCenter.Web.Tests.Locking;

public sealed class SetupCardTests : BunitContext
{
    // Shaped like a real passphrase (in range, with a digit and a symbol) but
    // written as an obvious placeholder so secret scanners do not read a test
    // fixture as a leaked credential.
    private const string ValidPassword = "example-pass-9!";

    private readonly ITokenVault _vault = Substitute.For<ITokenVault>();
    private readonly IAppLock _appLock = Substitute.For<IAppLock>();

    public static TheoryData<string, string> RejectedInput =>
        new()
        {
            { "short7c", "short7c" },
            { new string('x', 33), new string('x', 33) },
            { ValidPassword, "example-pass-9" },
            { string.Empty, string.Empty },
        };

    [Fact]
    public void SetupCard_WithAMatchingInRangePassword_SetsThePasswordAndRaisesCompleted()
    {
        // Arrange
        _appLock.UnlockAsync(ValidPassword, Arg.Any<CancellationToken>()).Returns(true);
        var completed = false;
        var cut = RenderCard(() => completed = true);

        // Act
        Submit(cut, ValidPassword, ValidPassword);

        // Assert
        _vault.Received(1).SetPasswordAsync(ValidPassword, Arg.Any<CancellationToken>());
        Assert.True(completed);
    }

    [Fact]
    public void SetupCard_WithAnInRangePasswordLackingDigitsAndSymbols_SetsThePassword()
    {
        // Arrange
        const string weak = "passwordonly";
        _appLock.UnlockAsync(weak, Arg.Any<CancellationToken>()).Returns(true);
        var cut = RenderCard();

        // Act
        Submit(cut, weak, weak);

        // Assert
        _vault.Received(1).SetPasswordAsync(weak, Arg.Any<CancellationToken>());
    }

    [Theory]
    [MemberData(nameof(RejectedInput))]
    public void SetupCard_WithRejectedInput_ShowsAMessageAndSetsNoPassword(
        string password,
        string confirmation
    )
    {
        // Arrange
        var completed = false;
        var cut = RenderCard(() => completed = true);

        // Act
        Submit(cut, password, confirmation);

        // Assert
        Assert.NotNull(cut.Find("[data-testid=setup-error]"));
        _vault.DidNotReceive().SetPasswordAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.False(completed);
    }

    [Fact]
    public void SetupCard_WhenTheUnlockDoesNotTake_RaisesCompletedSoTheGateReevaluates()
    {
        // Arrange
        _appLock.UnlockAsync(ValidPassword, Arg.Any<CancellationToken>()).Returns(false);
        var completed = false;
        var cut = RenderCard(() => completed = true);

        // Act
        Submit(cut, ValidPassword, ValidPassword);

        // Assert
        Assert.True(completed);
    }

    [Fact]
    public void SetupCard_WhenTheVaultRejectsTheSetup_ShowsAFailureAndDoesNotRaiseCompleted()
    {
        // Arrange
        _vault
            .SetPasswordAsync(ValidPassword, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("the vault is already initialized"));
        var completed = false;
        var cut = RenderCard(() => completed = true);

        // Act
        Submit(cut, ValidPassword, ValidPassword);

        // Assert
        Assert.NotNull(cut.Find("[data-testid=setup-failure]"));
        Assert.False(completed);
    }

    private static void Submit(
        IRenderedComponent<SetupCard> cut,
        string password,
        string confirmation
    )
    {
        cut.Find("[data-testid=setup-password]").Change(password);
        cut.Find("[data-testid=setup-confirm]").Change(confirmation);
        cut.Find("[data-testid=setup-submit]").Click();
    }

    private IRenderedComponent<SetupCard> RenderCard(Action? onCompleted = null)
    {
        Services.AddLogging();
        Services.AddSingleton(
            new InitializeVault(_vault, _appLock, Substitute.For<IRefreshTrigger>())
        );
        return Render<SetupCard>(ps => ps.Add(c => c.OnCompleted, onCompleted ?? (() => { })));
    }
}
