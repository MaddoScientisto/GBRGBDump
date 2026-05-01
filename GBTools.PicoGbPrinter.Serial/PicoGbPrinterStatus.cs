namespace GBTools.PicoGbPrinter.Serial;

public sealed class PicoGbPrinterStatus
{
    public PicoGbPrinterStatus(uint queuedCaptures, uint lastCaptureSize, uint replayCaptureSize, bool isPrinting, bool hasReplayCapture, bool debugEnabled)
    {
        QueuedCaptures = queuedCaptures;
        LastCaptureSize = lastCaptureSize;
        ReplayCaptureSize = replayCaptureSize;
        IsPrinting = isPrinting;
        HasReplayCapture = hasReplayCapture;
        DebugEnabled = debugEnabled;
    }

    public uint QueuedCaptures { get; }

    public uint LastCaptureSize { get; }

    public uint ReplayCaptureSize { get; }

    public bool IsPrinting { get; }

    public bool HasReplayCapture { get; }

    public bool DebugEnabled { get; }
}
