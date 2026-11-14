using System.Reflection;
using Xunit;

namespace VectorEngine.Tests;

public class EngineInfoTests
{
    [Fact]
    public void Engine_is_named() => Assert.Equal("VectorEngine", EngineInfo.Name);

    [Fact]
    public void The_version_is_the_package_version_without_a_source_revision()
    {
        Assert.StartsWith("0.1.0", EngineInfo.Version);
        Assert.DoesNotContain("+", EngineInfo.Version);
        Assert.DoesNotContain(" ", EngineInfo.Version);
    }

    [Fact]
    public void Any_assembly_has_a_readable_version()
    {
        // the framework's own assembly: whatever its version looks like, only the numbers are kept
        Assert.Matches(@"^\d+\.\d+\.\d+", EngineInfo.ReadVersion(typeof(object).Assembly));
    }
}
