using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Reflection;
using System.Drawing.Text;
using System.Windows.Forms;
using System.IO.Compression;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Registry = Microsoft.Win32.Registry;
using RegistryKey = Microsoft.Win32.RegistryKey;
using PaintDotNet;
using PaintDotNet.AppModel;
using PaintDotNet.Clipboard;
using PaintDotNet.IndirectUI;
using PaintDotNet.Collections;
using PaintDotNet.PropertySystem;
using PaintDotNet.Rendering;
using IntSliderControl = System.Int32;
using CheckboxControl = System.Boolean;
using TextboxControl = System.String;
using DoubleSliderControl = System.Double;
using ListBoxControl = System.Byte;
using RadioButtonControl = System.Byte;
using MultiLineTextboxControl = System.String;
using LabelComment = System.String;
using LayerControl = System.Int32;
using System.Drawing.Imaging;

[assembly: AssemblyTitle("MyScript plugin for Paint.NET")]
[assembly: AssemblyDescription("MyScript FileType")]
[assembly: AssemblyConfiguration("myscript")]
[assembly: AssemblyCompany("micha")]
[assembly: AssemblyProduct("MyScript")]
[assembly: AssemblyCopyright("Copyright ©2024 by micha")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("0.1.*")]
[assembly: AssemblyMetadata("BuiltByCodeLab", "Version=6.12.8807.38030")]
[assembly: SupportedOSPlatform("Windows")]

namespace MyScriptFileType
{
    public sealed class MyScriptFactory : IFileTypeFactory
    {
        public FileType[] GetFileTypeInstances()
        {
            return new[] { new MyScriptPlugin() };
        }
    }

    public class MyScriptSupportInfo : IPluginSupportInfo
    {
        public string Author => base.GetType().Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright;
        public string Copyright => base.GetType().Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
        public string DisplayName => base.GetType().Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product;
        public Version Version => base.GetType().Assembly.GetName().Version;
        public Uri WebsiteUri => new Uri("https://www.getpaint.net/redirect/plugins.html");
    }

    [PluginSupportInfo<MyScriptSupportInfo>(DisplayName = "MyScript")]
    internal class MyScriptPlugin : PropertyBasedFileType
    {
        public const string ImageSizeString = "Image Size";

        internal MyScriptPlugin()
            : base(
                "MyScript",
                new FileTypeOptions
                {
                    LoadExtensions = new string[] { ".ico" },
                    SaveExtensions = new string[] { ".ico" },
                    SupportsCancellation = true,
                    SupportsLayers = false
                })
        {
            RandomNumberInstanceSeed = unchecked((uint)DateTime.Now.Ticks);
        }

        public enum ImageSize
        {
            Icon_Auto,
            Icon_16x16,
            Icon_32x32,
            Icon_48x48,
            Icon_64x64,
            Icon_128x128,
            Icon_256x256
        }

        #region Random Number Support
        private readonly uint RandomNumberInstanceSeed;
        private uint RandomNumberRenderSeed = 0;

        internal static class RandomNumber
        {
            public static uint InitializeSeed(uint iSeed, float x, float y)
            {
                return CombineHashCodes(
                    iSeed,
                    CombineHashCodes(
                        Hash(Unsafe.As<float, uint>(ref x)),
                        Hash(Unsafe.As<float, uint>(ref y))));
            }

            public static uint InitializeSeed(uint instSeed, Point2Int32 scenePos)
            {
                return CombineHashCodes(
                    instSeed,
                    CombineHashCodes(
                        Hash(unchecked((uint)scenePos.X)),
                        Hash(unchecked((uint)scenePos.Y))));
            }

            public static uint Hash(uint input)
            {
                uint state = input * 747796405u + 2891336453u;
                uint word = ((state >> (int)((state >> 28) + 4)) ^ state) * 277803737u;
                return (word >> 22) ^ word;
            }

