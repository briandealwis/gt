using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;
using System.IO;
using GT.Utils;
using System.IO.Compression;
using System.Diagnostics;
using GT.Net;

namespace GT.GMC
{

    /// <summary>
    /// A facade for performing compression and decompression.
    /// GMC has two parts: a template compressor that finds repeated structure and
    /// syntax elements between messages, and a within-message compressor that uses
    /// Ziv-Lempel to reduce the size of large messages.  The template compressor
    /// examines messages to find repeated sequences, which are assigned a code
    /// and entered into a dictionary; these codes are then used to replace other
    /// occurrences of the sequence in future messages.  The template compression
    /// takes three parts: identifying and creating a template, compressing the
    /// message, and announcing any new templates.
    /// </summary>
    public class GMCMarshaller : IMarshaller
    {
        /// <summary>
        /// Special key to indicate content type
        /// </summary>
        private enum GMCWithinMessageCompression
        {
            None = 71,          // ASCII G: no within-message compression
            Deflated = 68,      // ASCII D: deflated
        }

        /// <summary>
        /// Special key to indicate types of content value
        /// </summary>
        private enum GMCMessageKey
        {
            Template = 0,
            HuffmanFrequencyTable = 1,
            Announcements = 2,
            Huffed = 3,
            NotHuffed = 4,
            Message = 5
        }

        /// <summary>
        /// GMC operates on the results from a different marshaller.
        /// This sub-marshaller is used to transform objects, etc. to bytes.
        /// </summary>
        private IMarshaller subMarshaller;

        /// <summary>
        /// The compressor used for messages being sent by this user.
        /// </summary>
        private GeneralMessageCompressor compressor;

        /// <summary>
        /// The per-user decompressors used to decompress messages from other users
        /// (as keyed by a unique identifier).  Decompressors require being notified
        /// of the templates and announcements (new dictionary entries) from the other
        /// users' systems.
        /// </summary>
        private Dictionary<int, GeneralMessageCompressor> decompressors; // uniqueId -> GeneralMessageCompressor

        /// Statistics
        public long totalUncompressedBytes = 0;
        public long totalCompressedBytes = 0;
        public long totalTemplateBytes = 0;
        public long totalAnnouncementBytes = 0;
        public long totalFrequenciesBytes = 0;
        public long numberDeflatedMessages = 0;
        public long totalDeflatedBytesSaved = 0;

        /// <summary>Creates a new instance of GMC.  
        /// Typically GMC will function as a singleton for each client, 
        /// however, there are cases where the designer may wish to have a variety of GMCS.</summary>
        /// <param name="v">the submarshaller to use for marshalling objects to bytes.</param>
        public GMCMarshaller(IMarshaller subMrshlr)
        {
            subMarshaller = subMrshlr;
            compressor = new GeneralMessageCompressor();
            decompressors = new Dictionary<int, GeneralMessageCompressor>();
        }

        public string[] Descriptors
        {
            get
            {
                List<string> descriptors = new List<string>();
                foreach (string subdescriptor in subMarshaller.Descriptors)
                {
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < subdescriptor.Length; i++)
                    {
                        sb.Append(Char.ConvertFromUtf32(Char.ConvertToUtf32(subdescriptor, i) ^ 71));
                    }
                    descriptors.Add(sb.ToString());
                }
                return descriptors.ToArray();
            }
        }

        public void Marshal(int uniqueId, Message message, Stream output, ITransport t)
        {
            MemoryStream bytes = new MemoryStream();
            subMarshaller.Marshal(uniqueId, message, bytes, t);
            byte[] encoded = Encode(bytes.ToArray());
            ByteUtils.Write(BitConverter.GetBytes(uniqueId), output);
            ByteUtils.EncodeLength(encoded.Length, output);
            ByteUtils.Write(encoded, output);
        }

        public Message Unmarshal(Stream input, ITransport t)
        {
            int encoderId = BitConverter.ToInt32(ByteUtils.Read(input, 4), 0);
            int length = ByteUtils.DecodeLength(input);
            byte[] decoded = Decode(encoderId, ByteUtils.Read(input, length));
            return subMarshaller.Unmarshal(new MemoryStream(decoded), t);
        }


        public void Dispose()
        {
            subMarshaller.Dispose();
        }

        /// <summary>
        /// Removes the decompressors for a certain userID
        /// </summary>
        /// <param name="userID">The userID to remove</param>
        public void RemoveUserID(int userID)
        {
            decompressors.Remove(userID);
        }

