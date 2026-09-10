namespace AutoBgm.Detection;

internal static class HelperProtocol
{
    public const int MaxReadSize = 32;

    // TCP can combine heartbeats into one read. Validate the entire read before
    // applying its most recent state; never interpret input as text or commands.
    public static bool TryReadLatest(ReadOnlySpan<byte> bytes, out byte latest)
    {
        latest = default;
        if (bytes.IsEmpty || bytes.Length > MaxReadSize)
            return false;
        foreach (var value in bytes)
        {
            if (value is not ((byte)'0') and not ((byte)'1') and not ((byte)'?'))
                return false;
        }
        latest = bytes[^1];
        return true;
    }
}
