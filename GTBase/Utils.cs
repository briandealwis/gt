using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace GT.Utils
{

    /// <summary>
    /// A simple interface, similar to <c>IEnumerator</c> for processing elements sequentially
    /// but with the ability to remove the current element.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IProcessingQueue<T>
    {
        /// <summary>
        /// Advance and return the next element.
        /// </summary>
        /// <returns>the next element to be considered</returns>
        T Current { get; }

        /// <summary>
        /// Remove the current element.
        /// </summary>
        void Remove();

        bool Empty { get; }
    }

    public class SequentialListProcessor<T> : IProcessingQueue<T>
        where T: class
    {
        protected IList<T> list;
        public SequentialListProcessor(IList<T> list)
        {
            this.list = list;
        }

        public T Current
        {
            get { return list.Count == 0 ? null : list[0]; }
        }

        public void Remove()
        {
            list.RemoveAt(0);
        }

        public bool Empty
        {
            get { return list.Count == 0; }
        }
    }

    public class PredicateListProcessor<T> : IProcessingQueue<T>
        where T: class
    {
        protected IList<T> list;
        protected T current = null;
        protected Predicate<T> predicate;

        public PredicateListProcessor(IList<T> list, Predicate<T> predicate)
        {
            this.list = list;
            this.predicate = predicate;
        }

        public T Current
        {
            get
            {
                if (current != null) { return current; }
                foreach (T element in list)
                {
                    if (predicate(element))
                    {
                        return current = element;
                    }
                }
                return null;
            }
        }

        public void Remove()
        {
            list.Remove(Current);
        }

        public bool Empty
        {
            get { return Current == null; }
        }
    }


    /// <summary>
    /// Set of useful functions for byte arrays
    /// </summary>
    public class ByteUtils
    {
        #region Debugging Utilities

        public static string DumpBytes(byte[] buffer)
        {
            return DumpBytes(buffer, 0, buffer.Length);
        }

        public static string DumpBytes(byte[] buffer, int offset, int count)
        {
            StringBuilder sb = new StringBuilder();
            for (int j = 0; j < count; j++)
            {
                if (offset + j < buffer.Length)
                {
                    sb.Append(((int)buffer[offset + j]).ToString("X2"));
                }
                else { sb.Append("  "); }
                if (j != count - 1) { sb.Append(' '); }
            }
            return sb.ToString();
        }

        public static string AsPrintable(byte[] buffer)
        {
            return AsPrintable(buffer, 0, buffer.Length);
        }

        public static string AsPrintable(byte[] buffer, int offset, int count)
        {
            StringBuilder sb = new StringBuilder();
            for (int j = 0; j < count; j++)
            {
                if (offset + j < buffer.Length)
                {
                    char ch = (char)buffer[offset + j];
                    if (Char.IsLetterOrDigit(ch) || Char.IsPunctuation(ch) || Char.IsSeparator(ch) ||
                        Char.IsSymbol(ch))
                    {
                        sb.Append(ch);
                    }
                    else { sb.Append('.'); }
                }
                else { sb.Append(' '); }
            }
            return sb.ToString();
        }

        public static void ShowDiffs(string prefix, byte[] first, byte[] second)
        {
            if (first.Length != second.Length)
            {
                Console.WriteLine(prefix + ": Messages lengths differ! ({0} vs {1})", first.Length, second.Length);
            }
            List<int> positions = new List<int>();
            for (int i = 0; i < Math.Min(first.Length, second.Length); i++)
            {
                if (first[i] != second[i])
                {
                    positions.Add(i);
                }
            }
            if (positions.Count == 0) { return; }
            Console.Write(prefix + ": Messages differ @ ");
            for (int i = 0; i < positions.Count; i++)
            {
                int start = positions[i];
                int end = positions[i];
                // skip over sequences
                while (i + 1 < positions.Count && positions[i] + 1 == positions[i + 1]) { end = positions[i++]; }
                if (start != end) { Console.Write("{0}-{1} ", start, end); }
                else { Console.Write("{0} ", start); }
            }
            Console.WriteLine();
            Console.WriteLine(" First array ({0} bytes):", first.Length);
            HexDump(first);
            Console.WriteLine(" Second array ({0} bytes)", second.Length);
            HexDump(second);
        }

        public static void HexDump(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i += 16)
            {
                Console.WriteLine("{0}/{1}: {2}  {3}",
                    i.ToString("D4"),   // decimal
                    i.ToString("X3"),   // hexadecimal
                    ByteUtils.DumpBytes(bytes, i, 16),
                    ByteUtils.AsPrintable(bytes, i, 16));
            }
        }

        #endregion

        public static bool Compare(byte[] b1, byte[] b2)
        {
            if (b1.Length != b2.Length) { return false; }
            return Compare(b1, 0, b2, 0, b1.Length);
        }

        public static bool Compare(byte[] b1, int b1start, byte[] b2, int b2start, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (b1[b1start + i] != b2[b2start + i])
                {
                    return false;
                }
            }
            return true;
        }

        public static void Write(byte[] buffer, Stream output)
        {
            output.Write(buffer, 0, buffer.Length);
        }

        public static byte[] Read(Stream input, int length)
        {
            byte[] bytes = new byte[length];
            int rc = input.Read(bytes, 0, length);
            if (rc != length) { Array.Resize(ref bytes, rc); }
            return bytes;
        }

        #region Special Number Marshalling Operations

        /// <summary>
        /// Encode a length on the stream in such a way to minimize the number of bytes required.
        /// Top two bits are used to record the number of bytes necessary for encoding the length.
        /// Assumes the length is &lt; 2^30 elements.  Lengths &lt; 64 elelements will fit in a single byte.
        /// </summary>
        /// <param name="length">the length to be encoded</param>
        /// <param name="output">where the encoded length should be placed.</param>
        public static void EncodeLength(int length, Stream output)
        {
            // assumptions: a byte is 8 bites.  seems safe :)
            if (length < 0) { throw new NotSupportedException("lengths must be positive"); }
            if (length < (1 << 6))  // 2^6 = 64
            {
                output.WriteByte((byte)length);
            }
            else if (length < (1 << (6 + 8)))  // 2^(6+8) = 16384
            {
                output.WriteByte((byte)(64 | ((length >> 8) & 63)));
                output.WriteByte((byte)(length & 255));
            }
            else if (length < (1 << (6 + 8 + 8)))   // 2^(6+8+8) = 4194304
            {
                output.WriteByte((byte)(128 | ((length >> 16) & 63)));
                output.WriteByte((byte)((length >> 8) & 255));
                output.WriteByte((byte)(length & 255));
            }
            else if (length < (1 << (6 + 8 + 8 + 8)))    // 2^(6+8+8+8) = 1073741824
            {
                output.WriteByte((byte)(192 | ((length >> 24) & 63)));
                output.WriteByte((byte)((length >> 16) & 255));
                output.WriteByte((byte)((length >> 8) & 255));
                output.WriteByte((byte)(length & 255));
            }
            else
            {
                throw new NotSupportedException("cannot encode lengths >= 2^30");
            }
        }

        /// <summary>
        /// Decode a length from the stream as encoded by EncodeLength() above.
        /// Top two bits are used to record the number of bytes necessary for encoding the length.
        /// </summary>
        /// <param name="input">stream containing the encoded length</param>
        /// <returns>the decoded length</returns>
        public static int DecodeLength(Stream input)
        {
            int b = input.ReadByte();
            int result = b & 63;
            int numBytes = b >> 6;
            if (numBytes >= 1)
            {
                if ((b = input.ReadByte()) < 0) { throw new InvalidDataException("EOF"); }
                result = (result << 8) | b;
            }
            if (numBytes >= 2)
            {
                if ((b = input.ReadByte()) < 0) { throw new InvalidDataException("EOF"); }
                result = (result << 8) | b;
            }
            if (numBytes >= 3)
            {
                if ((b = input.ReadByte()) < 0) { throw new InvalidDataException("EOF"); }
                result = (result << 8) | b;
            }
            if (numBytes > 3) { throw new InvalidDataException("encoding cannot have more than 3 bytes!"); }
            return result;
        }

        #endregion

        #region String-String Dictionary Encoding and Decoding

        /// <summary>
        /// A simple string-string dictionary that is simply encoded as a stream of bytes.
        /// This uses an encoding *similar* to Bencoding (<see>http://en.wikipedia.org/wiki/Bencoding</see>).
        /// Encoding of numbers is done with <c>ByteUtils.EncodeLength</c> and <c>ByteUtils.DecodeLength</c>.
        /// First is the number of key-value pairs.  Followed are the list of the n key-value pairs.  
        /// Each string is prefixed by its encoded length (in bytes as encoded in UTF-8) and then 
        /// the UTF-8 encoded string.
        /// </summary>
        public static int EncodedDictionaryByteCount(Dictionary<string, string> dict)
        {
            int count = 0;
            MemoryStream ms = new MemoryStream();

            EncodeLength(dict.Count, ms);
            foreach (string key in dict.Keys)
            {
                int nBytes = UTF8Encoding.UTF8.GetByteCount(key);
                EncodeLength(nBytes, ms);
                count += nBytes;

                nBytes = UTF8Encoding.UTF8.GetByteCount(dict[key]);
                EncodeLength(nBytes, ms);
                count += nBytes;
            }
            return (int)ms.Length + count;
        }

        public static void EncodeDictionary(Dictionary<string, string> dict, Stream output)
        {
            EncodeLength(dict.Count, output);
            foreach (string key in dict.Keys)
            {
                byte[] bytes = UTF8Encoding.UTF8.GetBytes(key);
                EncodeLength(bytes.Length, output);
                output.Write(bytes, 0, bytes.Length);

                bytes = UTF8Encoding.UTF8.GetBytes(dict[key]);
                EncodeLength(bytes.Length, output);
                output.Write(bytes, 0, bytes.Length);
            }
        }

        public static Dictionary<string, string> DecodeDictionary(Stream input)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            int nKeys = DecodeLength(input);
            for (int i = 0; i < nKeys; i++)
            {
                int nBytes = DecodeLength(input);
                byte[] bytes = new byte[nBytes];
                input.Read(bytes, 0, nBytes);
                string key = UTF8Encoding.UTF8.GetString(bytes);

                nBytes = DecodeLength(input);
                bytes = new byte[nBytes];
                input.Read(bytes, 0, nBytes);
                string value = UTF8Encoding.UTF8.GetString(bytes);

                dict[key] = value;
            }
            return dict;
        }
        #endregion


    }
}
