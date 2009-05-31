//
// GT: The Groupware Toolkit for C#
// Copyright (C) 2006 - 2009 by the University of Saskatchewan
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later
// version.
// 
// This library is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA
// 02110-1301  USA
// 

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

// A set of useful utility classes for building applications.
namespace GT.Utils
{
    // These exist in .NET 3.0 apparently///
    public delegate void Action<T1,T2>(T1 arg1, T2 arg2);
    public delegate void Action<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3);

    #region Byte-related Utilities

    /// <summary>
    /// Set of useful functions for byte arrays including:
    /// <ul>
    /// <li> dumping human-readable representations; </li>
    /// <li> finding differences between byte-arrays; </li>
    /// <li> reading and writing to streams; </li>
    /// <li> reading and writing compact representations of
    ///      integers and dictionaries. </li>
    /// </ul>
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

        public static byte[] Read(Stream input, uint length)
        {
            byte[] bytes = new byte[length];
            int rc = input.Read(bytes, 0, (int)length);
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
        public static void EncodeLength(uint length, Stream output)
        {
            // assumptions: a byte is 8 bites.  seems safe :)
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
        /// Encode a length as a byte array.
        /// Top two bits are used to record the number of bytes necessary for encoding the length.
        /// Assumes the length is &lt; 2^30 elements.  Lengths &lt; 64 elelements will fit in a single byte.
        /// </summary>
        /// <param name="length">the length to be encoded</param>
        public static byte[] EncodeLength(uint length)
        {
            // assumptions: a byte is 8 bites.  seems safe :)
            if (length < 0) { throw new NotSupportedException("lengths must be positive"); }
            if (length < (1 << 6))  // 2^6 = 64
            {
                return new[] { (byte)length };
            }
            else if (length < (1 << (6 + 8)))  // 2^(6+8) = 16384
            {
                return new[] { (byte)(64 | ((length >> 8) & 63)),
                    (byte)(length & 255) };
            }
            else if (length < (1 << (6 + 8 + 8)))   // 2^(6+8+8) = 4194304
            {
                return new[] { (byte)(128 | ((length >> 16) & 63)),
                    (byte)((length >> 8) & 255),
                    (byte)(length & 255) };
            }
            else if (length < (1 << (6 + 8 + 8 + 8)))    // 2^(6+8+8+8) = 1073741824
            {
                return new[] { (byte)(192 | ((length >> 24) & 63)),
                    (byte)((length >> 16) & 255),
                    (byte)((length >> 8) & 255),
                    (byte)(length & 255) };
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
        public static uint DecodeLength(Stream input)
        {
            int b = input.ReadByte();
            uint result = (uint)(b & 63);
            int numBytes = b >> 6;
            if (numBytes >= 1)
            {
                if ((b = input.ReadByte()) < 0) { throw new InvalidDataException("EOF"); }
                result = (result << 8) | (uint)b;
            }
            if (numBytes >= 2)
            {
                if ((b = input.ReadByte()) < 0) { throw new InvalidDataException("EOF"); }
                result = (result << 8) | (uint)b;
            }
            if (numBytes >= 3)
            {
                if ((b = input.ReadByte()) < 0) { throw new InvalidDataException("EOF"); }
                result = (result << 8) | (uint)b;
            }
            if (numBytes > 3) { throw new InvalidDataException("encoding cannot have more than 3 bytes!"); }
            return result;
        }

        /// <summary>
        /// Decode a length from the stream as encoded by EncodeLength() above.
        /// Top two bits are used to record the number of bytes necessary for encoding the length.
        /// </summary>
        /// <param name="bytes">byte content containing the encoded length</param>
        /// <param name="index">in: the index in which to decode the byte length, out: set to
        /// the index of the first byte following the encoded length</param>
        /// <returns>the decoded length</returns>
        public static uint DecodeLength(byte[] bytes, ref int index)
        {
            int numBytes = bytes[index] >> 6;
            uint result = (uint)bytes[index++] & 63;
            if (numBytes >= 1)
            {
                result = (result << 8) | bytes[index++];
            }
            if (numBytes >= 2)
            {
                result = (result << 8) | bytes[index++];
            }
            if (numBytes >= 3)
            {
                result = (result << 8) | bytes[index++];
            }
            if (numBytes > 3) { throw new InvalidDataException("encoding cannot have more than 3 bytes!"); }
            return result;
        }

                /// <summary>
        /// Decode a length from the stream as encoded by EncodeLength() above.
        /// Top two bits are used to record the number of bytes necessary for encoding the length.
        /// </summary>
        /// <param name="bytes">byte content containing the encoded length</param>
        /// <returns>the decoded length</returns>
        public static uint DecodeLength(byte[] bytes)
        {
            int index = 0;
            return DecodeLength(bytes, ref index);
            // if(index != bytes.Length) { throw new InvalidSomethingOrAnother(); }
        }

        #endregion

        #region String-String Dictionary Encoding and Decoding

        // A simple string-string dictionary that is simply encoded as a stream of bytes.
        // This uses an encoding *similar* to bencoding (http://en.wikipedia.org/wiki/Bencoding).
        // Encoding of numbers is done with <see cref="EncodeLength"/> and <see cref="DecodeLength"/>.
        // First is the number of key-value pairs.  Followed are the list of the n key-value pairs.  
        // Each string is prefixed by its encoded length (in bytes as encoded in UTF-8) and then 
        // the UTF-8 encoded string.
        
        /// <summary>
        /// Figure out how many bytes would be necessary to encode the provided dictionary
        /// using our bencoding-like format.
        /// </summary>
        public static uint EncodedDictionaryByteCount(IDictionary<string, string> dict)
        {
            uint count = 0;
            MemoryStream ms = new MemoryStream();

            EncodeLength((uint)dict.Count, ms);
            foreach (string key in dict.Keys)
            {
                uint nBytes = (uint)Encoding.UTF8.GetByteCount(key);
                EncodeLength(nBytes, ms);
                count += nBytes;

                nBytes = (uint)Encoding.UTF8.GetByteCount(dict[key]);
                EncodeLength(nBytes, ms);
                count += nBytes;
            }
            return (uint)(ms.Length + count);
        }

        /// <summary>
        /// Encode a strings dictionary onto a stream as using our bencoding-like format.
        /// </summary>
        /// <param name="dict">the dictionary to encode</param>
        /// <param name="output">the stream from which to decode</param>
        /// <returns>the decoded dictionary</returns>
        public static void EncodeDictionary(IDictionary<string, string> dict, Stream output)
        {
            EncodeLength((uint)dict.Count, output);
            foreach (string key in dict.Keys)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(key);
                EncodeLength((uint)bytes.Length, output);
                output.Write(bytes, 0, bytes.Length);

                bytes = Encoding.UTF8.GetBytes(dict[key]);
                EncodeLength((uint)bytes.Length, output);
                output.Write(bytes, 0, bytes.Length);
            }
        }

        /// <summary>
        /// Decode a strings dictionary from a stream encoded in our bencoding-like format.
        /// </summary>
        /// <param name="input">the stream from which to decode</param>
        /// <returns>the decoded dictionary</returns>
        public static Dictionary<string, string> DecodeDictionary(Stream input)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            uint nKeys = DecodeLength(input);
            for (int i = 0; i < nKeys; i++)
            {
                uint nBytes = DecodeLength(input);
                byte[] bytes = new byte[nBytes];
                input.Read(bytes, 0, (int)nBytes);
                string key = Encoding.UTF8.GetString(bytes);

                nBytes = DecodeLength(input);
                bytes = new byte[nBytes];
                input.Read(bytes, 0, (int)nBytes);
                string value = Encoding.UTF8.GetString(bytes);

                dict[key] = value;
            }
            return dict;
        }

        #endregion

    }

    #endregion
}
