namespace AutoBgm.Detection;

internal static class HelperProtocol
{
    public const int MaxReadSize = 32;

    // TCP can combine heartbeats into one read. Validate the entire read before
    // applying its most recent state; never interpret input as text or commands.
    public static byte ReadLatest(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty || bytes.Length > MaxReadSize)
            throw new IOException("Invalid Linux helper message size");
        foreach (var value in bytes)
        {
            if (value is not ((byte)'0') and not ((byte)'1') and not ((byte)'?'))
                throw new IOException("Invalid Linux helper protocol");
        }
        return bytes[^1];
    }
}
