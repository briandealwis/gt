using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;
using GT.Utils;
using GT.Net;

namespace GT.GMC
{



    /// <summary>A compression exception</summary>
    public enum EnumExceptionType
    {
        /// <summary>A template is missing</summary>
        MissingTemplate,
        /// <summary>An announcement is missing</summary>
        MissingAnnouncement,
        /// <summary>A frequency table is missing</summary>
        MissingFrequencyTable
    }

    /// <summary>More information is required to decompress this data.</summary>
    public class MissingInformationException : System.ApplicationException
    {
        /// <summary>The information type that is missing</summary>
        public EnumExceptionType ExceptionType;

        /// <summary>The template identifier that is missing</summary>
        public int Template;

        /// <summary>The identity of the user who has the missing information</summary>
        public int UserID;

        /// <summary>A list of missing announcements that are required</summary>
        public IList<byte> IDs = new List<byte>();

        /// <summary>Creates a new, blank, MissingInformationException.</summary>
        public MissingInformationException() { }

        /// <summary>Creates a new, blank, MissingInformationException.</summary>
        public MissingInformationException(string message) : base(message) { }

        /// <summary>Creates a new, blank, MissingInformationException.</summary>
        public MissingInformationException(string message, System.Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>Constructor needed for serialization 
        /// when exception propagates from a remoting server to the client.</summary>
        protected MissingInformationException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) { }
    }

    /// <summary>
    /// An object passed back after compression.
    /// Contains any templates, announcements, frequency tables, or other information
    /// needed to decompress data from this particular compressor.
    /// </summary>
    public class CompressedMessagePackage
    {
        public byte[] Message;              // the (possibly encoded) message
        public bool Gmced;                  // true if Message has been encoded with GMC
        public short TemplateId;            // the template with which Message was encoded
        public bool Huffed;                 // true if Message has been huffman-encoded
        public byte[] Template = null;      // not null if this message was encoded with a new template
        public uint[] FrequencyTable = null; // the updated byte frequency counts
        public Dictionary<uint, byte> Announcements;    // dictionary replacements

        public int EstimatedMessageLength()
        {
            int length = 2;     // templateId
            if (Template != null) { length += Template.Length; }
            if (FrequencyTable != null) { length += FrequencyTable.Length * 4; }
            if (Announcements != null) { length += Announcements.Count * 5; }
            length += Message.Length;
            return length;
        }
    }

    /// <summary>
    /// Implements the general message compressor as described by 
    /// C Gutwin, C Fedak, M Watson, J Dyck, T Bell (2006).  Improving network efficiency 
    /// in real-time groupware with general message compression.  In Proc of the Conf on Computer
    /// Supported Cooperative Work (CSCW), 119--128.  doi:10.1145/1180875.1180894.
    /// &lt;http://hci.usask.ca/publications/2006/compression.pdf&gt;
    /// </summary>
    public class GeneralMessageCompressor
    {
        private short lastTemplateId;   // last allocated template identifier
        private int maximumTemplates;   // max number of templates
        private List<TemplateBasedCompressor> compressors;
        private int messagesSaved;
        private double targetRatio;
        private LinkedList<byte[]> savedMessages;
        private bool useHuff;
        private TimeSpan timeThreshold;

        private CompressedMessagePackage cmp;

        public GeneralMessageCompressor()
        {
            maximumTemplates = 30;
            lastTemplateId = -1;    // incremented before being used; so first template will be #0

            compressors = new List<TemplateBasedCompressor>();
            targetRatio = 0.35;
            messagesSaved = 10;
            savedMessages = new LinkedList<byte[]>();
            useHuff = true;
            timeThreshold = TimeSpan.FromMilliseconds(2);
        }

        /// <summary>
        /// if we know a template that we would like to construct, we can give it to the Handler, 
        /// and it will add it.  
        /// </summary>
        /// <param name="template"></param>
        /// <returns></returns>
        public short ConstructTemplate(byte[] template)
        {
            // for the duration of this method, lastTemplateId is the current templateId.
            short templateId;
            lock (this)
            {
                templateId = lastTemplateId = (short)((lastTemplateId + 1) % maximumTemplates);
            }

            Debug.Assert(templateId <= compressors.Count);

            TemplateBasedCompressor ct = new TemplateBasedCompressor(templateId, template, useHuff);
            if (templateId == compressors.Count) { compressors.Add(ct); }
            else { compressors[templateId] = ct; }
            return templateId;
        }