        /// <summary>
        /// Encode (compress) the provided byte array.
        /// </summary>
        /// <param name="bytes">the bytes to be compressed</param>
        /// <returns>a compressed message package that holds the compressed message along
        /// with any updates required for its processing.</returns>
        public byte[] Encode(byte[] bytes)
        {
            DebugUtils.WriteLine("\n>>>> ENCODING <<<<<");
            totalUncompressedBytes += bytes.Length;

            CompressedMessagePackage cmp = compressor.Encode(bytes);
            MemoryStream result = new MemoryStream();
            DebugUtils.WriteLine("==> Encoding for template {0}", cmp.TemplateId);
            // note: must remember to replace/strip-off this first byte in TryDeflating() 
            result.WriteByte((byte)GMCWithinMessageCompression.None);
            ByteUtils.EncodeLength(cmp.TemplateId, result);
            if (cmp.Template != null)
            {
                DebugUtils.WriteLine("TID={0}: Encoding template: {1}", cmp.TemplateId, ByteUtils.DumpBytes(cmp.Template));
                result.WriteByte((byte)GMCMessageKey.Template);
                EncodeTemplate(cmp.Template, result);
            }
            result.WriteByte((byte)(cmp.Huffed ? GMCMessageKey.Huffed : GMCMessageKey.NotHuffed));
            if (cmp.FrequencyTable != null)
            {
                DebugUtils.WriteLine("TID={0}: Encoding frequency table", cmp.TemplateId);
                result.WriteByte((byte)GMCMessageKey.HuffmanFrequencyTable);
                EncodeFrequencyTable(cmp.FrequencyTable, result);
            }
            if (cmp.Announcements != null && cmp.Announcements.Count > 0)
            {
                DebugUtils.Write("TID={0}: Encoding {1} announcements:", cmp.TemplateId, cmp.Announcements.Count);
                foreach (KeyValuePair<uint, byte> kvp in cmp.Announcements)
                {
                    DebugUtils.Write(" {0}->{1}", kvp.Key, kvp.Value);
                }
                DebugUtils.WriteLine("");
                result.WriteByte((byte)GMCMessageKey.Announcements);
                EncodeAnnouncements(cmp.Announcements, result);
            }
            if (cmp.Message != null)    // this could be useful one day...
            {
                DebugUtils.WriteLine("Encoding message: {0}", ByteUtils.DumpBytes(cmp.Message));
                result.WriteByte((byte)GMCMessageKey.Message);
                ByteUtils.EncodeLength(cmp.Message.Length, result);
                result.Write(cmp.Message, 0, cmp.Message.Length);
            }

            bytes = TryDeflating(result.ToArray());
            totalCompressedBytes += bytes.Length;
            return bytes;
        }

        private byte[] TryDeflating(byte[] bytes)
        {
            // FIXME: deflating isn't working yet
            return bytes;   
            //MemoryStream output = new MemoryStream();
            //output.WriteByte((byte)GMCWithinMessageCompression.Deflated);
            //DeflateStream deflated = new DeflateStream(output, CompressionMode.Compress);
            //try
            //{
            //    // must strip off the first byte, which indicates no deflation
            //    Debug.Assert(bytes[0] == (byte)GMCWithinMessageCompression.None);
            //    deflated.Write(bytes, 1, bytes.Length - 1); deflated.Flush();
            //    if (output.Length < bytes.Length)
            //    {
            //        /* DebugUtils.WriteLine("GMC","Deflating message: originally {0} bytes, deflated {1} bytes ({2}%)",
            //            bytes.Length, deflated.Length, (float)deflated.Length / (float)bytes.Length); */
            //        DebugUtils.WriteLine("Deflating message: originally " + bytes.Length +
            //            " bytes, deflated " + output.Length + " bytes (" +
            //            ((float)output.Length / (float)bytes.Length) + ")");
            //        
            //numberDeflatedMessages++;
            //totalDeflatedBytesSaved += bytes.Length - output.Length;
            //        return output.ToArray();
            //    }
            //    return bytes;
            //}
            //finally { deflated.Close(); }
        }

        private void EncodeTemplate(byte[] tmpl, Stream output)
        {
            long p = output.Position;

            ByteUtils.EncodeLength(tmpl.Length, output);
            output.Write(tmpl, 0, tmpl.Length);

            totalTemplateBytes += output.Position - p;
        }

        private void EncodeAnnouncements(Dictionary<uint, byte> dictionary, Stream output)
        {
            long p = output.Position;
            
            ByteUtils.EncodeLength(dictionary.Count, output);
            byte[] result = new byte[4];
            foreach (uint longForm in dictionary.Keys)
            {
                BitConverter.GetBytes(longForm).CopyTo(result, 0);
                output.Write(result, 0, 4);
                output.WriteByte(dictionary[longForm]);
            }

            totalAnnouncementBytes += output.Position - p;
        }

