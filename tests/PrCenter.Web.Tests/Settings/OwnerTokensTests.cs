using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PrCenter.Core.Locking;
using PrCenter.Core.Ports;
using PrCenter.Core.Queue;
using PrCenter.Core.Settings;
using PrCenter.Web.Components.Settings;

namespace PrCenter.Web.Tests.Settings;

public sealed class OwnerTokensTests : BunitContext
{
    private static readonly DateTimeOffset SavedAt = new(2026, 3, 4, 9, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LastFreshAt = new(2026, 1, 2, 8, 0, 0, TimeSpan.Zero);

    private readonly ITokenVault _vault = Substitute.For<ITokenVault>();
    private readonly IGitHubFacts _gitHub = Substitute.For<IGitHubFacts>();
    private readonly IRefreshTrigger _trigger = Substitute.For<IRefreshTrigger>();
    private readonly QueueSnapshotHolder _holder = new(
        TimeProvider.System,
        NullLogger<QueueSnapshotHolder>.Instance
    );

    [Fact]
    public void OwnerTokens_ForAnOwnerWithAStatus_RendersTheOwnerSavedInstantAndStatus()
    {
        // Arrange
        StoredOwners(new OwnerTokenSummary("perfectserve", SavedAt));
        Publish(new OwnerStatus("perfectserve", OwnerFetchStatus.Ok));

        // Act
        var cut = RenderTable();

        // Assert
        var row = cut.Find("[data-testid=owner-row][data-owner=perfectserve]");
        Assert.Contains("perfectserve", row.TextContent, StringComparison.Ordinal);
        Assert.Contains("2026-03-04", row.TextContent, StringComparison.Ordinal);
        Assert.Contains("OK", row.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnerTokens_ForATokenWithNoSavedInstant_RendersAnExplicitUnknown()
    {
        // Arrange
        StoredOwners(new OwnerTokenSummary("ps-unite", savedAt: null));

        // Act
        var cut = RenderTable();

        // Assert
        Assert.Equal(
            "Unknown",
            cut.Find("[data-testid=owner-row][data-owner=ps-unite] [data-testid=owner-saved-at]")
                .TextContent.Trim()
        );
    }

    [Fact]
    public void OwnerTokens_ForAnOwnerAbsentFromTheSnapshot_RendersNotYetPolled()
    {
        // Arrange
        StoredOwners(new OwnerTokenSummary("ps-unite", SavedAt));
        Publish(new OwnerStatus("perfectserve", OwnerFetchStatus.Ok));

        // Act
        var cut = RenderTable();

        // Assert
        Assert.Equal(
            "Not yet polled",
            cut.Find("[data-testid=owner-row][data-owner=ps-unite] [data-testid=owner-status]")
                .TextContent.Trim()
        );
    }

    [Fact]
    public void OwnerTokens_WhenNoSnapshotHasBeenPublished_RendersNotYetPolled()
    {
        // Arrange
        StoredOwners(new OwnerTokenSummary("ps-unite", SavedAt));

        // Act
        var cut = RenderTable();

        // Assert
        Assert.Equal(
            "Not yet polled",
            cut.Find("[data-testid=owner-row][data-owner=ps-unite] [data-testid=owner-status]")
                .TextContent.Trim()
        );
    }

    [Theory]
    [InlineData(OwnerFetchStatus.MisconfiguredToken)]
    [InlineData(OwnerFetchStatus.Error)]
    public void OwnerTokens_ForANonOkStatus_RendersTheStatusAndItsDetail(OwnerFetchStatus status)
    {
        // Arrange
        StoredOwners(new OwnerTokenSummary("perfectserve", SavedAt));
        Publish(new OwnerStatus("perfectserve", status, "403 from the search API"));

        // Act
        var cut = RenderTable();

        // Assert
        var text = cut.Find(
            "[data-testid=owner-row][data-owner=perfectserve] [data-testid=owner-status]"
        ).TextContent;
        Assert.Contains("403 from the search API", text, StringComparison.Ordinal);
        Assert.DoesNotContain("OK", text, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnerTokens_ForACarriedOverOwner_DoesNotRenderTheLastFreshInstant()
    {
        // Arrange
        StoredOwners(new OwnerTokenSummary("perfectserve", SavedAt));
        Publish(
            new OwnerStatus("perfectserve", OwnerFetchStatus.Error, "rate limited", LastFreshAt)
        );

        // Act
        var cut = RenderTable();

        // Assert
        Assert.DoesNotContain("2026-01-02", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnerTokens_WithAValidSubmission_StoresTheTokenAndShowsNoTokenInTheMarkup()
    {
        // Arrange
        const string token = "github_pat_supersecretvalue";
        var cut = RenderTable();

        // Act
        Submit(cut, " perfectserve ", token);

        // Assert
        _vault.Received(1).StoreTokenAsync("perfectserve", token, Arg.Any<CancellationToken>());
        Assert.DoesNotContain("supersecret", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnerTokens_WhenTheOwnerNameIsTypedExactly_RemovesThatOwner()
    {
        // Arrange
        StoredOwners(new OwnerTokenSummary("ps-unite", SavedAt));
        var cut = RenderTable();

        // Act
        BeginDelete(cut, "ps-unite");
        ConfirmDelete(cut, "ps-unite");

        // Assert
        _vault.Received(1).DeleteTokenAsync("ps-unite", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void OwnerTokens_WhenDeleteIsClicked_AsksForTheOwnerNameAndDeletesNothingYet()
    {
        // Arrange
        StoredOwners(new OwnerTokenSummary("ps-unite", SavedAt));
        var cut = RenderTable();

        // Act
        BeginDelete(cut, "ps-unite");

        // Assert
        var confirmation = cut.Find(
            "[data-testid=owner-row][data-owner=ps-unite] [data-testid=delete-confirmation]"
        );
        Assert.Contains("ps-unite", confirmation.TextContent, StringComparison.Ordinal);
        _vault.DidNotReceive().DeleteTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("ps-unit")]
    [InlineData("PS-UNITE")]
    [InlineData("")]
    [InlineData("perfectserve")]
    public void OwnerTokens_WhenTheTypedNameDoesNotMatch_DeletesNothingAndStaysOnTheConfirmation(
        string typed
    )
    {
        // Arrange
        StoredOwners(new OwnerTokenSummary("ps-unite", SavedAt));
        var cut = RenderTable();

        // Act
        BeginDelete(cut, "ps-unite");
        ConfirmDelete(cut, typed);

        // Assert
        Assert.NotNull(
            cut.Find("[data-testid=owner-row][data-owner=ps-unite] [data-testid=delete-mismatch]")
        );
        _vault.DidNotReceive().DeleteTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void OwnerTokens_WhenTheDeleteIsCancelled_DeletesNothingAndLeavesTheConfirmation()
    {
        // Arrange
        StoredOwners(new OwnerTokenSummary("ps-unite", SavedAt));
        var cut = RenderTable();
        BeginDelete(cut, "ps-unite");

        // Act
        cut.Find("[data-testid=owner-row][data-owner=ps-unite] [data-testid=delete-cancel]")
            .Click();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid=delete-confirmation]"));
        _vault.DidNotReceive().DeleteTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void OwnerTokens_WhenAnotherRowStartsDeleting_ConfirmsOnlyTheNewRow()
    {
        // Arrange
        StoredOwners(
            new OwnerTokenSummary("ps-unite", SavedAt),
            new OwnerTokenSummary("perfectserve", SavedAt)
        );
        var cut = RenderTable();
        BeginDelete(cut, "ps-unite");

        // Act
        BeginDelete(cut, "perfectserve");

        // Assert
        var confirmation = Assert.Single(cut.FindAll("[data-testid=delete-confirmation]"));
        Assert.Contains("perfectserve", confirmation.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnerTokens_WhenTheTypedNameMatchesADifferentRow_DeletesTheConfirmedRowOnly()
    {
        // Arrange
        StoredOwners(
            new OwnerTokenSummary("ps-unite", SavedAt),
            new OwnerTokenSummary("perfectserve", SavedAt)
        );
        var cut = RenderTable();

        // Act
        BeginDelete(cut, "ps-unite");
        ConfirmDelete(cut, "perfectserve");

        // Assert
        _vault.DidNotReceive().DeleteTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void OwnerTokens_WhenTheTokenHasSurroundingWhitespace_StoresItTrimmed()
    {
        // Arrange
        var cut = RenderTable();

        // Act
        Submit(cut, "perfectserve", "  github_pat_value\n");

        // Assert
        _vault
            .Received(1)
            .StoreTokenAsync("perfectserve", "github_pat_value", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void OwnerTokens_WhenTheDeleteFails_KeepsTheConfirmationAndShowsAMessage()
    {
        // Arrange
        StoredOwners(new OwnerTokenSummary("ps-unite", SavedAt));
        _vault
            .DeleteTokenAsync("ps-unite", Arg.Any<CancellationToken>())
            .ThrowsAsync(new VaultLockedException("the vault is locked"));
        var cut = RenderTable();

        // Act
        BeginDelete(cut, "ps-unite");
        ConfirmDelete(cut, "ps-unite");

        // Assert
        Assert.NotNull(cut.Find("[data-testid=owner-token-error]"));
        Assert.NotNull(
            cut.Find(
                "[data-testid=owner-row][data-owner=ps-unite] [data-testid=delete-confirmation]"
            )
        );
    }

    [Fact]
    public void OwnerTokens_WhenTheSaveFails_ShowsAMessageAndKeepsTheEnteredOwner()
    {
        // Arrange
        _vault
            .StoreTokenAsync("perfectserve", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new VaultLockedException("the vault is locked"));
        var cut = RenderTable();

        // Act
        Submit(cut, "perfectserve", "github_pat_value");

        // Assert
        Assert.NotNull(cut.Find("[data-testid=owner-token-error]"));
        Assert.Equal("perfectserve", cut.Find("[data-testid=new-owner]").GetAttribute("value"));
    }

    [Theory]
    [MemberData(nameof(RejectedInput))]
    public void OwnerTokens_WithRejectedInput_ShowsAMessageAndStoresNothing(
        string owner,
        string token
    )
    {
        // Arrange
        var cut = RenderTable();

        // Act
        Submit(cut, owner, token);

        // Assert
        Assert.NotNull(cut.Find("[data-testid=owner-token-error]"));
        _vault
            .DidNotReceive()
            .StoreTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void OwnerTokens_WhenSavingOrDeleting_NeverCallsGitHub()
    {
        // Arrange
        StoredOwners(new OwnerTokenSummary("ps-unite", SavedAt));
        var cut = RenderTable();

        // Act
        Submit(cut, "perfectserve", "github_pat_value");
        BeginDelete(cut, "ps-unite");
        ConfirmDelete(cut, "ps-unite");

        // Assert
        Assert.Empty(_gitHub.ReceivedCalls());
    }

    public static TheoryData<string, string> RejectedInput =>
        new()
        {
            { string.Empty, "github_pat_value" },
            { "   ", "github_pat_value" },
            { new string('o', 256), "github_pat_value" },
            { "perfectserve", string.Empty },
            { "perfectserve", "   " },
            { "perfectserve", new string('t', 513) },
        };

    private static void BeginDelete(IRenderedComponent<OwnerTokens> cut, string owner) =>
        cut.Find($"[data-testid=owner-row][data-owner={owner}] [data-testid=delete-owner]").Click();

    private static void ConfirmDelete(IRenderedComponent<OwnerTokens> cut, string typed)
    {
        cut.Find("[data-testid=delete-confirm-input]").Change(typed);
        cut.Find("[data-testid=delete-confirm]").Click();
    }

    private static void Submit(IRenderedComponent<OwnerTokens> cut, string owner, string token)
    {
        cut.Find("[data-testid=new-owner]").Change(owner);
        cut.Find("[data-testid=new-token]").Change(token);
        cut.Find("[data-testid=save-owner]").Click();
    }

    private void StoredOwners(params OwnerTokenSummary[] owners) =>
        _vault.ListOwnerTokensAsync(Arg.Any<CancellationToken>()).Returns(owners);

    private void Publish(params OwnerStatus[] statuses) => _holder.Publish([], statuses);

    private IRenderedComponent<OwnerTokens> RenderTable()
    {
        Services.AddSingleton(_vault);
        Services.AddSingleton(_gitHub);
        Services.AddSingleton(_holder);
        Services.AddSingleton(new GetQueue(_holder));
        Services.AddSingleton(new SaveOwnerToken(_vault, _trigger));
        Services.AddSingleton(new RemoveOwner(_vault, _trigger));
        return Render<OwnerTokens>();
    }
}
