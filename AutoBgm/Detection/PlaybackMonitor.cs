using System.Net;
using System.Net.Sockets;
using Dalamud.Utility;

namespace AutoBgm.Detection;

public sealed record PlaybackState(bool Playing, string Detail, long UpdatedAt);

public sealed class PlaybackMonitor : IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task worker;
    private PlaybackState state = new(false, "Starting detection", Environment.TickCount64);
    public PlaybackState State => Volatile.Read(ref state);
    public string Backend { get; }
    public bool UsesLinuxHelper { get; }
    private string[] detectedPlayers = [];
    private string windowsPlayers;
    public string[] DetectedPlayers => Volatile.Read(ref detectedPlayers);
    public void SetWindowsPlayers(string players) => Volatile.Write(ref windowsPlayers, players);

    public PlaybackMonitor(DetectionMode mode, int port, string players)
    {
        // Use Dalamud's launcher-provided host information. Probing ntdll exports
        // from the plugin can miss Wine and incorrectly activate Windows media APIs.
        var linux = mode == DetectionMode.LinuxHelper || (mode == DetectionMode.Auto && Util.IsWine());
        UsesLinuxHelper = linux;
        windowsPlayers = players;
        Backend = linux ? "Linux helper" : "Windows media sessions";
        worker = Task.Run(() => RunAsync(linux, port, cancellation.Token));
    }

    private void Publish(bool playing, string detail) =>
        Volatile.Write(ref state, new(playing, detail, Environment.TickCount64));

    private async Task RunAsync(bool linux, int port, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (linux) await ReadHelperAsync(port, token);
                else await WindowsPlayback.RunAsync(() => Volatile.Read(ref windowsPlayers), Publish,
                    apps => Volatile.Write(ref detectedPlayers, apps), token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex) { Publish(false, $"Detection unavailable: {ex.Message}"); }
            try { await Task.Delay(1000, token); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ReadHelperAsync(int port, CancellationToken token)
    {
        using var client = new TcpClient();
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
        }
        var stream = client.GetStream();
        var buffer = new byte[1];
        while (!token.IsCancellationRequested)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            if (await stream.ReadAsync(buffer, timeout.Token) == 0)
                throw new IOException("Linux helper disconnected");
            // Protocol v1: one byte per heartbeat; no metadata or commands.
            switch (buffer[0])
            {
                case (byte)'1': Publish(true, "Media playing"); break;
                case (byte)'0': Publish(false, "Media paused, stopped, or absent"); break;
                case (byte)'?': Publish(false, "Linux playback detection unavailable"); break;
                default: throw new IOException("Invalid Linux helper protocol");
            }
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        // Never block the framework thread waiting for OS media APIs.
        _ = worker.ContinueWith(_ => cancellation.Dispose(), TaskScheduler.Default);
    }
}
