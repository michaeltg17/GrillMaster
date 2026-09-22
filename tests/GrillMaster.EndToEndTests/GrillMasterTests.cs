using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace GrillMaster.EndToEndTests;

/// <summary>
/// The real end-to-end: launches the built console app as an external process exactly the way a user
/// runs it — no mocks, configuration comes from the app's own <c>appsettings.json</c> (the live API) —
/// captures its console output and validates the report the user would see.
/// </summary>
public sealed partial class GrillMasterTests
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
        // ending with a single "Total: {rounds} rounds" line.
        lines.Should().NotBeEmpty();
        lines.Should().OnlyContain(line => MenuLine().IsMatch(line) || TotalLine().IsMatch(line));
        TotalLine().IsMatch(lines[^1]).Should().BeTrue();

        var menuMatches = lines.Where(line => MenuLine().IsMatch(line)).Select(line => MenuLine().Match(line)).ToList();
        menuMatches.Should().NotBeEmpty();

        // Menus are reported in name order.
        var menuNames = menuMatches.Select(match => match.Groups["menu"].Value).ToList();
        menuNames.Should().BeInAscendingOrder(StringComparer.Ordinal);

        // The total is the sum of the per-menu round counts.
        var total = int.Parse(TotalLine().Match(lines[^1]).Groups["rounds"].Value);
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
