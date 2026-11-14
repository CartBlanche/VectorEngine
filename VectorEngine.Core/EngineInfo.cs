using System;
using System.Reflection;

namespace VectorEngine;

/// <summary>Identifies the engine build; also proves a game is wired to the shared engine.</summary>
public static class EngineInfo
{
    public const string Name = "VectorEngine";

    /// <summary>
    /// The engine's version as it was built, such as "0.1.0-preview.5": the package version (Directory.Build.props), without the
    /// source revision the build appends after a "+". The start-up splash shows it.
    /// </summary>
    public static string Version { get; } = ReadVersion(typeof(EngineInfo).Assembly);

    internal static string ReadVersion(Assembly assembly)
    {
        string informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
        {
            informational = assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        int plus = informational.IndexOf('+');
        return plus >= 0 ? informational.Substring(0, plus) : informational;
    }
}
