using System.Reflection;
using NetArchTest.Rules;
using PrCenter.Core.Ports;

namespace PrCenter.ArchitectureTests;

/// <summary>
/// Guards the invariant that diagnostics are written and never read back into a
/// decision. Membership, update, and covered are pure functions of current
/// GitHub facts; a deriver that could consult what an earlier poll recorded
/// would be a stored transition machine wearing a diagnostics hat.
/// </summary>
public sealed class PollDiagnosticsBoundaryTests
{
    private static readonly Assembly CoreAssembly = typeof(IPollDiagnosticsSink).Assembly;

    [Theory]
    [InlineData("PrCenter.Core.Queue")]
    [InlineData("PrCenter.Core.Derivation")]
    public void RefreshAndDerivationNamespaces_DoNotDependOnTheDiagnosticsReader(string ns)
    {
        // Arrange / Act
        var result = Types
            .InAssembly(CoreAssembly)
            .That()
            .ResideInNamespace(ns)
            .ShouldNot()
            .HaveDependencyOnAny(typeof(IPollDiagnosticsReader).FullName!)
            .GetResult();

        // Assert -- the compiler cannot express this, so it is asserted here
        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void PollDiagnosticsSink_DeclaresNoReadMember()
    {
        // Act -- the write-only shape is what makes the invariant unbreakable by
        // accident: there is nothing on the sink a deriver could call to read
        var members = typeof(IPollDiagnosticsSink).GetMembers();

        // Assert
        var member = Assert.Single(members);
        Assert.Equal(nameof(IPollDiagnosticsSink.WriteAsync), member.Name);
    }

    private static string Describe(NetArchTest.Rules.TestResult result)
    {
        var names = result.FailingTypes?.Select(type => type.FullName) ?? [];
        return $"Failing types: {string.Join(", ", names)}";
    }
}
