// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using SharpDX.WIC;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenSlideServer
{
    class Utilities
    {
        public static byte[] ImageDetails(string name)
        {
            var osr = OpenSlideInterface.Openslide_open(name);

            if (osr == IntPtr.Zero)
            {
                return null;
            }

            var vendor = Marshal.PtrToStringAnsi(OpenSlideInterface.Openslide_detect_vendor(name));
            var levels = OpenSlideInterface.Openslide_get_level_count(osr);

            var widths = new Int64[levels];
            var heights = new Int64[levels];
            var dimensions = "[\r\n\t\t";

            var offset = LoadText(name + ".txt");

            if (offset == null)
            {
                offset = "\"0,0\"";
            }

            unsafe
            {
                Int64 w, h;
                Int64* pw = &w;
                Int64* ph = &h;

                for (var l = 0; l < levels; l++)
                {
                    OpenSlideInterface.Openslide_get_level_dimensions(osr, l, pw, ph);
                    widths[l] = w;
                    heights[l] = h;
                    dimensions += "\"" + l.ToString() + "," 
                        + w.ToString() + "," 
                        + h.ToString() + "\"" 
                        + ((l < levels - 1) ? ",\r\n\t\t" : "");
                }
            }
            dimensions += "\r\n\t]";

            OpenSlideInterface.Openslide_close(osr);

            var json = "{\r\n\t\"Name\":\"" + Path.GetFileName(name)
                + "\", \r\n\t\"Vendor\":\"" + vendor
                + "\", \r\n\t\"Levels\":" + levels.ToString()
                + ", \r\n\t\"Width\":" + widths[0].ToString()
                + ", \r\n\t\"Height\":" + heights[0].ToString()
                + ", \r\n\t\"Dimensions\":" + dimensions
                + ", \r\n\t\"Offset\":" + offset + "\r\n}";

            return Encoding.ASCII.GetBytes(json);
        }

        public static byte[] CreateRegion(string name, Int32 level, Int64 x, Int64 y, Int32 w, Int32 h, string format)
        {
            var osr = OpenSlideInterface.Openslide_open(name);

            if (osr == IntPtr.Zero)
            {
                Console.WriteLine("osr == IntPtr.Zero");
                return null;
            }

            var buffer = Marshal.AllocHGlobal(4 * w * h);

            OpenSlideInterface.Openslide_read_region(osr, buffer, x, y, level, w, h);
            OpenSlideInterface.Openslide_close(osr);

            byte[] bytes = null;

            if (format == null || format.Equals("PNG"))
            {
                bytes = CreateFormat(buffer, w, h, System.Drawing.Imaging.ImageFormat.Png);
            }
            else if (format.Equals("RAW"))
            {
                bytes = CreateRaw(buffer, w, h);
            }
            else if (format.Equals("JPG"))
            {
                bytes = CreateFormat(buffer, w, h, System.Drawing.Imaging.ImageFormat.Jpeg);
            }
            else if (format.Equals("BMP"))
            {
                bytes = CreateFormat(buffer, w, h, System.Drawing.Imaging.ImageFormat.Bmp);
            }

            Marshal.FreeHGlobal(buffer);

            return bytes;
        }

        static byte[] CreateFormat(IntPtr pixels, int width, int height, System.Drawing.Imaging.ImageFormat format)
        {
            using (var bitmap = new System.Drawing.Bitmap(width, height, width * 4, System.Drawing.Imaging.PixelFormat.Format32bppArgb, pixels))
            {
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, format);
                    return stream.ToArray();
                }
            }           
        }

        static byte[] CreateRaw(IntPtr pixels, int width, int height)
        {
            using (var bitmap = new System.Drawing.Bitmap(width, height, width * 4, System.Drawing.Imaging.PixelFormat.Format32bppArgb, pixels))
            {
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);

                    var factory = new ImagingFactory2();
                    using (var decoder = new BitmapDecoder(factory, stream, DecodeOptions.CacheOnDemand))
                    {
                        using (var formatConverter = new FormatConverter(factory))
                        {
                            formatConverter.Initialize(decoder.GetFrame(0), PixelFormat.Format32bppPRGBA);

                            var stride = formatConverter.Size.Width * 4;
                            using (var dataStream = new SharpDX.DataStream(formatConverter.Size.Height * stride, true, true))
                            {
                                formatConverter.CopyPixels(stride, dataStream);

                                byte[] b;

                                using (BinaryReader br = new BinaryReader(dataStream))
                                {
                                    b = br.ReadBytes((int)dataStream.Length);
                                }

                                return b;
                            }                                                    
                        }
                    }
                }
            }
        }

        public static byte[] LoadFile(string fileName)
        {
            if (File.Exists(fileName))
            {
                return File.ReadAllBytes(fileName);
            }
            return null;
        }

        public static string LoadText(string fileName)
        {
            if (File.Exists(fileName))
            {
                return File.ReadAllText(fileName);
            }
            return null;
        }

        public static void SaveFile(string fileName, byte[] data)
        {
            File.WriteAllBytes(fileName, data);
        }
    }
}
