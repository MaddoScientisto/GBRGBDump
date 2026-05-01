using System.Runtime.InteropServices;

namespace GBTools.GBxCart.Serial.Serial;

internal static class GbxCartSerialConnectionFactory
{
    public static IGbxCartSerialConnection Create(GbxCartClientOptions options)
    {
#if NET8_0_OR_GREATER
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new RjcpGbxCartSerialConnection(options);
        }
#endif

        return new SystemIoGbxCartSerialConnection(options);
    }
}
