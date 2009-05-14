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
using System.Diagnostics;
using Common.Logging;
using GT.Utils;

namespace GT.GMC
{
    /// <summary>
    /// Represents a sequence of template-based compressors.  Currently a staged pipeline of:
    /// <list>
    /// <item>a trie compressor that replaces sequences in a message with corresponding
    /// subsequence of the template</item>
    /// <item>a dictionary compressor that replaces the template-subsequence indices with
    /// byte-based shortcuts</item>
    /// <item>a Huffman encoder that remaps the byte-based shortcuts</item>
    /// </list>
    /// </summary>
    public class TemplateBasedCompressor
    {
        private ILog log;

        private short templateId;
        private byte[] template;
        private TrieCompressor tc;
        private HuffmanCompressor hc;
        
        public int encodedSavings = 0;

        // private SkipCompressor sc;

        #region Record new announcements and updated frequences
        private Dictionary<uint, byte> dictionaryAdditions = new Dictionary<uint,byte>();
        private uint[] frequencies;
        /// <summary>
        /// Track whether the pending frequency updates in <c>frequencies</c> have been
        /// successfully sent.
        /// </summary>
        private bool frequenciesSent = false;
        private bool templateSent = false;
        #endregion

        /// <summary>
        /// Record how often has this instance been used?
        /// </summary>
        private long uses = 0;
        public long Uses { get { return uses; } }
        
        /// <summary>
        /// Note usage of this template.
        /// </summary>
        public void NoteTemplateUse()
        {
            uses++;
        }


        public byte[] Template { get { return template; } }
        public short TemplateId { get { return templateId; } }

        public TemplateBasedCompressor(short tid, byte[] tmplt, bool huff)
        {
            log = LogManager.GetLogger(GetType());

            templateId = tid;
            template = tmplt;
            tc = new TrieCompressor(templateId, template);
            tc.DictionaryShortcutChanged += HandleNewAnnouncement;
            hc = new HuffmanCompressor(templateId);
            hc.HuffmanFrequenciesChanged += HandleUpdatedFrequencies;
            hc.HuffmanEncoding = huff;
        }

        /// <summary>
        /// Set whether huffman encoding should be used.
        /// </summary>
        /// <param name="setting">true if data should be huffman-encoded, false otherwise.</param>
        public bool HuffmanEncoding
        {
            get { return hc.HuffmanEncoding; }
            set
            {
                hc.HuffmanEncoding = value;
            }
        }


        /// <summary>
        /// Opportunity for maintenance.  If a template has been used enough, turn huffman encoding 
        /// on for it
        /// </summary>
        public void Check()
        {
            if (Uses % 200 == 0) { hc.UpdateHuffmanEncoding(); }
        }

        /// <summary>
        /// All updates (announcements, frequency changes) have been sent and
        /// received by the others.  Nil out all records to begin accumulation
        /// anew.
        /// </summary>
        public void UpdatesAccepted()
        {
            dictionaryAdditions = new Dictionary<uint,byte>();
            if (frequenciesSent)
            {
                frequencies = null;
                frequenciesSent = false;
            }
            templateSent = true;    // should have been sent and received
        }

        /// Any updates (announcements, frequency changes) were not sent and
        /// should be resent on subsequent messages.
        public void UpdatesRejected()
        {
            // continue accumulating dictionary updates and ensure 
            // huffman frequency table is still available to resend later
            frequenciesSent = false;
        }

