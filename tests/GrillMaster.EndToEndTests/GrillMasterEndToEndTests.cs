using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace GrillMaster.EndToEndTests;

/// <summary>
/// The real end-to-end: launches the built console app as an external process exactly the way a user
/// runs it — no mocks, configuration comes from the app's own <c>appsettings.json</c> (the live API) —
/// captures its console output and validates the report the user would see. Console color is not
/// validated: with stdout redirected (as here) .NET writes no color codes at all, so the captured text
/// carries no color information — observing it would require a Windows pseudoconsole (ConPTY).
/// </summary>
public sealed partial class GrillMasterEndToEndTests
{
    [GeneratedRegex(@"^(?!Total:)(?<menu>.+): (?<rounds>\d+) rounds$")]
    private static partial Regex MenuLine();

    [GeneratedRegex(@"^Total: (?<rounds>\d+) rounds$")]
    private static partial Regex TotalLine();

    [Fact]
    public async Task PrintsTheExpectedGrillReport()
    {
        var (exitCode, stdout, stderr) = await RunGrillMaster();

        exitCode.Should().Be(0);
        stderr.Should().BeEmpty();

        var lines = stdout
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Length > 0)
            .ToList();

        // The user sees only the report: one "{menu}: {rounds} rounds" line per menu,
        // ending with a single "Total: {rounds} rounds" line — and nothing else.
        lines.Should().NotBeEmpty();
        lines.Should().OnlyContain(line => MenuLine().IsMatch(line) || TotalLine().IsMatch(line));
        TotalLine().IsMatch(lines[^1]).Should().BeTrue();

        var menuMatches = lines.Where(line => MenuLine().IsMatch(line)).Select(line => MenuLine().Match(line)).ToList();
        var totalMatches = lines.Where(line => TotalLine().IsMatch(line)).Select(line => TotalLine().Match(line)).ToList();

        // The live API serves exactly 15 menus, and the report has exactly one total line.
        menuMatches.Should().HaveCount(15);
        totalMatches.Should().HaveCount(1);

        // Menus are reported in name order.
        var menuNames = menuMatches.Select(match => match.Groups["menu"].Value).ToList();
        menuNames.Should().BeInAscendingOrder(StringComparer.Ordinal);

        // The total is the sum of the per-menu round counts.
        var total = int.Parse(totalMatches.Single().Groups["rounds"].Value);
        total.Should().Be(menuMatches.Sum(match => int.Parse(match.Groups["rounds"].Value)));
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunGrillMaster()
    {
        var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "GrillMaster.Console.exe" : "GrillMaster.Console";

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(AppContext.BaseDirectory, fileName),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("Failed to start the console app.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(120));

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return (process.ExitCode, await stdoutTask, await stderrTask);
    }
}