        private void EncodeFrequencyTable(uint[] frequencies, Stream output)
        {
            long p = output.Position;

            Debug.Assert(frequencies.Length == 256);
            for (int j = 0; j < 256; j++)
            {
                output.WriteByte((byte)((frequencies[j] >> 24) & 0xFF));
                output.WriteByte((byte)((frequencies[j] >> 16) & 0xFF));
                output.WriteByte((byte)((frequencies[j] >> 8) & 0xFF));
                output.WriteByte((byte)((frequencies[j]) & 0xFF));
            }

            totalFrequenciesBytes += output.Position - p;
        }

        /// <summary>
        /// Decode the provided byte array received from the user with unique
        /// identity <c>userId</c>.
        /// </summary>
        /// <param name="userId">the unique identity for the user</param>
        /// <param name="encodedBytes">the encoded message received</param>
        /// <returns></returns>
        public byte[] Decode(int userId, byte[] encodedBytes)
        {
            DebugUtils.WriteLine("\n>>>> DECODING <<<<<");
            Stream input = new MemoryStream(encodedBytes);
            if (input.ReadByte() == (byte)GMCWithinMessageCompression.Deflated)
            {
                input = new DeflateStream(input, CompressionMode.Decompress);
            }
            CompressedMessagePackage cmp = new CompressedMessagePackage();
            cmp.TemplateId = (short)ByteUtils.DecodeLength(input);
            DebugUtils.WriteLine("==> Decoding with template {0}", cmp.TemplateId);
            int inputValue;
            while((inputValue = input.ReadByte()) != -1) {
                switch ((GMCMessageKey)inputValue)
                {
                case GMCMessageKey.Huffed: cmp.Huffed = true; break;
                case GMCMessageKey.NotHuffed: cmp.Huffed = false; break;
                case GMCMessageKey.Template:
                    cmp.Template = DecodeTemplate(input);
                    DebugUtils.WriteLine("TID={0}: Decoded template: {1}", cmp.TemplateId, ByteUtils.DumpBytes(cmp.Template));
                    break;
                case GMCMessageKey.HuffmanFrequencyTable:
                    cmp.FrequencyTable = DecodeFrequencies(input);
                    DebugUtils.WriteLine("TID={0}: Decoded huffman frequencies", cmp.TemplateId);
                    break;
                case GMCMessageKey.Announcements:
                    cmp.Announcements = DecodeAnnouncements(input);
                    DebugUtils.Write("TID={0}: Decoded {1} dictionary announcements:", cmp.TemplateId, cmp.Announcements.Count);
                    foreach (KeyValuePair<uint, byte> kvp in cmp.Announcements)
                    {
                        DebugUtils.Write(" {0}->{1}", kvp.Key, kvp.Value);
                    }
                    DebugUtils.WriteLine("");
                    break;
                case GMCMessageKey.Message:
                    int len = ByteUtils.DecodeLength(input);
                    cmp.Message = new byte[len];
                    input.Read(cmp.Message, 0, len);
                    DebugUtils.WriteLine("Decoded message: {0}", ByteUtils.DumpBytes(cmp.Message));
                    break;
                default:
                    throw new MarshallingException("invalid GMC message");
                }
            }

            GeneralMessageCompressor handler;
            if (!decompressors.TryGetValue(userId, out handler))
            {
                if (cmp.Template == null)
                {
                    //we haven't received any templates whatsoever from this person.
                    MissingInformationException mte = new MissingInformationException();
                    mte.Template = 0;
                    mte.UserID = userId;
                    mte.ExceptionType = EnumExceptionType.MissingTemplate;
                    throw mte;
                }
                handler = decompressors[userId] = new GeneralMessageCompressor();
            }
            return handler.Decode(cmp, userId);            
        }

        private Dictionary<uint, byte> DecodeAnnouncements(Stream input)
        {
            int count = ByteUtils.DecodeLength(input);
            byte[] buffer = new byte[4];
            Dictionary<uint, byte> result = new Dictionary<uint,byte>();
            for (int i = 0; i < count; i++)
            {
                input.Read(buffer, 0, 4);
                result[BitConverter.ToUInt32(buffer, 0)] = (byte)input.ReadByte();
            }
            return result;
        }

        private byte[] DecodeTemplate(Stream input)
        {
            int length = ByteUtils.DecodeLength(input);
            byte[] tmpl = new byte[length];
            input.Read(tmpl, 0, length);
            return tmpl;
        }

        private uint[] DecodeFrequencies(Stream input)
        {
            uint[] result = new uint[256];
            for (int i = 0; i < 256; i++)
            {
                result[i] = (uint)input.ReadByte() << 24;
                result[i] |= (uint)input.ReadByte() << 16;
                result[i] |= (uint)input.ReadByte() << 8;
                result[i] |= (uint)input.ReadByte();
            }
            return result;
        }
    }
}
