﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Komponent.IO;
using Kontract;
using Kontract.Attributes;
using Kontract.Interfaces;

namespace plugin_sony_images.GIM
{
    [Export(typeof(GimAdapter))]
    [Export(typeof(IImageAdapter))]
    [Export(typeof(IIdentifyFiles))]
    [Export(typeof(ILoadFiles))]
    [Export(typeof(ILoadStreams))]
    [Export(typeof(ISaveFiles))]
    [Export(typeof(ISaveStreams))]
    [PluginInfo("1AD809EE-3C0F-4837-98AD-E21EC42F29B8", "Graphic Image Map", "GIM", "IcySon55", "", "This is the GIM image adapter for Kuriimu2.")]
    [PluginExtensionInfo("*.gim")]
    public sealed class GimAdapter : IImageAdapter, IIdentifyFiles, ILoadFiles, ILoadStreams, ISaveFiles, ISaveStreams
    {
        private GIM _format;
        private List<BitmapInfo> _bitmapInfos;

        #region Properties

        [FormFieldIgnore]
        public IList<BitmapInfo> BitmapInfos => _bitmapInfos;

        #endregion

        public bool Identify(string filename)
        {
            try
            {
                // TODO: Look for potential BE files that might have the "GIM" magic
                using (var br = new BinaryReaderX(File.OpenRead(filename)))
                    return br.PeekString(3) == "MIG";
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Load(string filename)
        {
            if (File.Exists(filename))
                Load(File.OpenRead(filename));
        }

        public void Load(params Stream[] inputs)
        {
            if (inputs.Length <= 0)
                throw new Exception("No stream was provided to load from.");

            _format = new GIM(inputs[0]);
            _bitmapInfos = _format.Images.Select((i, index) => new BitmapInfo { Bitmaps = i.Select(p => p.Item1).ToList(), Name = $"{index}" }).ToList();
        }

        public async Task<bool> Encode(IProgress<ProgressReport> progress)
        {
            // TODO: Get Kanvas to encode the image and update the UI with it.
            return false;
        }

        public void Save(string filename, int versionIndex = 0)
        {
            _format.Images = _bitmapInfos.Zip(_format.Images, (b, f) => b.Bitmaps.Zip(f, (b2, f2) => (b2, f2.Item2, f2.Item3, f2.Item4, f2.Item5)).ToList()).ToList();
            using (var output = File.Create(filename))
                _format.Save(output);
        }

        public void Save(Stream output, int versionIndex = 0)
        {
            _format.Images = _bitmapInfos.Zip(_format.Images, (b, f) => b.Bitmaps.Zip(f, (b2, f2) => (b2, f2.Item2, f2.Item3, f2.Item4, f2.Item5)).ToList()).ToList();
            _format.Save(output);
        }

        public void Dispose() { }
    }
}
