using UrgentCare.Core.Configuration;
using UrgentCare.Web.Session;

namespace UrgentCare.Web.Tests;

/// <summary>
/// The web app must run the same scenarios the CLI loads from shared/config, or the parity
/// guarantee between the two hosts means nothing.
/// </summary>
public class BundledScenariosTests
{
    private readonly BundledScenarios _bundled = new();

    [Fact]
    public void ShipsDefaultAndLean_WithDefaultFirst()
    {
        var names = _bundled.All.Select(s => s.Name).ToList();

        Assert.Equal("default", names[0]);
        Assert.Contains("lean", names);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("lean")]
    public void MatchesTheFileTheCliLoads(string name)
    {
        var fromDisk = new ScenarioConfigLoader().LoadByName(SharedConfigDirectory(), name);
        var bundled = _bundled.All.Single(s => s.Name == name).Config;

        Assert.Equal(fromDisk.ScenarioId, bundled.ScenarioId);
        Assert.Equal(fromDisk.Seed, bundled.Seed);
        Assert.Equal(fromDisk.TimeHorizon, bundled.TimeHorizon);
        Assert.Equal(fromDisk.Department, bundled.Department);
    }

    /// <summary>Walks up from the test output folder to the repository root.</summary>
    internal static string SharedConfigDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "UrgentCare.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "shared", "config");
    }
}
