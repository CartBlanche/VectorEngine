using System;
using System.IO;
using System.Text.Json;
using VectorEngine.Splash;
using Xunit;

namespace VectorEngine.Tests;

public class VectorEngineLogoTests
{
    static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "VectorEngine.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, Path.Combine(parts));
    }

    // branding/make-logos.py draws the logo files from logo-geometry.json; this keeps that file the same shape the splash draws.
    [Fact]
    public void The_logo_files_are_drawn_from_the_splash_geometry()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(RepoFile("branding", "logo-geometry.json")));
        var root = doc.RootElement;

        Assert.Equal(VectorEngineLogo.Width, root.GetProperty("canvas")[0].GetSingle());
        Assert.Equal(VectorEngineLogo.Height, root.GetProperty("canvas")[1].GetSingle());
        Check(root.GetProperty("mark"), VectorEngineLogo.Mark, withLetter: false);
        Check(root.GetProperty("wordmark"), VectorEngineLogo.Wordmark, withLetter: true);
    }

    static void Check(JsonElement json, System.Collections.Generic.IReadOnlyList<VectorEngineLogo.Segment> live, bool withLetter)
    {
        Assert.Equal(live.Count, json.GetArrayLength());
        for (int i = 0; i < live.Count; i++)
        {
            var s = json[i];
            Assert.Equal(live[i].A.X, s[0].GetSingle(), 0.01f);
            Assert.Equal(live[i].A.Y, s[1].GetSingle(), 0.01f);
            Assert.Equal(live[i].B.X, s[2].GetSingle(), 0.01f);
            Assert.Equal(live[i].B.Y, s[3].GetSingle(), 0.01f);
            if (withLetter)
                Assert.Equal(live[i].Letter, s[4].GetInt32());
        }
    }
}
