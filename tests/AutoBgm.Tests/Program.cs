using AutoBgm;

var checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks++;
}

foreach (var initiallyMuted in new[] { false, true })
{
    var muted = initiallyMuted;
    var pending = false;
    var writes = 0;
    var controller = new BgmController(() => muted, v => { muted = v; writes++; },
        () => pending, v => pending = v);
    controller.Update(false);
    Check(muted == initiallyMuted && writes == 0, "Idle preserves settings");
    controller.Update(true);
    Check(muted && pending == !initiallyMuted, "Playing mutes and records ownership");
    controller.Update(true);
    Check(writes == (initiallyMuted ? 0 : 1), "Steady playback avoids repeated writes");
    controller.Update(false);
    Check(muted == initiallyMuted && !pending, "Pause/stop/failure restores only owned mute");
    controller.Update(true);
    controller.Restore();
    Check(muted == initiallyMuted && !pending, "Unload restores");
}
{
    var muted = true;
    var pending = true;
    var controller = new BgmController(() => muted, v => muted = v, () => pending, v => pending = v);
    controller.Update(false);
    Check(!muted && !pending, "Recovers persisted mute after a crash");
}
{
    var muted = false;
    var pending = false;
    var fail = true;
    var controller = new BgmController(() => muted,
        v => { if (fail) throw new Exception("Write failed"); muted = v; },
        () => pending, v => pending = v);
    try { controller.Update(true); } catch (Exception) { }
    Check(pending && !muted, "Ownership survives failed write");
    fail = false;
    controller.Update(true);
    Check(pending && muted, "Retries a failed mute during playback");
    controller.Update(false);
    Check(!pending && !muted, "Failure recovery clears ownership safely");
}
Console.WriteLine($"Passed {checks} BGM lifecycle checks.");

// Treat the socket peer as untrusted even though it is on localhost.
var protocolChecks = 0;
void Reject(byte[] input)
{
    if (AutoBgm.Detection.HelperProtocol.TryReadLatest(input, out var latest))
        throw new Exception("Invalid helper input accepted");
    if (latest != default) throw new Exception("Invalid input leaked a partial status");
    protocolChecks++;
}
Reject([]);
Reject(new byte[33]);
Reject("GET / HTTP/1.1"u8.ToArray());
Reject("{\"playing\":true}"u8.ToArray());
for (var value = 0; value <= 255; value++)
{
    if (value is (byte)'0' or (byte)'1' or (byte)'?') continue;
    Reject([(byte)'1', (byte)value, (byte)'0']);
}
foreach (var input in new[] { "0", "1", "?", "10", "01", "1?", new string('1', 32) })
{
    var bytes = System.Text.Encoding.ASCII.GetBytes(input);
    if (!AutoBgm.Detection.HelperProtocol.TryReadLatest(bytes, out var latest) || latest != bytes[^1])
        throw new Exception("Combined heartbeats should use the most recent valid status");
    protocolChecks++;
}
Console.WriteLine($"Passed {protocolChecks} untrusted helper input checks.");
