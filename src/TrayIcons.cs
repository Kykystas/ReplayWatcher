using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ReplayWatcher
{
    internal sealed class TrayIcons : IDisposable
    {
        private readonly List<MemoryStream> iconStreams = new List<MemoryStream>();
        internal readonly Icon On, Off, Unknown;

        internal TrayIcons() { On = Chameleon(0); Off = Chameleon(1); Unknown = Chameleon(2); }

        private Icon Chameleon(int state)
        {
            using (var stream = typeof(TrayIcons).Assembly.GetManifestResourceStream("ReplayWatcher.AppIcon"))
            using (Icon source = new Icon(stream, SystemInformation.SmallIconSize))
            using (Bitmap bitmap = source.ToBitmap())
            {
                // Render state colors from the embedded icon, preserving its silhouette and alpha.
                for (int y = 0; y < bitmap.Height; y++)
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        Color pixel = bitmap.GetPixel(x, y);
                        if (pixel.A == 0 || state == 0) continue;
                        if (state == 1 && pixel.G > pixel.R * 1.1 && pixel.G > pixel.B * 1.1)
                            bitmap.SetPixel(x, y, Color.FromArgb(pixel.A, pixel.G, (int)(pixel.R * 0.24 + pixel.B * 0.1), (int)(pixel.B * 0.35 + pixel.R * 0.18)));
                        else if (state == 2)
                        {
                            int gray = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);
                            bitmap.SetPixel(x, y, Color.FromArgb(pixel.A, gray, gray, gray));
                        }
                    }
                // Use an uncompressed DIB: .NET Framework corrupts small PNG-based ICO frames.
                var data = new MemoryStream();
                try
                {
                    int width = bitmap.Width, height = bitmap.Height;
                    int maskStride = ((width + 31) / 32) * 4;
                    byte[] mask = new byte[maskStride * height];
                    int imageSize = width * height * 4 + mask.Length;
                    using (var writer = new BinaryWriter(data, System.Text.Encoding.UTF8, true))
                    {
                        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)1);
                        writer.Write((byte)width); writer.Write((byte)height);
                        writer.Write((byte)0); writer.Write((byte)0);
                        writer.Write((ushort)1); writer.Write((ushort)32);
                        writer.Write(40 + imageSize); writer.Write(22);
                        writer.Write(40); writer.Write(width); writer.Write(height * 2);
                        writer.Write((ushort)1); writer.Write((ushort)32);
                        writer.Write(0); writer.Write(imageSize);
                        writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
                        for (int y = height - 1; y >= 0; y--)
                            for (int x = 0; x < width; x++)
                            {
                                Color pixel = bitmap.GetPixel(x, y);
                                writer.Write(pixel.B); writer.Write(pixel.G); writer.Write(pixel.R); writer.Write(pixel.A);
                                if (pixel.A == 0) mask[(height - 1 - y) * maskStride + x / 8] |= (byte)(128 >> (x % 8));
                            }
                        writer.Write(mask);
                    }
                    data.Position = 0;
                    var icon = new Icon(data, bitmap.Size);
                    iconStreams.Add(data); return icon;
                }
                catch { data.Dispose(); throw; }
            }
        }

        public void Dispose()
        {
            On.Dispose(); Off.Dispose(); Unknown.Dispose();
            foreach (var stream in iconStreams) stream.Dispose();
            iconStreams.Clear();
        }
    }
}