        /// <summary>
        /// Adds a template to a specified location.  Replaces an existing template.
        /// </summary>
        /// <param name="template">the template value</param>
        /// <param name="templateId">the template identifier</param>
        private void AddTemplate(byte[] template, short templateId)
        {
            Debug.Assert(templateId <= compressors.Count);
            TemplateBasedCompressor ct = new TemplateBasedCompressor(templateId, template, useHuff);
            if (templateId < compressors.Count)
            {
                /*if this template is the same from before, we don't want to lose all the announcements
                 * we know from before.  If we already have this exact template, it could be that someone
                 * else requested it and it was broadcast to everyone.*/
                if (ct.Equals(compressors[templateId])) { return; }
                /*else we should get rid of the old and bring in the new*/
                compressors[templateId] = ct;
                return;
            }
            //we haven't seen this before, add it; throws an exception if templateId > compressors.Count
            compressors.Insert(templateId, ct);   
        }


        /// <summary>
        /// Set whether huffman encoding should be used.  Enabled by default.
        /// </summary>
        /// <param name="setting">true if data should be huffman-encoded, false otherwise.</param>
        public bool HuffmanEncoding
        {
            get { return useHuff; }
            set { useHuff = value; }
        }

        /// <summary>
        /// Set the number of messages to be saved for comparison when generating new templates.
        /// A new template is created when the last N messages are not well-compressed.
        /// </summary>
        /// <param name="number"></param>
        public void SetMessagesSaved(int number)
        {
            messagesSaved = number;
        }

        /// <summary>
        /// Provide opportunity for any maintenance.
        /// </summary>
        private void CheckTemplates()
        {
            foreach (TemplateBasedCompressor ct in compressors) { ct.Check(); }
        }

        /// <summary>
        /// Compresses the message as best it can.   If the compression is above the set ratio, then 
        /// we label it as a candidate template and test to see if would make a good template.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public CompressedMessagePackage Encode(byte[] message)
        {
            int time = System.Environment.TickCount;    // milliseconds

            CheckTemplates();
            CompressedMessagePackage cmp = null;

            if (compressors.Count == 0)
            {
                // If we have no templates, then we construct a template from this
                // message and then encode this message with itself.
                short templateId = ConstructTemplate(message);
                cmp = EncodeWith(templateId, message);
                /// FIXME: GMC currently assumes that messages are sent reliably.
                /// This should be changed -- not clear how yet though.
                compressors[templateId].UpdatesAccepted();
                return cmp;
            }

            // Try encoding this message with all current templates and select the best
            cmp = FindBestEncoding(message);

            float compression = (float)cmp.EstimatedMessageLength() / (float)message.Length;
            if (compression > targetRatio)
            { 
                /// we need to run the template generator now as it is awful.
                // also, run the huffman encoding, for good measure.
                // ct.GetFastCompressor().generateTree();
                savedMessages.AddLast(message);
                if (savedMessages.Count > messagesSaved) { savedMessages.RemoveFirst(); }

                if (ShouldGenerateNewTemplate(message))
                {
                    short templateId = ConstructTemplate(message);
                    cmp = EncodeWith(templateId, message);
                }
            }

            time = System.Environment.TickCount - time;
            if (time > timeThreshold.TotalMilliseconds) {
                DebugUtils.WriteLine("GMC: encoding took {0}ms > desired time of {1}ms", time, timeThreshold.TotalMilliseconds);
                targetRatio = Math.Max(0.9, targetRatio + .05); 
            }
            else if (time < timeThreshold.TotalMilliseconds) {
                DebugUtils.WriteLine("GMC: encoding took {0}ms < desired time of {1}ms", time, timeThreshold.TotalMilliseconds);
                targetRatio = Math.Max(0.2, targetRatio - .005);
            }

            /// FIXME: GMC currently assumes that messages are sent reliably.
            /// This should be changed -- not clear how yet though.
            /// Perhaps it should be transactional?  I.e., Commit()/Abort()?
            if (cmp.Gmced)
            {
                foreach (TemplateBasedCompressor ct in compressors)
                {
                    if (ct.TemplateId == cmp.TemplateId)
                    {
                        ct.UpdatesAccepted();
                    }
                    else
                    {
                        ct.UpdatesRejected();
                    }
                }
            }

            return cmp;
        }

        /// <summary>
        /// Compresses a message using a specific template.
        /// </summary>
        /// <param name="templateId">the template to be used</param>
        /// <param name="message">the message to encode</param>
        /// <param name="cmp">the place for additional results</param>
        /// <returns></returns>
        public CompressedMessagePackage EncodeWith(int templateId, byte[] message)
        {
            return compressors[templateId].Encode(message);
        }