            public static float NextFloat(ref uint seed)
            {
                seed = Hash(seed);
                return (seed >> 8) * 5.96046448E-08f;
            }

            public static int NextInt32(ref uint seed)
            {
                seed = Hash(seed);
                return unchecked((int)seed);
            }

            public static int NextInt32(ref uint seed, int maxValue)
            {
                seed = Hash(seed);
                return unchecked((int)(seed & 0x80000000) % maxValue);
            }

            public static int Next(ref uint seed)
            {
                seed = Hash(seed);
                return unchecked((int)seed);
            }

            public static int Next(ref uint seed, int maxValue)
            {
                seed = Hash(seed);
                return unchecked((int)(seed & 0x80000000) % maxValue);
            }

            public static byte NextByte(ref uint seed)
            {
                seed = Hash(seed);
                return (byte)(seed & 0xFF);
            }

            private static uint CombineHashCodes(uint hash1, uint hash2)
            {
                uint result = hash1;
                result = ((result << 5) + result) ^ hash2;
                return result;
            }
        }
        #endregion


        public override PropertyCollection OnCreateSavePropertyCollection()
        {
            List<Property> props = new List<Property>();

            props.Add(StaticListChoiceProperty.CreateForEnum(ImageSizeString, ImageSize.Icon_Auto));

            return new PropertyCollection(props);
        }

        public override ControlInfo OnCreateSaveConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultSaveConfigUI(props);

            return configUI;
        }

        protected override void OnSaveT(Document input, Stream output, PropertyBasedSaveConfigToken token, Surface scratchSurface, ProgressEventHandler progressCallback)
        {

            //SaveImage(input, output, token, scratchSurface, progressCallback);

            scratchSurface.Clear();
            input.CreateRenderer().Render(scratchSurface);

            Bitmap applyPixels = new Bitmap(scratchSurface.Width, scratchSurface.Height);

            //loop Width
            for (int i = 0; i < applyPixels.Width; i++)
            {
                //loop Height
                for (int j = 0; j < applyPixels.Height; j++)
                {
                    applyPixels.SetPixel(i, j, scratchSurface[i, j]);
                }
            }

            //Resize image
            ImageSize bitDepth = (ImageSize)token.GetProperty(ImageSizeString).Value;
            switch (bitDepth)
            {
                case ImageSize.Icon_Auto:
                case ImageSize.Icon_32x32:
                    applyPixels = new Bitmap(applyPixels, 32, 32);
                    break;
                case ImageSize.Icon_16x16:
                    applyPixels = new Bitmap(applyPixels, 16, 16);
                    break;
                case ImageSize.Icon_48x48:
                    applyPixels = new Bitmap(applyPixels, 48, 48);
                    break;
                case ImageSize.Icon_64x64:
                    applyPixels = new Bitmap(applyPixels, 64, 64);
                    break;
                case ImageSize.Icon_128x128:
                    applyPixels = new Bitmap(applyPixels, 128, 128);
                    break;
                case ImageSize.Icon_256x256:
                    applyPixels = new Bitmap(applyPixels, 256, 256);
                    break;
                default:
                    applyPixels = new Bitmap(applyPixels, 32, 32);
                    break;
            }

            BinaryWriter iconWriter = new BinaryWriter(output);

            //Check for any null streams
            if (iconWriter == null || output == null)
                return;

            MemoryStream memoryStream = new MemoryStream();
            applyPixels.Save(memoryStream, ImageFormat.Png);

            //https://fileformats.fandom.com/wiki/Icon
            // Icon file format

            // 0-1 reserved, 0
            iconWriter.Write((short)0);

            // 2-3 image type, 1 = icon, 2 = cursor
            iconWriter.Write((short)1);

            // 4-5 number of images
            iconWriter.Write((short)1);

            // 0 image width
            iconWriter.Write((byte)applyPixels.Width);

            // 1 image height
            iconWriter.Write((byte)applyPixels.Height);

            // 2 number of colors
            iconWriter.Write((byte)0);

            // 3 reserved
            iconWriter.Write((byte)0);

            // 4-5 color planes
            iconWriter.Write((short)0);

            // 6-7 bits per pixel
            iconWriter.Write((short)32);

            // 8-11 size of image data
            iconWriter.Write((int)memoryStream.Length);

            // 12-15 offset of image data
            iconWriter.Write((int)22);

            iconWriter.Write(memoryStream.ToArray());
            memoryStream.Close();

            iconWriter.Flush();
        }

