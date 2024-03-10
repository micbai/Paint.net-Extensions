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

        public enum PropertyNames
        {
            Amount1
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

            props.Add(new BooleanProperty(PropertyNames.Amount1, false));

            return new PropertyCollection(props);
        }

        public override ControlInfo OnCreateSaveConfigUI(PropertyCollection props)
        {
            ControlInfo configUI = CreateDefaultSaveConfigUI(props);

            configUI.SetPropertyControlValue(PropertyNames.Amount1, ControlInfoPropertyNames.DisplayName, string.Empty);
            configUI.SetPropertyControlValue(PropertyNames.Amount1, ControlInfoPropertyNames.Description, "Checkbox Description");
            configUI.SetPropertyControlValue(PropertyNames.Amount1, ControlInfoPropertyNames.ShowHeaderLine, false);

            return configUI;
        }

        protected override void OnSaveT(Document input, Stream output, PropertyBasedSaveConfigToken token, Surface scratchSurface, ProgressEventHandler progressCallback)
        {
            Amount1 = token.GetProperty<BooleanProperty>(PropertyNames.Amount1).Value;

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
