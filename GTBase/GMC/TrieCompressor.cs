using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using GT.Utils;
using System.IO;

namespace GT.GMC
{
    /// <summary>
    /// The dictionary compressor is unable to capture the range required to
    /// encode the trie references.
    /// </summary>
    public class ShortcutsExhaustedException : GTException { }

    public class TrieCompressor
    {
        protected readonly byte[] template;
        protected short templateId;

        #region Variables for trie-based compression

        protected readonly SuffixTrie encodingTrie;
        protected readonly IList<byte[]> decodingTable;

        #endregion

        #region Variables for integer to byte encoding

        public delegate void DictionaryShortcutChange(short templateId, uint longForm, byte shortcut);
        public event DictionaryShortcutChange DictionaryShortcutChanged;

        /// <summary>
        /// Indication of a single unencoded byte that should
        /// be included verbatim.  The byte should follow this indicator.
        /// </summary>
        private const byte EscapeSingleCharacter = Byte.MinValue;

        /// <summary>
        /// Indication of a sequence of unencoded bytes that should
        /// be included verbatim.  Following this indicator is an encoded
        /// positive integer indicating the length of the sequence.
        /// </summary>
        private const byte EscapeMultipleCharacterSequence = Byte.MinValue + 1;

        // The following commented code was from an experiment to allow slip-streaming 
        // of announcements into the stream...
        ///// <summary>
        ///// Indication of a dictionary addition. Following this indicator is an encoded
        ///// positive integer (the long form) and a byte (the shortform).
        ///// </summary>
        //private const byte EscapeDictionaryAnnouncement = Byte.MinValue + 2;
        //public const byte MinimumShortcut = Byte.MinValue + 3;

        public const byte MinimumShortcut = Byte.MinValue + 2;
        public const byte MaximumShortcut = Byte.MaxValue;

        /// <summary>
        /// Record how many shortcuts have been announced.
        /// </summary>
        private uint announcements = 0;
        public uint Announcements { get { return announcements; } }

        private uint[] inverseShortcuts = new uint[256];
        private Dictionary<uint, byte> shortcuts = new Dictionary<uint, byte>();
        private uint[] shortcutUsage = new uint[256];

        /// <summary>
        /// The last allocated shortcut.  Shortcuts start at 3: 0 is used to indicate a 
        /// single-character escape, 1 for a many-character escape.
        /// </summary>
        private byte lastAllocatedShortcut = MinimumShortcut - 1; // is incremented before first use; see FindOrAddShortcut

        #endregion


        public TrieCompressor(short tid, byte[] tmplt)
        {
            templateId = tid;
            template = tmplt;
            encodingTrie = new SuffixTrie();
            encodingTrie.Add(tmplt);
            decodingTable = encodingTrie.GenerateEncodingTable();
        }

        public byte[] Template { get { return template; } }

        /// <summary>
        /// Encode a message (a set of bytes) to a shorter representation by replacing sequences
        /// of bytes found in this instance's template by an index into the template-trie.
        /// These indices are remapped to a byte shortcut.
        /// </summary>
        /// <param name="input">the message to encode</param>
        /// <returns>the byte-encoded variant</returns>
        public byte[] Encode(byte[] message)
        {
            try
            {
                return TryEncode(message);
            }
            catch (ShortcutsExhaustedException e)
            {
                // Try resetting all shortcuts once: if the shortcuts are exhausted again,
                // then we'll need to rewrite this code to slip-stream new definitions into
                // the byte stream.  We started on this functionality previously (see the
                // reference to EscapeDictionaryAnnoncement above and the commented out
                // code in Decode and WriteShortcut), but it was difficult to handle cases across
                // unreliable connections where the announcements should be repeated until
                // confirmed as being received.
                lastAllocatedShortcut = MinimumShortcut - 1;    // incremented before first use
                shortcuts = new Dictionary<uint,byte>();
                inverseShortcuts = new uint[256];
                shortcutUsage = new uint[256];
                return TryEncode(message);
            }
        }

