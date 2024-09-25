using System;
using System.Text;

namespace GBRGBDump
{
    public interface IFileMetaService
    {
        FileMetaData GetFileMeta(byte[] data, int baseAddress, bool cartIsJP);
    }

    public class FileMetaService : IFileMetaService
    {
        private readonly ICharMapService _charMapService;
        private readonly IParseCustomMetadataService _parseCustomMetadataService;

        public FileMetaService(ICharMapService charMapService, IParseCustomMetadataService parseCustomMetadataService)
        {
            _charMapService = charMapService;
            _parseCustomMetadataService = parseCustomMetadataService;
        }

        public FileMetaData GetFileMeta(byte[] data, int baseAddress, bool cartIsJP)
        {
            int cartIndex = (baseAddress / 0x1000) - 2;
            int albumIndex = cartIndex >= 0 ? data[0x11b2 + cartIndex] : 64;
            int frameNumber = data[baseAddress + 0x00F54];
            byte[] thumbnail = new byte[256]; // Adjust size as needed
            Array.Copy(data, baseAddress + 0x00E00, thumbnail, 0, thumbnail.Length);

            ImageMetaData meta = null;

            if (albumIndex < 64)
            {
                RomTypes romType = _parseCustomMetadataService.GetRomType(thumbnail);
                meta = new ImageMetaData { RomType = romType };

                if (romType != RomTypes.Stock)
                {
                    meta.CustomMeta = _parseCustomMetadataService.ParseCustomMetadata(thumbnail, romType);
                }

                if (romType == RomTypes.Stock)
                {
                    meta.BasicMeta = ParseBasicMetadata(data, baseAddress, cartIsJP);
                }
            }

            return new FileMetaData
            {
                CartIndex = cartIndex,
                AlbumIndex = albumIndex,
                BaseAddress = baseAddress,
                FrameNumber = frameNumber,
                Meta = meta
            };
        }

        public BasicMetaData ParseBasicMetadata(byte[] data, int baseAddress, bool cartIsJP)
        {
            // User ID
            byte[] userIdBytes = new byte[4];
            Array.Copy(data, baseAddress + 0x00F00, userIdBytes, 0, 4);
            string userId = ParseUserId(userIdBytes, cartIsJP);

            // Username
            byte[] userNameBytes = new byte[9];
            Array.Copy(data, baseAddress + 0x00F04, userNameBytes, 0, 9);
            string userName = _charMapService.ConvertToReadable(userNameBytes, cartIsJP);

            // Gender and Blood Type
            byte genderAndBloodType = data[baseAddress + 0x00F0D];
            string gender = ParseGender(genderAndBloodType);
            string bloodType = ParseBloodType(genderAndBloodType, cartIsJP);

            // Birthdate
            byte[] birthDateBytes = new byte[4];
            Array.Copy(data, baseAddress + 0x00F0E, birthDateBytes, 0, 4);
            string birthDate = ParseBirthDate(birthDateBytes, cartIsJP);

            // Comment
            byte[] commentBytes = new byte[27];
            Array.Copy(data, baseAddress + 0x00F15, commentBytes, 0, 27);
            string comment = _charMapService.ConvertToReadable(commentBytes, cartIsJP);

            // Is Copy
            bool isCopy = data[baseAddress + 0x00F33] == 1;

            return new BasicMetaData
            {
                UserId = userId,
                BirthDate = birthDate,
                UserName = userName,
                Gender = gender,
                BloodType = bloodType,
                Comment = comment,
                IsCopy = isCopy
            };
        }

        private string ParseUserId(byte[] userId, bool cartIsJP)
        {
            // Implement based on specific logic for parsing user ID
            return _charMapService.ConvertToReadable(userId, cartIsJP);
        }

        private string ParseGender(byte genderByte)
        {
            // Implement based on specific logic for parsing gender
            return (genderByte & 0x01) != 0 ? "Male" : (genderByte & 0x02) != 0 ? "Female" : "Unknown";
        }

        private string ParseBloodType(byte bloodTypeByte, bool cartIsJP)
        {
            // Only applicable if cartIsJP is true
            if (!cartIsJP) return "N/A";

            switch (bloodTypeByte & 0x1C)
            {
                case 0x04: return "A";
                case 0x08: return "B";
                case 0x0C: return "O";
                case 0x10: return "AB";
                default: return "Unknown";
            }
        }

        private string ParseBirthDate(byte[] birthDate, bool cartIsJP)
        {
            // Implement based on specific logic for parsing birth date
            return "01/01/1990"; // Placeholder
        }
    }
}
