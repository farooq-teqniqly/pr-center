using System.Reflection;
using NetArchTest.Rules;
using PrCenter.Core.Ports;
using PrCenter.GitHub;
using PrCenter.Persistence;

namespace PrCenter.ArchitectureTests;

public sealed class DependencyDirectionTests
{
    private static readonly Assembly CoreAssembly = typeof(IGitHubFacts).Assembly;
    private static readonly Assembly GitHubAssembly =
        typeof(GitHubServiceCollectionExtensions).Assembly;
    private static readonly Assembly PersistenceAssembly =
        typeof(PersistenceServiceCollectionExtensions).Assembly;

    [Fact]
    public void Core_DoesNotDependOnAdaptersWebOrInfrastructure()
    {
        // Arrange / Act
        var result = Types
            .InAssembly(CoreAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "PrCenter.GitHub",
                "PrCenter.Persistence",
                "PrCenter.Web",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore"
            )
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void GitHubAdapter_DoesNotDependOnPersistenceOrWeb()
    {
        // Arrange / Act
        var result = Types
            .InAssembly(GitHubAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("PrCenter.Persistence", "PrCenter.Web")
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void PersistenceAdapter_DoesNotDependOnGitHubOrWeb()
    {
        // Arrange / Act
        var result = Types
            .InAssembly(PersistenceAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("PrCenter.GitHub", "PrCenter.Web")
            .GetResult();

        // Assert
        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(NetArchTest.Rules.TestResult result)
    {
        var names = result.FailingTypes?.Select(t => t.FullName) ?? [];
        return $"Failing types: {string.Join(", ", names)}";
    }
}
