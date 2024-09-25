using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump
{
    public interface ICharMapService
    {
        string ConvertToReadable(byte[] data, bool cartIsJP);
        string ConvertDigit(byte byteValue);
    }

    public class CharMapService : ICharMapService
    {
        private readonly Dictionary<int, string> _charMapInt;
        private readonly Dictionary<int, string> _charMapJp;
        private readonly Dictionary<int, string> _charMapDateDigit;

        public CharMapService()
        {
            _charMapInt = new Dictionary<int, string>
            {
                { 0x56, "A" },
                { 0x57, "B" },
                { 0x58, "C" },
                { 0x59, "D" },
                { 0x5a, "E" },
                { 0x5b, "F" },
                { 0x5c, "G" },
                { 0x5d, "H" },
                { 0x5e, "I" },
                { 0x5f, "J" },
                { 0x60, "K" },
                { 0x61, "L" },
                { 0x62, "M" },
                { 0x63, "N" },
                { 0x64, "O" },
                { 0x65, "P" },
                { 0x66, "Q" },
                { 0x67, "R" },
                { 0x68, "S" },
                { 0x69, "T" },
                { 0x6a, "U" },
                { 0x6b, "V" },
                { 0x6c, "W" },
                { 0x6d, "X" },
                { 0x6e, "Y" },
                { 0x6f, "Z" },
                { 0x70, "_" },
                { 0x71, "\'" },
                { 0x72, "," },
                { 0x73, "." },
                { 0x74, "Á" },
                { 0x75, "Â" },
                { 0x76, "À" },
                { 0x77, "Ä" },
                { 0x78, "É" },
                { 0x79, "Ê" },
                { 0x7a, "È" },
                { 0x7b, "Ë" },
                { 0x7c, "Í" },
                { 0x7d, "Ï" },
                { 0x7e, "Ó" },
                { 0x7f, "Ö" },
                { 0x80, "Ú" },
                { 0x81, "Ü" },
                { 0x82, "Ñ" },
                { 0x83, "-" },
                { 0x84, "&" },
                { 0x85, "!" },
                { 0x86, "?" },
                { 0x87, " " },
                { 0x88, "a" },
                { 0x89, "b" },
                { 0x8a, "c" },
                { 0x8b, "d" },
                { 0x8c, "e" },
                { 0x8d, "f" },
                { 0x8e, "g" },
                { 0x8f, "h" },
                { 0x90, "i" },
                { 0x91, "j" },
                { 0x92, "k" },
                { 0x93, "l" },
                { 0x94, "m" },
                { 0x95, "n" },
                { 0x96, "o" },
                { 0x97, "p" },
                { 0x98, "q" },
                { 0x99, "r" },
                { 0x9a, "s" },
                { 0x9b, "t" },
                { 0x9c, "u" },
                { 0x9d, "v" },
                { 0x9e, "w" },
                { 0x9f, "x" },
                { 0xa0, "y" },
                { 0xa1, "z" },
                { 0xa2, "•" },
                { 0xa3, "~" },
                { 0xa4, "📱" },
                { 0xa5, " " },
                { 0xa6, "á" },
                { 0xa7, "â" },
                { 0xa8, "à" },
                { 0xa9, "ä" },
                { 0xaa, "é" },
                { 0xab, "ê" },
                { 0xac, "è" },
                { 0xad, "ë" },
                { 0xae, "í" },
                { 0xaf, "ï" },
                { 0xb0, "ó" },
                { 0xb1, "ö" },
                { 0xb2, "ú" },
                { 0xb3, "ü" },
                { 0xb4, "ñ" },
                { 0xb5, "ḉ" },
                { 0xb6, "ß" },
                { 0xb7, "😄" },
                { 0xb8, "😟" },
                { 0xb9, "\ud83d\ude05" },
                { 0xba, "0" },
                { 0xbb, "1" },
                { 0xbc, "2" },
                { 0xbd, "3" },
                { 0xbe, "4" },
                { 0xbf, "5" },
                { 0xc0, "6" },
                { 0xc1, "7" },
                { 0xc2, "8" },
                { 0xc3, "9" },
                { 0xc4, "/" },
                { 0xc5, ":" },
                { 0xc6, "~" },
                { 0xc7, "\"" },
                { 0xc8, "@" },
            };

            _charMapJp = new Dictionary<int, string>();

            _charMapDateDigit = new Dictionary<int, string>
            {
                { 0, "0" }, { 1, "1" }, { 2, "2" }, { 3, "3" }, { 4, "4" },
                { 5, "5" }, { 6, "6" }, { 7, "7" }, { 8, "8" }, { 9, "9" },
                { 10, "?" }, { 11, "?" }, { 12, "?" }, { 13, "?" }, { 14, "?" }, { 15, "?" }
            };
        }

        public string ConvertToReadable(byte[] data, bool cartIsJP)
        {
            var charMap = cartIsJP ? _charMapJp : _charMapInt;
            var result = new StringBuilder();

            foreach (var value in data)
            {
                if (charMap.TryGetValue(value, out var character))
                    result.Append(character);
                else
                    result.Append(' '); // Default to space if character not found
            }

            return result.ToString().Trim();
        }

        public string ConvertDigit(byte byteValue)
        {
            if (_charMapDateDigit.TryGetValue(byteValue, out var digit))
                return digit;
            return "--"; // Default to "--" if digit not found
        }


    }
}
