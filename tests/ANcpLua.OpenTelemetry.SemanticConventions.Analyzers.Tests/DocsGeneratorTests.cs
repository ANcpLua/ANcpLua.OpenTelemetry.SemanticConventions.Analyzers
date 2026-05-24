// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;

using Qyl.OpenTelemetry.SemanticConventions.Analyzers;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Tests;

public class DocsGeneratorTests
{
    [Fact]
    public async Task Validate_No_Changes_Mode_Succeeds()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var project = Path.Combine(
            repoRoot,
            "tools",
            "ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.DocsGenerator");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(project);
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("--check");

        using var process = Process.Start(startInfo)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, stdout + stderr);
    }

    [Fact]
    public async Task Audit_Mode_Prints_Current_Curated_Classification()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var project = Path.Combine(
            repoRoot,
            "tools",
            "ANcpLua.OpenTelemetry.SemanticConventions.Analyzers.DocsGenerator");

        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(project);
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("--audit");

        using var process = Process.Start(startInfo)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, stdout + stderr);
        Assert.Contains("Curated changelog mentions: 156", stdout, StringComparison.Ordinal);
        Assert.Contains("Live [Obsolete] metadata rows: 105", stdout, StringComparison.Ordinal);
        Assert.Contains("Supplemental diagnostic rows: 51", stdout, StringComparison.Ordinal);
        Assert.Contains("Supplemental attribute-value fallback rows outside the 156 count: 21", stdout, StringComparison.Ordinal);
    }

    private static string FindRepoRoot(string start)
    {
        var current = new DirectoryInfo(start);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Qyl.OpenTelemetry.SemanticConventions.Analyzers.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
