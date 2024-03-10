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
using PaintDotNet.Imaging;
using System.Drawing.Imaging;

[assembly: AssemblyTitle("Iconify plugin for Paint.NET")]
[assembly: AssemblyDescription("Iconify FileType")]
[assembly: AssemblyConfiguration("iconify")]
[assembly: AssemblyCompany("Michael Baier")]
[assembly: AssemblyProduct("Iconify")]
[assembly: AssemblyCopyright("Copyright ©2024 by Michael Baier")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("0.1.*")]
[assembly: AssemblyMetadata("BuiltByCodeLab", "Version=6.12.8807.38030")]
[assembly: SupportedOSPlatform("Windows")]

namespace IconifyFileType
{
    public enum ImageSize
    {
        IconAuto,
        Icon_16x16 = 16,
        Icon_32x32 = 32,
        Icon_48x48 = 48,
        Icon_64x64 = 64,
        Icon_128x128 = 128,
        Icon_256x256 = 256
    }

    public sealed class IconifyFactory : IFileTypeFactory
    {
        public FileType[] GetFileTypeInstances()
        {
            return new[] { new IconifyPlugin() };
        }
    }

    public class IconifySupportInfo : IPluginSupportInfo
    {
        public string Author => base.GetType().Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright;
        public string Copyright => base.GetType().Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
        public string DisplayName => base.GetType().Assembly.GetCustomAttribute<AssemblyProductAttribute>().Product;
        public Version Version => base.GetType().Assembly.GetName().Version;
        public Uri WebsiteUri => new Uri("https://www.getpaint.net/redirect/plugins.html");
    }

    [PluginSupportInfo<IconifySupportInfo>(DisplayName = "Iconify")]
    internal class IconifyPlugin : PropertyBasedFileType
    {
        public const string ImageSizeString = "Image Size";

        internal IconifyPlugin()
            : base(
                "Iconify",
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
            props.Add(StaticListChoiceProperty.CreateForEnum(ImageSizeString, ImageSize.IconAuto));
            return new PropertyCollection(props);
        }

        public override ControlInfo OnCreateSaveConfigUI(PropertyCollection props)
        {
            ControlInfo config = CreateDefaultSaveConfigUI(props);

            return config;
        }

        protected override void OnSaveT(Document input, Stream output, PropertyBasedSaveConfigToken token, Surface scratchSurface, ProgressEventHandler progressCallback)
        {
            SaveImage(input, output, token, scratchSurface, progressCallback);
        }

        protected override Document OnLoad(Stream input)
        {
            return LoadImage(input);
        }

        #region User Entered Code
        // Name:
        // Author:
        // Version:
        // Desc:
        // URL:
        // LoadExtns: .foo, .bar
        // SaveExtns: .foo, .bar
        // Flattened: false

        private const string HeaderSignature = ".PDN";

        void SaveImage(Document input, Stream output, PropertyBasedSaveConfigToken token, Surface scratchSurface, ProgressEventHandler progressCallback)
        {
            // Render a flattened view of the Document to the scratch surface.
            input.CreateRenderer().Render(scratchSurface);

            // create a new bitmap
            Bitmap bmp = new Bitmap(scratchSurface.Width, scratchSurface.Height);
            for (int y = 0; y < scratchSurface.Height; y++)
            {
                for (int x = 0; x < scratchSurface.Width; x++)
                {
                    // Read the pixel values from the bitmap
                    bmp.SetPixel(x, y, scratchSurface[x, y]);
                }
            }

            ImageSize imageSize = (ImageSize)token.GetProperty(ImageSizeString).Value;
            int pixelSize = imageSize switch
            {
                ImageSize.IconAuto or ImageSize.Icon_32x32 => 32,
                ImageSize.Icon_16x16 => 16,
                ImageSize.Icon_48x48 => 48,
                ImageSize.Icon_64x64 => 64,
                ImageSize.Icon_128x128 => 128,
                ImageSize.Icon_256x256 => 256,
                _ => 32
            };

            // Create icon
            ConvertToIco(bmp, pixelSize, output);
        }

        /// <summary>
        /// https://stackoverflow.com/questions/17212704/convert-image-to-icon-in-c-sharp
        /// </summary>
        /// <param name="img"></param>
        /// <param name="size"></param>
        /// <param name="outStream"></param>
        private void ConvertToIco(Bitmap img, int size, Stream outStream)
        {
            var bmp = new Bitmap(img, size, size);
            var bw = new BinaryWriter(outStream);
            using (var msImg = new MemoryStream())
            {
                img.Save(msImg, ImageFormat.Png);
                bw.Write((short)0);           //0-1 reserved
                bw.Write((short)1);           //2-3 image type, 1 = icon, 2 = cursor
                bw.Write((short)1);           //4-5 number of images
                bw.Write((byte)size);         //6 image width
                bw.Write((byte)size);         //7 image height
                bw.Write((byte)0);            //8 number of colors
                bw.Write((byte)0);            //9 reserved
                bw.Write((short)0);           //10-11 color planes
                bw.Write((short)32);          //12-13 bits per pixel
                bw.Write((int)msImg.Length);  //14-17 size of image data
                bw.Write(22);                 //18-21 offset of image data
                bw.Write(msImg.ToArray());    // write image data
                bw.Flush();
                bw.Seek(0, SeekOrigin.Begin);
            }
        }


        Document LoadImage(Stream input)
        {
            Document doc = null;

            Icon icon = new Icon(input);
            Bitmap bmp = icon.ToBitmap();

            doc = new Document(bmp.Width, bmp.Height);

            // Create a background layer.
            BitmapLayer layer = Layer.CreateBackgroundLayer(bmp.Width, bmp.Height);

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    // Read the pixel values from the bitmap
                    layer.Surface[x, y] = bmp.GetPixel(x, y);
                }
            }

            // Add the new layer to the Document.
            doc.Layers.Add(layer);

            return doc;
        }


        #endregion
    }
}
