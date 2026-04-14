namespace GBTools.ImageSharp.GameBoyCamera.Metadata;

public sealed record GameBoyCameraBasicMetadata(
    string UserId,
    string UserName,
    string BirthDate,
    string Gender,
    string BloodType,
    string Comment,
    bool IsCopy);