using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
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
    public void OwnerTokens_WhenAnOwnerIsDeleted_RemovesThatOwner()
    {
        // Arrange
        StoredOwners(new OwnerTokenSummary("ps-unite", SavedAt));
        var cut = RenderTable();

        // Act
        cut.Find("[data-testid=owner-row][data-owner=ps-unite] [data-testid=delete-owner]")
            .Click();

        // Assert
        _vault.Received(1).DeleteTokenAsync("ps-unite", Arg.Any<CancellationToken>());
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
        cut.Find("[data-testid=owner-row][data-owner=ps-unite] [data-testid=delete-owner]").Click();

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
