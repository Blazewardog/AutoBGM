namespace AutoBgm;

// All calls run on the framework thread. true means the game's BGM mute is on.
public sealed class BgmController(Func<bool> readMuted, Action<bool> writeMuted,
                                  Func<bool> readPending, Action<bool> writePending)
{
    private bool recovered;

    public void Update(bool playing)
    {
        if (!recovered)
        {
            Restore();
            recovered = true;
        }
        if (!playing) { Restore(); return; }
        if (!readMuted())
        {
            if (!readPending()) writePending(true);
            writeMuted(true);
        }
    }

    public void Restore()
    {
        if (!readPending()) return;
        if (readMuted()) writeMuted(false);
        writePending(false);
    }
}
