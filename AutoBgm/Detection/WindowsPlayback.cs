using Windows.Media.Control;

namespace AutoBgm.Detection;

internal static class WindowsPlayback
{
    // Kept separate so Wine never needs to activate Windows media APIs.
    public static async Task RunAsync(Func<string> players, Action<bool, string> publish,
                                     Action<string[]> publishPlayers, CancellationToken token)
    {
        var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(token);
        while (!token.IsCancellationRequested)
        {
            var filters = players().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var detected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var playing = false;
            foreach (var session in manager.GetSessions())
            {
                var id = session.SourceAppUserModelId;
                if (id.Contains("ffxiv", StringComparison.OrdinalIgnoreCase)) continue;
                detected.Add(id);
                if (filters.Length > 0 && !filters.Any(f => id.Contains(f, StringComparison.OrdinalIgnoreCase))) continue;
                if (session.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    playing = true;
            }
            publishPlayers(detected.Order(StringComparer.OrdinalIgnoreCase).ToArray());
            publish(playing, playing ? "Media playing" : "Media paused, stopped, or absent");
            await Task.Delay(500, token);
        }
    }
}
