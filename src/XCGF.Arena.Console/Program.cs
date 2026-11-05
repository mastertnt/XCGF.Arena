using System.Globalization;
using System.Net;
using XCGF.Arena.Data;
using XCGF.Arena.Dis;
using XCGF.Arena.Scenarios;

namespace XCGF.Arena.ConsoleHost;

internal static class Program
{
    private const int SuccessExitCode = 0;
    private const int ErrorExitCode = 1;
    private const int UsageExitCode = 2;
    private const int CancelledExitCode = 130;

    private const string Usage = """
        Usage: XCGF.Arena.Console [options]
          --library <path>     XCGF.Data library (default: the XCGF.Data example library)
          --duration <s>       simulation time to play, in seconds (default: 600)
          --step <s>           simulation step, in seconds (default: 1)
          --realtime           pace the simulation on the wall clock
          --dis <host[:port]>  send DIS 7 Entity State PDUs over UDP (port 3000 by default)
          --exercise <id>      DIS exercise identifier (default: 1)
        """;

    private static async Task<int> Main(string[] pArguments)
    {
        if (!TryParse(pArguments, out ArenaRunOptions? lOptions, out string? lError))
        {
            await Console.Error.WriteLineAsync(lError + Environment.NewLine + Usage);
            return UsageExitCode;
        }

        using CancellationTokenSource lCancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (pSender, pEventArgs) =>
        {
            // Let the arena stop by itself instead of having the runtime kill the process.
            pEventArgs.Cancel = true;
            lCancellation.Cancel();
        };

        ArenaRunner lRunner = new ArenaRunner();
        try
        {
            await lRunner.RunAsync(lOptions!, Console.Out, lCancellation.Token);
            if (lOptions!.Dis is UdpDisPublisher lUdp)
            {
                await Console.Out.WriteLineAsync($"{lUdp.SentCount} DIS PDUs sent.");
            }

            return SuccessExitCode;
        }
        catch (OperationCanceledException lException)
        {
            await Console.Error.WriteLineAsync($"XCGF.Arena stopped: {lException.Message}");
            return CancelledExitCode;
        }
        catch (XcgfDataException lException)
        {
            await Console.Error.WriteLineAsync($"XCGF.Arena failed: {lException.Message}");
            return ErrorExitCode;
        }
        finally
        {
            (lOptions!.Dis as IDisposable)?.Dispose();
        }
    }

    private static bool TryParse(string[] pArguments, out ArenaRunOptions? pOptions, out string? pError)
    {
        string lLibrary = Path.Combine(AppContext.BaseDirectory, "XCGF.Data", "examples", "library", "library.xml");
        double lDuration = 600;
        double lStep = 1;
        bool lRealTime = false;
        IPEndPoint? lDis = null;
        byte lExercise = 1;
        pOptions = null;
        pError = null;
        for (int lIndex = 0; lIndex < pArguments.Length; lIndex++)
        {
            string lArgument = pArguments[lIndex];
            string? lValue = lIndex + 1 < pArguments.Length ? pArguments[lIndex + 1] : null;
            switch (lArgument)
            {
                case "--library" when lValue is not null:
                    lLibrary = lValue;
                    lIndex++;
                    break;
                case "--duration" when TryParseSeconds(lValue, out lDuration):
                    lIndex++;
                    break;
                case "--step" when TryParseSeconds(lValue, out lStep):
                    lIndex++;
                    break;
                case "--realtime":
                    lRealTime = true;
                    break;
                case "--dis" when TryParseEndPoint(lValue, out lDis):
                    lIndex++;
                    break;
                case "--exercise" when byte.TryParse(lValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out lExercise):
                    lIndex++;
                    break;
                default:
                    pError = $"Invalid argument '{lArgument}'.";
                    return false;
            }
        }

        IDisPublisher? lPublisher = lDis is null ? null : new UdpDisPublisher(lDis, lExercise);
        pOptions = new ArenaRunOptions(lLibrary, new TwoFightersScenario(), TimeSpan.FromSeconds(lDuration), TimeSpan.FromSeconds(lStep), lRealTime, lPublisher);
        return true;
    }

    private static bool TryParseEndPoint(string? pText, out IPEndPoint? pEndPoint)
    {
        pEndPoint = null;
        if (pText is null)
        {
            return false;
        }

        if (IPEndPoint.TryParse(pText, out IPEndPoint? lEndPoint))
        {
            pEndPoint = lEndPoint.Port == 0 ? new IPEndPoint(lEndPoint.Address, UdpDisPublisher.DefaultPort) : lEndPoint;
            return true;
        }

        return false;
    }

    private static bool TryParseSeconds(string? pText, out double pSeconds)
    {
        return double.TryParse(pText, NumberStyles.Float, CultureInfo.InvariantCulture, out pSeconds) && pSeconds > 0;
    }
}
