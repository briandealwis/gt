using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace GT.Utils
{
    #region Queue Processing
    /// <summary>
    /// A simple interface, similar to <c>IEnumerator</c> for processing elements sequentially
    /// but with the ability to remove the current element.
    /// </summary>
    /// <typeparam name="T">the type of the elements</typeparam>
    public interface IProcessingQueue<T>
    {
        /// <summary>
        /// Return the current element.
        /// </summary>
        /// <returns>the current element to be considered or null if there are no more
        /// elements remaining (i.e., Empty == true).</returns>
        T Current { get; }

        /// <summary>
        /// Remove the current element and advance to the next element.
        /// </summary>
        void Remove();

        /// <summary>
        /// Return true if there are no more elements remaining.
        /// </summary>
        bool Empty { get; }
    }

    /// <summary>
    /// A processing queue on all the elements of a list.
    /// </summary>
    /// <typeparam name="T">the type of elements</typeparam>
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


    /// <summary>
    /// A processing queue for a list of objects meeting the requirements defined
    /// by a predicate.
    /// </summary>
    /// <typeparam name="T">the type of the elements</typeparam>
    public class PredicateListProcessor<T> : IProcessingQueue<T>
        where T: class
    {
        protected IList<T> list;
        protected int index = 0;
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
                while(index < list.Count) {
                    if(predicate(list[index])) { return list[index]; }
                    index++;
                }
                return null;
            }
        }

        public void Remove()
        {
            list.RemoveAt(index);
        }

        public bool Empty
        {
            get { return index >= list.Count; }
        }
    }

    /// <summary>
    /// A chained set of processing queues.
    /// </summary>
    /// <typeparam name="T">the type of the elements</typeparam>
    public class ProcessorChain<T> : IProcessingQueue<T>
    {
        protected IList<IProcessingQueue<T>> chain;

        /// <summary>
        /// Create a new instance.  The provided chain will be modified.
        /// </summary>
        /// <param name="chain"></param>
        public ProcessorChain(IList<IProcessingQueue<T>> chain) {
            this.chain = chain;
        }

        public ProcessorChain(params IProcessingQueue<T>[] queues)
        {
            chain = new List<IProcessingQueue<T>>(queues.Length);
            foreach(IProcessingQueue<T> q in queues) { chain.Add(q); }
        }

        public T Current
        {
            get { 
                while(chain.Count > 0) {
                    if(chain[0].Empty) { chain.RemoveAt(0); continue; }
                    return chain[0].Current;
                }
                return default(T);
            }
        }

        public void Remove()
        {
            chain[0].Remove();
            while(chain.Count > 0 && chain[0].Empty) { chain.RemoveAt(0); }
        }

        public bool Empty
        {
            get { 
                while(chain.Count > 0 && chain[0].Empty) { chain.RemoveAt(0); }
                return chain.Count == 0;
            }
        }
    }

    /// <summary>
    /// A processing queue for a single element.
    /// </summary>
    /// <typeparam name="T">the type of the elements</typeparam>
    public class SingleElementProcessor<T> : IProcessingQueue<T> 
        where T: class
    {
        protected T element;

        public SingleElementProcessor(T elmt)
        {
            if (elmt == null) { throw new ArgumentNullException("elmt"); }
            element = elmt;
        }

        public T Current
        {
            get { return element; }
        }

        public void Remove()
        {
            element = null;
        }

        public bool Empty
        {
            get { return element == null; }
        }
    }

    /// <summary>
    /// A processor for a series of queues; the queues are processed in round-robin.
    /// As a queue is depleted, it is removed from the provided set of queues.
    /// </summary>
    public class RoundRobinProcessor<K,V> : IProcessingQueue<V>
        where V: class
    {
        // We ensure that emptied queues are removed in Remove()
        protected IDictionary<K, Queue<V>> queues;
        protected List<K> keys;
        protected int index = 0;

        public RoundRobinProcessor(IDictionary<K, Queue<V>> qs)
        {
            queues = qs;
            keys = new List<K>(qs.Count);
            foreach (K id in queues.Keys)
            {
                if (queues[id].Count == 0) { queues.Remove(id); }
                else { keys.Add(id); }
            }
        }

        public V Current
        {
            get
            {
                if (queues.Count == 0) { return null; }
                return queues[keys[index]].Peek();
            }
        }

        public void Remove()
        {
            if (queues.Count == 0) { return; }
            queues[keys[index]].Dequeue();
            if (queues[keys[index]].Count == 0)
            {
                queues.Remove(keys[index]);
                keys.RemoveAt(index);
            }
            else { index++; }   // move onto the next queue
            if (index >= keys.Count) { index = 0; }
        }

        public bool Empty
        {
            get { return queues.Count == 0; }
        }
    }



    #endregion

    #region Byte-related Utilities
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
            Console.WriteLine(HexDump(first));
            Console.WriteLine(" Second array ({0} bytes)", second.Length);
            Console.WriteLine(HexDump(second));
        }

        public static string HexDump(byte[] bytes)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i += 16)
            {
                sb.Append(i.ToString("D4"));   // decimal
                sb.Append('/');
                sb.Append(i.ToString("X3"));   // hexadecimal
                sb.Append(": ");
                sb.Append(DumpBytes(bytes, i, 16));
                sb.Append("  ");
                sb.Append(AsPrintable(bytes, i, 16));
                sb.Append('\n');
            }
            return sb.ToString();
        }

        #endregion

        #region Byte Array Comparisons
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
        #endregion

        #region Stream Utilities
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
        #endregion

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
        public static int EncodedDictionaryByteCount(IDictionary<string, string> dict)
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

        public static void EncodeDictionary(IDictionary<string, string> dict, Stream output)
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

    #endregion
}
