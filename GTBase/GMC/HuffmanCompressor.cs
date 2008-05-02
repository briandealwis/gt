using System;
using GT.Utils;

namespace GT.GMC
{
    public class HuffmanCompressor
    {
        #region Delegates and Events for Announcements
        
        public delegate void HuffmanFrequenciesChange(short templateId, uint[] frequencies);

        public event HuffmanFrequenciesChange HuffmanFrequenciesChanged;

        #endregion

        private short templateId;

        /// <summary>
        /// Has the byte frequency changed since the Huffman encoding was last updated?
        /// </summary>
        private bool changed = false;

        /// <summary>
        /// Should huffman encoding be used?  (Distinct from: can huffman encoding be used?
        /// determined by whether a huffman-encoding tree has been generated.)
        /// </summary>
        private bool useHuff = false;

        /// <summary>
        /// The per-byte usage.
        /// </summary>
        private uint[] byteUsage = new uint[256];

        /// <summary>
        /// The generated huffman-encoding tree
        /// </summary>
        private HuffmanEncodingTree het;

        /// <summary>
        /// Statistics: record how many trees have been generated
        /// </summary>
        public int huffedTrees = 0;

        /// <summary>
        /// Set whether huffman encoding should be used.
        /// </summary>
        /// <param name="setting">true if data should be huffman-encoded, false otherwise.</param>
        public bool HuffmanEncoding
        {
            get { return useHuff; }
            set
            {
                useHuff = value;
            }
        }

        public HuffmanCompressor(short tid)
        {
            templateId = tid;
        }

        /// <summary>
        /// Return true if huffman encoding is active and *available* (e.g., a tree was made).
        /// </summary>
        /// <returns></returns>
        public bool HuffmanActive()
        {
            return useHuff && het != null;
        }

        /// <summary>
        /// Turn a sequence of shortcuts for this compressor into a huff-encoding.
        /// Return null if we're not currently active.
        /// </summary>
        /// <param name="bytes">The bytes to be Huffman encoded</param>
        /// <returns></returns>
        public byte[] Encode(byte[] bytes)
        {
            // Note the byte usages to inform future huffman encodings
            foreach (byte b in bytes) { NoteUsage(b); }

            if (!HuffmanActive()) { return null; }
            return het.EncodeArray(bytes);
        }

        /// <summary>
        /// Turn huffed values back into a sequence of shortcuts.
        /// </summary>
        /// <param name="huffedBytes">the huffman-encoded bytes</param>
        /// <returns>expanded set of bytes</returns>
        public byte[] Decode(byte[] huffedBytes)
        {
            if (het == null) { return null; }
            return het.DecodeArray(huffedBytes);
        }

        /// <summary>
        /// Update the usage values 
        /// </summary>
        /// <param name="newusages"></param>
        public void SetFrequencies(uint[] newusages)
        {
            changed = true;
            byteUsage = newusages;
            UpdateHuffmanEncoding();
        }

        /// <summary>
        /// Build/update a huffman encoding tree (if necessary) for this set of shortcuts 
        /// given the current set of byte usages.
        /// </summary>
        public bool UpdateHuffmanEncoding()
        {
            if (!useHuff) { return false; }
            if (het != null && !changed) { return false; }  // already generated and no change
            changed = false;
            het = new HuffmanEncodingTree(byteUsage);
            huffedTrees++;
            if (HuffmanFrequenciesChanged != null)
            {
                HuffmanFrequenciesChanged(templateId, byteUsage);
            }
            byteUsage = new uint[256];   // ensure huffman encoding is based on recent usage
            return true;
        }

        /// <summary>
        /// Note usage of a particular byte value; note that the Huffman tree will require
        /// being updated
        /// </summary>
        /// <param name="value">the byte usd</param>
        public void NoteUsage(byte value)
        {
            changed = true;
            ++byteUsage[(int)value];
        }
    }
}