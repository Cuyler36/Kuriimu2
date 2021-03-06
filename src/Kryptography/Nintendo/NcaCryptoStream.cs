﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kryptography.Nintendo
{
    public class NcaCryptoStream : Stream
    {
        private NcaKeyStorage _ncaKeyStorage;
        private Stream _stream;

        private NcaHeaderCryptoStream _ncaHeader;
        private List<SectionEntry> _bodySections;
        private NcaBodySectionCryptoStream[] _ncaBodySections;

        public bool IsHeaderEncrypted => _ncaHeader.IsHeaderEncrypted;
        public NCAVersion NCAVersion => _ncaHeader.NCAVersion;

        public NcaCryptoStream(Stream input) : this(input, @"bin\switch_keys.dat")
        {
        }

        public NcaCryptoStream(Stream input, string keyFile)
        {
            _stream = input;
            _ncaKeyStorage = new NcaKeyStorage(keyFile);

            _ncaHeader = new NcaHeaderCryptoStream(input, _ncaKeyStorage);
            var keyArea = _ncaHeader.PeekDecryptedKeyArea();

            _ncaBodySections = new NcaBodySectionCryptoStream[4];
            _bodySections = _ncaHeader.PeekSections();
            for (int i = 0; i < 4; i++)
                if (_bodySections[i].mediaOffset != 0 && _bodySections[i].endMediaOffset != 0)
                    _ncaBodySections[i] = new NcaBodySectionCryptoStream(
                        _stream,
                        _bodySections[i].mediaOffset * Common.MediaSize,
                        _bodySections[i].endMediaOffset * Common.MediaSize,
                        _ncaHeader.HasRightsId,
                        _ncaHeader.PeekMasterKeyRev(),
                        _ncaHeader.PeekSectionCryptoType(i),
                        keyArea,
                        _ncaKeyStorage,
                        _ncaHeader.PeekSectionCtr(i).Reverse().ToArray());
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => _stream.Length;

        public override long Position { get => _stream.Position; set => Seek(value, SeekOrigin.Begin); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = 0;

            //Header reading
            if (Position < Common.NcaHeaderSize)
            {
                read = _ncaHeader.Read(buffer, offset, (int)Math.Min(count, Common.NcaHeaderSize - Position));
                if (read >= count)
                    return read;
            }

            //Body reading
            while (read < count && Position < Length)
            {
                if (_bodySections.AnyInRange(Position))
                {
                    var sectionIndex = _bodySections.GetInRangeIndex(Position);

                    var toRead = Math.Min(count - read, _bodySections[sectionIndex].endMediaOffset * Common.MediaSize - Position);

                    read += _ncaBodySections[sectionIndex].Read(buffer, offset + read, (int)toRead);
                }
                else
                {
                    var nextSection = _bodySections.Where(s => Position < s.mediaOffset * Common.MediaSize && s.mediaOffset != 0 && s.endMediaOffset != 0).OrderBy(s => s.mediaOffset).FirstOrDefault();
                    var nextSectionOffset = (nextSection == null) ? Length : nextSection.mediaOffset * Common.MediaSize;

                    var toRead = Math.Min(count - read, nextSectionOffset - Position);

                    read += _stream.Read(buffer, offset + read, (int)toRead);
                }
            }

            Position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}