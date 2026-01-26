//Copyright (c) 2020 KEYENCE CORPORATION. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LJXA_ImageAcquisitionSample
{
    public static class TiffConverter
    {
        public static void Save(string savePath, List<ushort> image, int lines, int width)
        {
            using (FileStream stream = new FileStream(savePath, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                WriteTiffHeader(stream, (uint)lines, (uint)width);
                writer.Write(image.SelectMany(BitConverter.GetBytes).ToArray());
            }
        }

        public static void Save(string savePath, List<float> image, int lines, int width)
        {
            using (FileStream stream = new FileStream(savePath, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                WriteTiffHeader(stream, (uint)lines, (uint)width);
                writer.Write(image.SelectMany(BitConverter.GetBytes).ToArray());
            }
        }

        private static void WriteTiffHeader(Stream stream, uint lines, uint width)
        {
            // <header(8)> + <tag count(2)> + <tag(12)>*11 + <next IFD(4)> + <resolution unit(8)>*2
            const uint stripOffset = 162;

            stream.Position = 0;
            // Header (little endian)
            stream.Write(new byte[] { 0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00 }, 0, 8);

            // Tag count
            stream.Write(new byte[] { 0x0B, 0x00 }, 0, 2);

            // Image Width
            WriteTiffTag(stream, 0x0100, 3, 1, width);

            // Image Length
            WriteTiffTag(stream, 0x0101, 3, 1, lines);

            // Bits per sample
            WriteTiffTag(stream, 0x0102, 3, 1, 16);

            // Compression (no compression)
            WriteTiffTag(stream, 0x0103, 3, 1, 1);

            // Photometric interpretation (white mode & monochrome)
            WriteTiffTag(stream, 0x0106, 3, 1, 1);

            // Strip offsets
            WriteTiffTag(stream, 0x0111, 3, 1, stripOffset);

            // Rows per strip
            WriteTiffTag(stream, 0x0116, 3, 1, lines);

            // strip byte counts
            WriteTiffTag(stream, 0x0117, 4, 1, width * lines * 2);

            // X resolution address
            WriteTiffTag(stream, 0x011A, 5, 1, stripOffset - 16);

            // Y resolution address
            WriteTiffTag(stream, 0x011B, 5, 1, stripOffset - 8);

            // Resolution unit (inch)
            WriteTiffTag(stream, 0x0128, 3, 1, 2);

            // Next IFD
            stream.Write(BitConverter.GetBytes(0), 0, 4);

            // X resolution and Y resolution
            stream.Write(BitConverter.GetBytes(96), 0, 4);
            stream.Write(BitConverter.GetBytes(1), 0, 4);
            stream.Write(BitConverter.GetBytes(96), 0, 4);
            stream.Write(BitConverter.GetBytes(1), 0, 4);
        }

        private static void WriteTiffTag(Stream stream, ushort kind, ushort dataType, uint dataSize, uint data)
        {
            List<byte> list = new List<byte>();
            list.AddRange(BitConverter.GetBytes(kind));
            list.AddRange(BitConverter.GetBytes(dataType));
            list.AddRange(BitConverter.GetBytes(dataSize));
            list.AddRange(BitConverter.GetBytes(data));
            byte[] tag = list.ToArray();
            stream.Write(tag, 0, tag.Length);
        }
    }
}