        /// <summary>
        /// Decode the provided message using the specified template
        /// </summary>
        /// <param name="templateName">the template identifier</param>
        /// <param name="message">the encoded (compressed) message</param>
        /// <param name="userId">the user from which the encoded message was received</param>
        /// <returns>The decoded (uncompressed) message</returns>
        public byte[] Decode(CompressedMessagePackage cmp, int userId)
        {
            if (cmp.Template != null)
            {
                AddTemplate(cmp.Template, cmp.TemplateId);
            }
            if (cmp.TemplateId < 0 || cmp.TemplateId > compressors.Count)
            {
                MissingInformationException mte = new MissingInformationException();
                mte.Template = cmp.TemplateId;
                mte.UserID = userId;
                mte.ExceptionType = EnumExceptionType.MissingTemplate;
                throw mte;
            }
            
            return compressors[cmp.TemplateId].Decode(cmp);
        }


        /// <summary>
        /// Find the best compression for a message using existing messages
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private CompressedMessagePackage FindBestEncoding(byte[] message)
        {
            CompressedMessagePackage best = null;
            int bestSize = Int32.MaxValue;
            //boolean compressed = false
            for (short templateId = 0; templateId < compressors.Count; templateId++)
            {
                if (compressors[templateId] == null) { continue; }
                CompressedMessagePackage encoding = EncodeWith(templateId, message);

                if (encoding.Message.Length <= bestSize)
                {
                    best = encoding;
                    bestSize = best.Message.Length;
                }
            }
            return best;
        }

        // FIXME: we preserve the behaviour of the original code by just using the
        // trie-encoding result; but perhaps we should just use the result of all
        // stages given that the cost of evaluating the all stages is so cheap?
        private int FindBestTrieEncoding(byte[] message)
        {
            int bestSize = Int32.MaxValue;
            for (short templateId = 0; templateId < compressors.Count; templateId++)
            {
                if (compressors[templateId] == null) { continue; }
                int encodedLength = compressors[templateId].TrieCompressor.EncodedSize(message);
                if (encodedLength < bestSize) { bestSize = encodedLength; }
            }
            return bestSize;
        }

        /// <summary>
        /// Check if the provided candidate would make a good template
        /// FIXME: Why are we throwing the template rep away?
        /// </summary>
        /// <param name="templateCandidate">the template</param>
        /// <returns></returns>
        public bool ShouldGenerateNewTemplate(byte[] templateCandidate)
        {
            TrieCompressor tc = new TrieCompressor(-1, templateCandidate);
            int candidateSize = 0;
            int currentSize = 0;
            foreach(byte[] message in savedMessages) {
                candidateSize += tc.EncodedSize(message);
                // This assumes the trie encoding approximates the overall encoding...
                currentSize += FindBestTrieEncoding(message);
            }
            return candidateSize < currentSize;
        }

        #region Debugging / Informational commands

        /** returns total templates */
        public int TotalTemplates { get { return compressors.Count; } }

        /// <summary>
        /// Number of announcements that have been made
        /// </summary>
        public uint TotalAnnouncements
        {
            get
            {
                uint announcements = 0;
                foreach (TemplateBasedCompressor ct in compressors)
                {
                    announcements += ct.TrieCompressor.Announcements;
                }
                return announcements;
            }
        }

        /** In bytes, the size of the templates */
        public int TotalTemplateSize
        {
            get
            {
                int result = 0;
                foreach (TemplateBasedCompressor ct in compressors)
                {
                    result += ct.Template.Length + 1;
                }
                return result;
            }
        }

        /** Print all the encodings of a Huffed template*/
        //public void DumpEncodings() 
        //{
        //    int i = 0;
        //    foreach (TemplateBasedCompressor ct in compressors)
        //    {
        //        DictionaryCompressor fc = ct.DictionaryCompressor;
        //        if(fc.huffmanEnabled()){
        //            Console.WriteLine("template " + i);
        //            fc.het.PrintTable();
        //        }
        //        i++;
        //    }
        //}

        /** Print the usage of every value of a huffed Template*/
        //public void PrintUsages()
        //{
        //    for(int i=0;i<compressors.Count;i++){
        //        Console.WriteLine("Template Number " + i);
        //        DictionaryCompressor fc = compressors[i].DictionaryCompressor;
        //        for(int j = 0; j < fc.usage.Length; j++){
        //            Console.WriteLine("Table Entry: " + j + " Occurance :" + fc.usage[j]);
        //        }
        //        Console.WriteLine("");
        //    }
        //}

        #endregion
    }
}
