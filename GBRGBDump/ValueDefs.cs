using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBRGBDump
{
    public static class Masks
    {
        public const int Capture = 0b00000010;
        public const int EdgeExclusive = 0b10000000;
        public const int EdgeOpMode = 0b01100000;
        public const int Gain = 0b00011111;
        public const int EdgeRatio = 0b01110000;
        public const int InvertOutput = 0b00001000;
        public const int VoltageRef = 0b00000111;
        public const int ZeroPoint = 0b11000000;
        public const int VoltageOut = 0b00111111;
        public const int DitherSet = 0b00000001;
        public const int DitherOnOff = 0b00000010;
    }

    public static class Values
    {
        public static readonly Dictionary<string, int> Capture = new Dictionary<string, int>
        {
            { "positive", 0b00000010 },
            { "negative", 0b00000000 }
        };

        public static readonly Dictionary<string, int> Gain = new Dictionary<string, int>
        {
            { "140", 0b00000000 }, // 14.0 (gbcam gain:  5.01)
            { "155", 0b00000001 }, // 15.5
            { "170", 0b00000010 }, // 17.0
            { "185", 0b00000011 }, // 18.5
            { "200", 0b00000100 }, // 20.0 (gbcam gain: 10.00)
            { "200Dup", 0b00010000 }, // 20.0 (d)
            { "215", 0b00000101 }, // 21.5
            { "215Dup", 0b00010001 }, // 21.5 (d)
            { "230", 0b00000110 }, // 23.0
            { "230Dup", 0b00010010 }, // 23.0 (d)
            { "245", 0b00000111 }, // 24.5
            { "245Dup", 0b00010011 }, // 24.5 (d)
            { "260", 0b00001000 }, // 26.0 (gbcam gain: 19.95)
            { "260Dup", 0b00010100 }, // 26.0 (d)
            { "275", 0b00010101 }, // 27.5
            { "290", 0b00001001 }, // 29.0
            { "290Dup", 0b00010110 }, // 29.0 (d)
            { "305", 0b00010111 }, // 30.5
            { "320", 0b00001010 }, // 32.0 (gbcam gain: 39.81)
            {
                "320Dup", 0b00011000
            }, // 32.0 (d)
            {
                "350", 0b00001011
            }, // 35.0
            {
                "350Dup", 0b00011001
            }, // 35.0 (d)
            {
                "380", 0b00001100
            }, // 38.0
            {
                "380Dup", 0b00011010
            }, // 38.0 (d)
            {
                "410", 0b00001101
            }, // 41.0
            {
                "410Dup", 0b00011011
            }, // 41.0 (d)
            {
                "440", 0b00011100
            }, // 44.0
            {
                "455", 0b00001110
            }, // 45.5
            {
                "470", 0b00011101
            }, // 47.0
            {
                "515", 0b00001111
            }, // 51.5
            {
                "515Dup", 0b00011110
            }, // 51.5 (d)
            // Add other values similarly
            { "575", 0b00011111 } // 57.5
        };

        public static readonly Dictionary<string, int> EdgeOpMode = new Dictionary<string, int>
        {
            { "none", 0b00000000 },
            { "horizontal", 0b00100000 },
            { "vertical", 0b01000000 },
            { "2d", 0b01100000 }
        };

        public static readonly Dictionary<string, int> EdgeExclusive = new Dictionary<string, int>
        {
            { "on", 0b10000000 },
            { "off", 0b00000000 }
        };

        public static readonly Dictionary<string, int> EdgeRatio = new Dictionary<string, int>
        {
            { "050", 0b00000000 },
            { "075", 0b00010000 },
            { "100", 0b00100000 },
            { "125", 0b00110000 },
            { "200", 0b01000000 },
            { "300", 0b01010000 },
            { "400", 0b01100000 },
            { "500", 0b01110000 }
        };

        public static readonly Dictionary<string, int> InvertOutput = new Dictionary<string, int>
        {
            { "on", 0b00001000 },
            { "off", 0b00000000 }
        };

        public static readonly Dictionary<string, int> VoltageRef = new Dictionary<string, int>
        {
            { "00v", 0b00000000 },
            { "05v", 0b00000001 },
            { "10v", 0b00000010 },
            { "15v", 0b00000011 },
            { "20v", 0b00000100 },
            { "25v", 0b00000101 },
            { "30v", 0b00000110 },
            { "35v", 0b00000111 }
        };

        public static readonly Dictionary<string, int> ZeroPoint = new Dictionary<string, int>
        {
            { "disabled", 0b00000000 },
            { "positive", 0b10000000 },
            { "negative", 0b01000000 }
        };

        public static readonly Dictionary<string, int> VoltageOut = new Dictionary<string, int>
        {
            { "neg992", 0b00011111 }, // -0.992mV
            { "neg960", 0b00011110 }, // -0.960mV
            { "neg928", 0b00011101 }, // -0.928mV
            { "neg896", 0b00011100 }, // -0.896mV
            { "neg864", 0b00011011 }, // -0.864mV
            { "neg832", 0b00011010 }, // -0.832mV
            { "neg800", 0b00011001 }, // -0.800mV
            { "neg768", 0b00011000 }, // -0.768mV
            { "neg736", 0b00010111 }, // -0.736mV
            { "neg704", 0b00010110 }, // -0.704mV
            { "neg672", 0b00010101 }, // -0.672mV
            { "neg640", 0b00010100 }, // -0.640mV
            { "neg608", 0b00010011 }, // -0.608mV
            { "neg576", 0b00010010 }, // -0.576mV
            { "neg544", 0b00010001 }, // -0.544mV
            { "neg512", 0b00010000 }, // -0.512mV
            { "neg480", 0b00001111 }, // -0.480mV
            { "neg448", 0b00001110 }, // -0.448mV
            { "neg416", 0b00001101 }, // -0.416mV
            { "neg384", 0b00001100 }, // -0.384mV
            { "neg352", 0b00001011 }, // -0.352mV
            { "neg320", 0b00001010 }, // -0.320mV
            { "neg288", 0b00001001 }, // -0.288mV
            { "neg256", 0b00001000 }, // -0.256mV
            { "neg224", 0b00000111 }, // -0.224mV
            { "neg192", 0b00000110 }, // -0.192mV
            { "neg160", 0b00000101 }, // -0.160mV
            { "neg128", 0b00000100 }, // -0.128mV
            { "neg096", 0b00000011 }, // -0.096mV
            { "neg064", 0b00000010 }, // -0.064mV
            { "neg032", 0b00000001 }, // -0.032mV
            { "neg000", 0b00000000 }, // -0.000mV
            { "pos000", 0b00100000 }, //  0.000mV
            { "pos032", 0b00100001 }, //  0.032mV
            { "pos064", 0b00100010 }, //  0.064mV
            { "pos096", 0b00100011 }, //  0.096mV
            { "pos128", 0b00100100 }, //  0.128mV
            { "pos160", 0b00100101 }, //  0.160mV
            { "pos192", 0b00100110 }, //  0.192mV
            { "pos224", 0b00100111 }, //  0.224mV
            { "pos256", 0b00101000 }, //  0.256mV
            { "pos288", 0b00101001 }, //  0.288mV
            { "pos320", 0b00101010 }, //  0.320mV
            { "pos352", 0b00101011 }, //  0.352mV
            { "pos384", 0b00101100 }, //  0.384mV
            { "pos416", 0b00101101 }, //  0.416mV
            { "pos448", 0b00101110 }, //  0.448mV
            { "pos480", 0b00101111 }, //  0.480mV
            { "pos512", 0b00110000 }, //  0.512mV
            { "pos544", 0b00110001 }, //  0.544mV
            { "pos576", 0b00110010 }, //  0.576mV
            { "pos608", 0b00110011 }, //  0.608mV
            { "pos640", 0b00110100 }, //  0.640mV
            { "pos672", 0b00110101 }, //  0.672mV
            { "pos704", 0b00110110 }, //  0.704mV
            { "pos736", 0b00110111 }, //  0.736mV
            { "pos768", 0b00111000 }, //  0.768mV
            { "pos800", 0b00111001 }, //  0.800mV
            { "pos832", 0b00111010 }, //  0.832mV
            { "pos864", 0b00111011 }, //  0.864mV
            { "pos896", 0b00111100 }, //  0.896mV
            { "pos928", 0b00111101 }, //  0.928mV
            { "pos960", 0b00111110 }, //  0.960mV
            { "pos992", 0b00111111 }, //  0.992mV
        };

        public static readonly Dictionary<string, int> Dither = new Dictionary<string, int>
        {
            { "setHigh", 0b00000000 },
            { "setLow", 0b00000001 },
            { "on", 0b00000000 },
            { "off", 0b00000010 }
        };
    }

    public struct RomByteOffsets
    {
        public int ThumbnailByteCapture;
        public int ThumbnailByteEdgegains;
        public int ThumbnailByteExposureHigh;
        public int ThumbnailByteExposureLow;
        public int ThumbnailByteEdmovolt;
        public int ThumbnailByteVoutzero;
        public int ThumbnailByteDitherset;
        public int ThumbnailByteContrast;
    }

    public static class ByteOffsets
    {
        public static readonly RomByteOffsets Pxlr = new RomByteOffsets
        {
            ThumbnailByteCapture = 0x00,
            ThumbnailByteEdgegains = 0x10,
            ThumbnailByteExposureHigh = 0x20,
            ThumbnailByteExposureLow = 0x30,
            ThumbnailByteEdmovolt = 0xC6,
            ThumbnailByteVoutzero = 0xD6,
            ThumbnailByteDitherset = 0xE6,
            ThumbnailByteContrast = 0xF6
        };

        public static readonly RomByteOffsets Photo = new RomByteOffsets
        {
            ThumbnailByteCapture = 0xC8,
            ThumbnailByteEdgegains = 0xC9,
            ThumbnailByteExposureHigh = 0xCB,
            ThumbnailByteExposureLow = 0xCA,
            ThumbnailByteEdmovolt = 0xCC,
            ThumbnailByteVoutzero = 0xCD,
            ThumbnailByteDitherset = 0xC8,
            ThumbnailByteContrast = 0xC8
        };
    }
}