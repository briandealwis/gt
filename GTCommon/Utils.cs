using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace GT.Net
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
        protected List<T> list;
        public SequentialListProcessor(List<T> list)
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
        protected List<T> list;
        protected T current = null;
        protected Predicate<T> predicate;

        public PredicateListProcessor(List<T> list, Predicate<T> predicate)
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
        public static string DumpBytes(byte[] buffer, int offset, int count)
        {
            StringBuilder sb = new StringBuilder();
            for (int j = 0; j < count; j++)
            {
                if ((int)buffer[offset + j] < 10) { sb.Append('0'); }
                sb.Append(Convert.ToString((int)buffer[offset + j], 16));
                if (j != count - 1) { sb.Append(' '); }
            }
            return sb.ToString();
        }

        public static string AsPrintable(byte[] buffer, int offset, int count)
        {
            StringBuilder sb = new StringBuilder();
            for (int j = 0; j < count; j++)
            {
                char ch = (char)buffer[offset + j];
                if (Char.IsLetterOrDigit(ch) || Char.IsPunctuation(ch) || Char.IsSeparator(ch) ||
                    Char.IsSymbol(ch))
                {
                    sb.Append(ch);
                }
                else { sb.Append('.'); }
            }
            return sb.ToString();
        }
        #endregion

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


        #region Special Marshalling Operators

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
            if(length < 0) { throw new NotSupportedException("lengths must be positive"); }
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
            if (numBytes >= 1) {
                if ((b = input.ReadByte()) < 0) { throw new InvalidDataException("EOF"); }
                result = (result << 8) | b;
            }
            if (numBytes >= 2) {
                if ((b = input.ReadByte()) < 0) { throw new InvalidDataException("EOF"); }
                result = (result << 8) | b;
            }
            if (numBytes >= 3) {
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
        public static int EncodedDictionaryByteCount(Dictionary<string, string> dict) {
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

        public static void EncodeDictionary(Dictionary<string, string> dict, Stream output) {
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

        public static Dictionary<string, string> DecodeDictionary(Stream input) {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            int nKeys = DecodeLength(input);
            for(int i = 0; i < nKeys; i++)
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


        public static int EncodedLengthByteCount(long p)
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }

}
