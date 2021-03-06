﻿using System;
using System.Numerics;
using System.Security.Cryptography;

namespace Kryptography.AES.CTR
{
    public class CtrCryptoTransform : ICryptoTransform
    {
        public byte[] IV { get; set; }
        private ICryptoTransform _cryptor;

        private bool _firstTransform = true;
        private bool _littleEndianCtr;

        public CtrCryptoTransform(ICryptoTransform cryptor, byte[] iv, bool littleEndianCtr)
        {
            IV = iv;
            _cryptor = cryptor;
            _littleEndianCtr = littleEndianCtr;
        }

        public int InputBlockSize => 16;

        public int OutputBlockSize => 16;

        public bool CanTransformMultipleBlocks => true;

        public bool CanReuseTransform => true;

        public void Dispose()
        {
            _cryptor.Dispose();
        }

        public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
        {
            Validate();

            var ivToUse = GetFirstToUseIV(inputOffset);

            Process(inputBuffer, inputOffset, inputCount, outputBuffer, outputOffset, ivToUse);

            _firstTransform = false;
            return inputCount;
        }

        private byte[] GetFirstToUseIV(int offset)
        {
            var increase = offset / InputBlockSize;
            if (increase == 0) return IV;

            var buffer = new byte[InputBlockSize];
            Array.Copy(IV, 0, buffer, 0, InputBlockSize);
            buffer.Increment(increase, _littleEndianCtr);

            return buffer;
        }

        private void Process(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset, byte[] iv)
        {
            var alignedCount = (int)Math.Ceiling((double)inputCount / InputBlockSize) * InputBlockSize;

            var encryptedIVs = CreateXORPad(iv, alignedCount);

            XORData(inputBuffer, inputOffset, inputCount, outputBuffer, outputOffset, encryptedIVs);
        }

        private void XORData(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset, byte[] encryptedIVs)
        {
            var simdLength = Vector<byte>.Count;
            var j = 0;
            for (j = 0; j <= inputCount - simdLength; j += simdLength)
            {
                var va = new Vector<byte>(inputBuffer, j + inputOffset);
                var vb = new Vector<byte>(encryptedIVs, j);
                (va ^ vb).CopyTo(outputBuffer, j + outputOffset);
            }

            for (; j < inputCount; ++j)
            {
                outputBuffer[outputOffset + j] = (byte)(inputBuffer[inputOffset + j] ^ encryptedIVs[j]);
            }
        }

        private byte[] CreateXORPad(byte[] initialIV, int alignedCount)
        {
            var ivs = new byte[alignedCount];
            for (int i = 0; i < alignedCount; i += InputBlockSize)
            {
                Array.Copy(initialIV, 0, ivs, i, InputBlockSize);
                initialIV.Increment(1, _littleEndianCtr);
            }
            var encryptedIVs = new byte[alignedCount];
            _cryptor.TransformBlock(ivs, 0, alignedCount, encryptedIVs, 0);

            return encryptedIVs;
        }

        private void Validate()
        {
            if (!_firstTransform && !CanReuseTransform)
                throw new NotSupportedException("Can't reuse transform.");
        }

        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            var outputBuffer = new byte[inputCount];
            TransformBlock(inputBuffer, inputOffset, inputCount, outputBuffer, 0);

            return outputBuffer;
        }
    }
}