        protected byte[] TryEncode(byte[] message)
        {
            MemoryStream result = new MemoryStream();
            int endIndex = 0;

            while (endIndex < message.Length)
            {
                int escapedIndex = -1;
                int startIndex = endIndex;

                /// After GetCode(), endIndex should point to the *next* character to consider.
                /// So message[startIndex..endIndex-1] will have been encoded.
                /// If there are characters that must be escaped, then escapedIndex > endIndex
                /// and message[endIndex..escapedIndex-1] should be escaped
                uint code = encodingTrie.GetCode(message, ref endIndex, out escapedIndex);
                if (startIndex < endIndex)
                {
                    WriteShortcut(code, result);
                }
                if (endIndex < escapedIndex)
                {
                    if (escapedIndex - endIndex == 1)
                    {
                        result.WriteByte(EscapeSingleCharacter);
                        result.WriteByte(message[endIndex++]);
                    }
                    else
                    {
                        result.WriteByte(EscapeMultipleCharacterSequence);
                        ByteUtils.EncodeLength(escapedIndex - endIndex, result);
                        result.Write(message, endIndex, escapedIndex - endIndex);
                    }
                    endIndex = escapedIndex;
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// Decode a trie-based encoded message.
        /// </summary>
        /// <param name="encoded">the encoded bytes</param>
        /// <returns>the decoded bytes</returns>
        public byte[] Decode(byte[] encoded)
        {
            MemoryStream input = new MemoryStream(encoded);
            MemoryStream result = new MemoryStream(encoded.Length);
            IList<byte> missingAnnouncements = new List<byte>();
            int b;
            while ((b = input.ReadByte()) >= 0)
            {
                switch(b) {
                case EscapeSingleCharacter:
                    result.WriteByte((byte)input.ReadByte());
                    break;

                case EscapeMultipleCharacterSequence:
                    int length = ByteUtils.DecodeLength(input);
                    while (length-- > 0 && input.Position < input.Length)
                    {
                        result.WriteByte((byte)input.ReadByte());
                    }
                    break;

                //case EscapeDictionaryAnnouncement:
                //    byte shortForm = (byte)input.ReadByte();
                //    int longForm = ByteUtils.DecodeLength(input);
                //    HandleAnnouncement(longForm, shortForm);

                //    if (inverseShortcuts[shortForm] != 0)
                //    {
                //        byte[] decoded = decodingTable[inverseShortcuts[shortForm]];
                //        result.Write(decoded, 0, decoded.Length);
                //    }
                //    else
                //    {
                //        missingAnnouncements.Add((byte)shortForm);
                //    }
                //    break;

                default:
                    if(inverseShortcuts[b] != 0)
                    {
                        byte[] decoded = decodingTable[(int)inverseShortcuts[b]];
                        result.Write(decoded, 0, decoded.Length);
                    }
                    else
                    {
                        missingAnnouncements.Add((byte)b);
                    }
                    break;
                }
            }
            if (missingAnnouncements.Count > 0)
            {
                MissingInformationException mcce = new MissingInformationException();
                mcce.Template = templateId;
                //mcce.UserID = userId;
                mcce.IDs = missingAnnouncements;
                mcce.ExceptionType = EnumExceptionType.MissingAnnouncement;
                throw mcce;
            }
            return result.ToArray();
        }



        /// <summary>
        /// Get the asoociated byte shortcut for an integer (used to map a trie code to a byte)
        /// </summary>
        /// <param name="longForm">The full name of the trie</param>
        /// <param name="cmp">The cmp in which to put any new announcements</param>
        /// <returns></returns>
        public byte WriteShortcut(uint longForm, Stream output)
        {
            byte shortForm;

            if (shortcuts.TryGetValue(longForm, out shortForm))
            {
                output.WriteByte(shortForm);
                shortcutUsage[shortForm]++;
                return shortForm;
            }

            // longForm does not yet have a shortcut
            if (lastAllocatedShortcut == MaximumShortcut) // Byte.MaxValue)
            {
                // We've run out of available shortcuts: throw the ShortcutsExhaustedException
                // and restart.  See the comments in Encode() for details.
                throw new ShortcutsExhaustedException();
            }
            shortForm = (byte)(++lastAllocatedShortcut);
            shortcuts.Add(longForm, shortForm);
            inverseShortcuts[shortForm] = longForm;
            shortcutUsage[shortForm] = 1;   // overrides old usage in case this has changed

            //Console.WriteLine("[tid={0}] Adding dictionary entry: {1} <--> {2}", templateId, longForm, shortForm);
            announcements++;
            if (DictionaryShortcutChanged != null)
            {
                DictionaryShortcutChanged(templateId, longForm, shortForm);
            }
            output.WriteByte(shortForm);
            /////// Encode the new dictionary lookup on the stream
            ////output.WriteByte(EscapeDictionaryAnnouncement);
            ////output.WriteByte(shortForm);
            ////ByteUtils.EncodeLength(longForm, output);
            ////// Note: an addition implicitly means the shortform was written too :-)

            return shortForm;
        }

        /// <summary>
        /// Receive a dictionary update announcement
        /// </summary>
        /// <param name="longForm"></param>
        /// <param name="shortForm"></param>
        internal void HandleAnnouncement(uint longForm, byte shortForm)
        {
            //Console.WriteLine("[tid={0}] Received dictionary entry: {1} <--> {2}", templateId, 
            //    longForm, shortForm);
            inverseShortcuts[shortForm] = longForm;
            shortcuts[longForm] = shortForm;
        }



        internal void CheckSameState(TrieCompressor other)
        {
            InvalidStateException.Assert(decodingTable.Count == other.decodingTable.Count,
                "decoding tables have different lengths");
            for (int i = 0; i < decodingTable.Count; i++)
            {
                InvalidStateException.Assert(ByteUtils.Compare(decodingTable[i], other.decodingTable[i]),
                    "different decoding value for " + i);
            }

            InvalidStateException.Assert(shortcuts.Count == other.shortcuts.Count,
                "shortcut dictionaries have different counts");
            InvalidStateException.Assert(inverseShortcuts.Length == other.inverseShortcuts.Length,
                "inverse shortcut lengths have different counts");
            InvalidStateException.Assert(inverseShortcuts[0] == 0 && inverseShortcuts[1] == 0,
                "bytes 0 and 1 are reserved!");
            for (int i = MinimumShortcut; i <= MaximumShortcut; i++)
            {
                InvalidStateException.Assert(shortcuts.ContainsValue((byte)i) == other.shortcuts.ContainsValue((byte)i),
                    "difference in byte mapping");
                InvalidStateException.Assert(inverseShortcuts[i] == other.inverseShortcuts[i],
                    "difference in byte mapping");
                if (shortcuts.ContainsValue((byte)i))
                {
                    InvalidStateException.Assert(shortcuts[inverseShortcuts[i]] == i,
                        "Mismatch between instance's shortcuts and inverseShortcuts!");
                    InvalidStateException.Assert(inverseShortcuts[i] == other.inverseShortcuts[i],
                        "Mismatch between instances' inverseShortcuts!");
                    InvalidStateException.Assert(shortcuts[inverseShortcuts[i]] == other.shortcuts[other.inverseShortcuts[i]],
                        "Mismatch between instances' inverseShortcuts!");
                }
            }
        }

        /// <summary>
        /// Get an estimate of the size of the message as encoded with this template trie compressor.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public int EncodedSize(byte[] message)
        {
            int size = 0;
            int endIndex = 0;
            while (endIndex < message.Length)
            {
                int escapedIndex = -1;
                int startIndex = endIndex;

                /// After GetCode(), endIndex should point to the *next* character to consider.
                /// So message[startIndex..endIndex-1] will have been encoded.
                /// If there are characters that must be escaped, then escapedIndex > endIndex
                /// and message[endIndex..escapedIndex-1] should be escaped
                uint code = encodingTrie.GetCode(message, ref endIndex, out escapedIndex);
                if (startIndex < endIndex)
                {
                    size++; // 1 byte shortcut for the code
                }
                if (endIndex < escapedIndex)
                {
                    if (escapedIndex - endIndex == 1)
                    {
                        size += 1 + 1;  // 1 byte for the special marker, 1 byte for the character
                    }
                    else
                    {
                        size += 1 + 1 + (escapedIndex - endIndex);  // 1 byte for marker, 1 for len + n bytes
                    }
                    endIndex = escapedIndex;
                }
            }
            return size;
        }
    }
}