        protected override Document OnLoad(Stream input)
        {
            //return LoadImage(input);

            Icon newIcon = new Icon(input);
            Bitmap bitmapOfIcon = newIcon.ToBitmap();

            Document doc = null;

            if (bitmapOfIcon.Width > 0 && bitmapOfIcon.Height > 0)
            {
                doc = new Document(bitmapOfIcon.Width, bitmapOfIcon.Height);

                BitmapLayer layer = Layer.CreateBackgroundLayer(bitmapOfIcon.Width, bitmapOfIcon.Height);

                Surface surface = layer.Surface;

                for (int y = 0; y < surface.Height; y++)
                {
                    for (int x = 0; x < surface.Width; x++)
                    {
                        surface[x, y] = bitmapOfIcon.GetPixel(x, y);
                    }
                }

                doc.Layers.Add(layer);
            }

            return doc;
        }
        #region User Entered Code
        // Name:
        // Author:
        // Version:
        // Desc:
        // URL:
        // LoadExtns: .ico
        // SaveExtns: .ico
        // Flattened: false
        #region UICode
        CheckboxControl Amount1 = false; // Checkbox Description
        #endregion

        private const string HeaderSignature = ".PDN";

        void SaveImage(Document input, Stream output, PropertyBasedSaveConfigToken token, Surface scratchSurface, ProgressEventHandler progressCallback)
        {
            // Render a flattened view of the Document to the scratch surface.
            input.CreateRenderer().Render(scratchSurface);

            if (Amount1)
            {
                new UnaryPixelOps.Invert().Apply(scratchSurface, scratchSurface.Bounds);
            }

            // The stream paint.net hands us must not be closed.
            using (BinaryWriter writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
            {
                // Write the file header.
                writer.Write(Encoding.ASCII.GetBytes(HeaderSignature));
                writer.Write(scratchSurface.Width);
                writer.Write(scratchSurface.Height);

                for (int y = 0; y < scratchSurface.Height; y++)
                {
                    // Report progress if the callback is not null.
                    if (progressCallback != null)
                    {
                        double percent = (double)y / scratchSurface.Height;

                        progressCallback(null, new ProgressEventArgs(percent));
                    }

                    for (int x = 0; x < scratchSurface.Width; x++)
                    {
                        // Write the pixel values.
                        ColorBgra color = scratchSurface[x, y];

                        writer.Write(color.Bgra);
                    }
                }
            }
        }

        Document LoadImage(Stream input)
        {
            Document doc = null;

            // The stream paint.net hands us must not be closed.
            using (BinaryReader reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true))
            {
                // Read and validate the file header.
                byte[] headerSignature = reader.ReadBytes(4);

                if (Encoding.ASCII.GetString(headerSignature) != HeaderSignature)
                {
                    throw new FormatException("Invalid file signature.");
                }

                int width = reader.ReadInt32();
                int height = reader.ReadInt32();

                // Create a new Document.
                doc = new Document(width, height);

                // Create a background layer.
                BitmapLayer layer = Layer.CreateBackgroundLayer(width, height);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // Read the pixel values from the file.
                        uint bgraColor = reader.ReadUInt32();

                        layer.Surface[x, y] = ColorBgra.FromUInt32(bgraColor);
                    }
                }

                // Add the new layer to the Document.
                doc.Layers.Add(layer);
            }

            return doc;
        }


        #endregion
    }
}