        public override bool Equals(object obj)
        {
            if (obj is TemplateBasedCompressor)
            {
                return template.Equals(((TemplateBasedCompressor)obj).template);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return template.GetHashCode();
        }

        public TrieCompressor TrieCompressor { get { return tc; } }
        public HuffmanCompressor HuffmanCompressor { get { return hc; } }


        /// <summary>
        /// Record the announcement of a new dictionary entry, to be added to
        /// the next outgoing message.
        /// </summary>
        /// <param name="longForm">The full trie identity</param>
        /// <param name="shortForm">The shortcut identity</param>
        /// <param name="templateId">Who the shortcut will be used with</param>
        public void HandleNewAnnouncement(short templateId, uint longForm, byte shortForm)
        {
            Debug.Assert(templateId == this.templateId);
            //Console.WriteLine("[tid={0}] TBC.HandleNewAnnouncement: {1} <--> {2}", templateId, longForm, shortForm);
            InvalidStateException.Assert(!dictionaryAdditions.ContainsKey(longForm),
                "Announcements have wrapped", this);
            dictionaryAdditions[longForm] = shortForm;
        }

        /// <summary>
        /// Record the announcement of a set of byte frequencies, to be added to
        /// the next outgoing message.
        /// </summary>
        /// <param name="templateId">the associated template</param>
        /// <param name="frequencies">the byte-usage frequencies</param>
        public void HandleUpdatedFrequencies(short templateId, uint[] frequencies)
        {
            Debug.Assert(templateId == this.templateId);
            this.frequencies = frequencies;
            frequenciesSent = false;
        }


        public CompressedMessagePackage Encode(byte[] message)
        {
            NoteTemplateUse();

            CompressedMessagePackage cmp = new CompressedMessagePackage();
            cmp.TemplateId = templateId;

            // Encode with the trie compressor -- compress repeated sequences
            byte[] encoded = tc.Encode(message);

            // FIXME: don't currently understand the skip compressor anyways
            //encoded = ct.SkipCompressor.Compress(encoded);
            //sc.AddPattern(encoded);
            
            // If enabled, and it improves the case, use huffman encoding to replace 
            // frequently-used bytes
            byte[] huffed = hc.Encode(encoded);
            if (huffed != null && huffed.Length < encoded.Length)
            {
                cmp.Huffed = true;
                cmp.Message = huffed;

                // Only send the huffman frequency table when actually necessary!
                // After all, we might have a few updates before it's actually received...
                cmp.FrequencyTable = frequencies;
                frequenciesSent = true; // pending updates are scheduled to be sent
            }
            else
            {
                cmp.Huffed = false;
                cmp.Message = encoded;

                // we must reset this flag. Although we may have been scheduled to send the pending
                // frequency updates on a previous call, the update may not have been sent if
                // another template was selected.
                frequenciesSent = false;
            }

            if (cmp.Message.Length < message.Length)
            {
                encodedSavings += message.Length - cmp.Message.Length;
            }

            cmp.Gmced = true;   // FIXME: could check that encoded.Length < message.Length
            if (!templateSent) { cmp.Template = template; }
            cmp.Announcements = dictionaryAdditions;
            return cmp;
        }

        /// <summary>
        /// Decompresses a message using the provided compressors.
        /// </summary>
        /// <param name="cmp">the details of the message to be decompressed</param>
        /// <returns>The decoded (uncompressed) message</returns>
        public byte[] Decode(CompressedMessagePackage cmp)
        {
            List<byte> missingAnnouncements = new List<byte>();

            if (cmp.FrequencyTable != null)
            {
                Debug.Assert(cmp.FrequencyTable.Length == 256);
                hc.SetFrequencies(cmp.FrequencyTable);
                if(log.IsTraceEnabled)
                {
                    log.Trace(String.Format("[tid={0}] received huffman frequencies {1}", 
                        templateId, cmp.FrequencyTable));
                }
            }
            if (cmp.Announcements != null)
            {
                foreach (uint longForm in cmp.Announcements.Keys)
                {
                    tc.HandleAnnouncement(longForm, cmp.Announcements[longForm]);
                }
            }

            byte[] message = cmp.Message;
            Debug.Assert(message != null);
            if (cmp.Huffed) { message = hc.Decode(message); }
            Debug.Assert(message != null);
            /// if(cmp.Gmced) { 
            message = tc.Decode(message);
            /// }

            return message;
        }

        public override string ToString()
        {
            return GetType().Name + "(tid=" + TemplateId + "; template=" + ByteUtils.DumpBytes(template)
                   + " [" + ByteUtils.AsPrintable(template) + "])";
        }
    }
}
