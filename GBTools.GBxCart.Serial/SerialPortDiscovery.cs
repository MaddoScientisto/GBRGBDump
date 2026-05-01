using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Management;

namespace GBTools.GBxCart.Serial;

internal static class SerialPortDiscovery
{
    private static readonly Regex ComPortRegex = new Regex(@"\((COM\d+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex VidPidRegex = new Regex(@"VID_([0-9A-F]{4}).*PID_([0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<GbxCartPortInfo> GetAvailablePorts()
    {
        string[] portNames = SerialPort.GetPortNames()
            .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (portNames.Length == 0)
        {
            return Array.Empty<GbxCartPortInfo>();
        }

        var portsByName = portNames.ToDictionary(
            static portName => portName,
            static portName => new GbxCartPortInfo(portName, portName, portName, null, null, null, null),
            StringComparer.OrdinalIgnoreCase);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using ManagementObjectSearcher searcher = new(
                    "SELECT Name, Description, Manufacturer, PNPDeviceID FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");
                foreach (ManagementObject port in searcher.Get().OfType<ManagementObject>())
                {
                    string? name = port["Name"]?.ToString();
                    string? description = port["Description"]?.ToString();
                    string? manufacturer = port["Manufacturer"]?.ToString();
                    string? pnpDeviceId = port["PNPDeviceID"]?.ToString();
                    string? portName = ExtractPortName(name) ?? ExtractPortName(description);
                    if (string.IsNullOrWhiteSpace(portName))
                    {
                        continue;
                    }

                    string normalizedPortName = portName!;
                    if (!portsByName.ContainsKey(normalizedPortName))
                    {
                        continue;
                    }

                    portsByName[normalizedPortName] = new GbxCartPortInfo(
                        normalizedPortName,
                        name ?? normalizedPortName,
                        description ?? name ?? normalizedPortName,
                        manufacturer,
                        pnpDeviceId,
                        TryExtractVendorId(pnpDeviceId),
                        TryExtractProductId(pnpDeviceId));
                }
            }
            catch
            {
            }
        }

        return portsByName.Values
            .OrderByDescending(static port => port.IsKnownUsbBridge)
            .ThenBy(static port => port.PortName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? ExtractPortName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        Match match = ComPortRegex.Match(value);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static int? TryExtractVendorId(string? pnpDeviceId)
    {
        Match match = VidPidRegex.Match(pnpDeviceId ?? string.Empty);
        return match.Success && int.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out int value)
            ? value
            : null;
    }

    private static int? TryExtractProductId(string? pnpDeviceId)
    {
        Match match = VidPidRegex.Match(pnpDeviceId ?? string.Empty);
        return match.Success && int.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.HexNumber, null, out int value)
            ? value
            : null;
    }
}