using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace GT.Utils
{
    /// <summary>
    /// A set of utility functions relating to bits.
    /// </summary>
    public class BitUtils
    {
        /// <summary>
        /// A lookup table to return the position of the highest bit set
        /// for numbers on [0,256].  Taken from  Sean Anderson's 
        /// <a href="http://www-graphics.stanford.edu/~seander/bithacks.html">
        /// BitWiddling Hacks</a>.
        /// </summary>
        protected static readonly short[] HighestBitLookupTable256 =
        {
             -1, 0, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 3, 3, 3, 3,
              4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
              5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
              5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 5,
              6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
              6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
              6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
              6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6, 6,
              7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
              7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
              7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
              7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
              7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
              7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
              7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
              7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7        
        };

        /// <summary>
        /// Returns the position of the highest bit set in the provided value.
        /// Note: number of bits required is this number + 1.
        /// Taken from  Sean Anderson's 
        /// <a href="http://www-graphics.stanford.edu/~seander/bithacks.html">
        /// BitWiddling Hacks</a>.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int HighestBitSet(byte value)
        {
            return HighestBitLookupTable256[value];
        }

        /// <summary>
        /// Returns the position of the highest bit set in the provided value.
        /// Note: number of bits required is this number + 1.
        /// Taken from  Sean Anderson's 
        /// <a href="http://www-graphics.stanford.edu/~seander/bithacks.html">
        /// BitWiddling Hacks</a>.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int HighestBitSet(uint value)
        {
            int r; // r will be lg(v)
            uint t, tt; // temporaries

            if((tt = value >> 16) != 0)
            {
                r = (t = tt >> 8) != 0
                    ? 24 + HighestBitLookupTable256[t] 
                    : 16 + HighestBitLookupTable256[tt];
            }
            else
            {
                r = (t = value >> 8) != 0 
                    ? 8 + HighestBitLookupTable256[t] 
                    : HighestBitLookupTable256[value];
            }
            return r;
        }

        /// <summary>
        /// Verify whether the provided value is a power of 2.
        /// Taken from  Sean Anderson's 
        /// <a href="http://www-graphics.stanford.edu/~seander/bithacks.html">
        /// BitWiddling Hacks</a>.
        /// </summary>
        /// <returns>true if the value is a power of 2, false otherwise.
        /// Note that this implementation considers 0 to not be a power of 2.</returns>
        public static bool IsPowerOf2(uint value)
        {
            return (value & (value - 1)) == 0 && value != 0;
        }

        /// <summary>
        /// Round up the provided value to the nearest power of 2.
        /// Taken from  Sean Anderson's 
        /// <a href="http://www-graphics.stanford.edu/~seander/bithacks.html">
        /// BitWiddling Hacks</a>.
        /// </summary>
        public static uint RoundUpToPowerOf2(uint v)
        {
            // compute the next highest power of 2 of 32-bit v
            if (v == 0) { return 1; }   // otherwise 0 => 0
            v--;    // 0 - 1 => 2^32
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            return v + 1;
        }
    }

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
        /// Create a new bit tuple with <see cref="setBits"/> allocated (and zero'd) 
        /// and space for <see cref="estimatedBits"/>.
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
