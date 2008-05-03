using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace GT.Utils
{
    /// <summary>
    /// A growable list of bits, intended to be compatible with BitArray.
    /// </summary>
    public class BitTuple
    {
        private List<byte> bytes;
        private int length;

        public BitTuple()
        {
            bytes = new List<byte>(4);
            length = 0;
        }

        public BitTuple(int setBits) : this(setBits, setBits) { }

        /// <summary>
        /// Create a new bit tuple with <c>setBits</c> allocated (and zero'd) 
        /// and space for <c>estimatedBits</c>.</c>
        /// </summary>
        /// <param name="setBits">number of bits to be set</param>
        /// <param name="estimatedBits">initially allocated space</param>
        public BitTuple(int setBits, int estimatedBits)
        {
            bytes = new List<byte>((estimatedBits + 7) / 8);
            for (int i = 0; i < (setBits + 7) / 8; i++) { bytes.Add(0); }
            length = setBits;
        }

        public BitTuple(BitArray bs) : this(bs, bs.Length) { }

        public BitTuple(BitArray bs, int numBits)
            : this(numBits)
        {
            for (int i = 0; i < numBits; i++) { this[i] = bs[i]; }
        }

        public BitTuple(bool[] bits) : this(bits, bits.Length) { }
        public BitTuple(bool[] bits, int numBits) : this(numBits)
        {
            for (int i = 0; i < numBits; i++) { this[i] = bits[i]; }
        }

        public BitTuple(byte[] bytes) : this(bytes, bytes.Length * 8) {}
        public BitTuple(byte[] bytes, int numBits) : this(numBits) 
        {
            for (int i = 0; i < numBits; i++) { 
                this[i] = (bytes[i / 8] & (byte)(1 << (i % 8))) != 0; 
            }
        }

        public int Length { get { return length; } }

        public bool this[int index]
        {
            get {
                if (index < 0 || index >= length) { throw new ArgumentException("index out of range"); }
                return (bytes[index / 8] & (byte)(1 << (index % 8))) != 0;
            }
            set
            {
                if (index < 0 || index >= length) { throw new ArgumentException("index out of range"); }
                if (value)
                {
                    bytes[index / 8] |= (byte)(1 << (index % 8));
                }
                else
                {
                    bytes[index / 8] &= (byte)(~(1 << (index % 8)));
                }
            }
        }

        public void Add(bool value)
        {
            if (length % 8 == 0) { bytes.Add(0); }
            if (value) { bytes[length / 8] |= (byte)(1 << (length % 8)); }
            length++;
        }

        public void AddAll(BitArray bits)
        {
            for (int i = 0; i < bits.Length; i++) { Add(bits[i]); }
        }

        public void AddAll(bool[] bits)
        {
            for (int i = 0; i < bits.Length; i++) { Add(bits[i]); }
        }

        /// <summary>
        /// Return the equivalent BitArray.
        /// </summary>
        /// <returns></returns>
        public BitArray ToBitArray()
        {
            BitArray result = new BitArray(length);
            for (int i = 0; i < length; i++) { result[i] = this[i]; }
            return result;
        }

        /// <summary>
        /// Return the equivalent byte array; note that any filler bits 
        /// required to meet a full byte are zero.  The format matches the
        /// bits as encoded by BitArray.
        /// </summary>
        /// <returns></returns>
        public byte[] ToArray()
        {
            byte[] result = new byte[bytes.Count];
            bytes.CopyTo(result, 0);
            return result;
        }

        override public string ToString()
        {
            StringBuilder result = new StringBuilder("BitTuple{");
            result.Append(Length);
            result.Append(" bits: ");
            for (int i = 0; i < Length; i++)
            {
                result.Append(this[i] ? '1' : '0');
            }
            result.Append("}");
            return result.ToString();
        }

    }

}
