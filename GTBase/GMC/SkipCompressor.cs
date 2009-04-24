namespace GT.GMC
{

    /// FIXME: This code doesn't seem to be working, not was it actually being used
    /// (fortunately) given the length test in AddPattern().
    /// FIXME: The SkipTrie was no different from our SuffixTrie.  Fixup the
    /// following to use the SuffixTrie.
    //public class SkipCompressor
    //{
    //    public int bytesSaved;
    //    SkipTrie trie;
    //    DictionaryCompressor fc;
    //    //	LinkedList<ArrayHolder> potentials;
    //    Dictionary<ArrayHolder, ArrayHolder> potentials;
    //    Dictionary<Byte, ArrayHolder> table;
    //    bool active;
    //    int listSize = 30;

    //    /// MIN_VALUE and MAX_VALUE determine the possible number of shortcuts
    //    /// that can be managed by this instance.
    //    static byte MIN_VALUE = Byte.MinValue + 3;
    //    static byte MAX_VALUE = Byte.MinValue + 63;
    //    static int SIZE = MAX_VALUE - MIN_VALUE;
    //    int counter;

    //    public SkipCompressor(DictionaryCompressor fast)
    //    {
    //        counter = 0;
    //        bytesSaved = 0;
    //        trie = new SkipTrie();
    //        fc = fast;
    //        active = false;
    //        table = new Dictionary<Byte, ArrayHolder>(100);
    //        potentials = new Dictionary<ArrayHolder, ArrayHolder>();
    //        //potentials = new LinkedList<ArrayHolder>();
    //    }

    //    public void AddPattern(byte[] pattern)
    //    {
    //        if (pattern.Length > 15 && pattern.Length < 30)
    //        {
    //            active = true;
    //            trie.CreateCorpus(pattern);
    //        }
    //    }

    //    private byte AllocateHashIndex()
    //    {
    //        if (counter > SIZE)
    //        {
    //            // Discard least used entry that is not in use
    //            int usage = Int32.MaxValue;
    //            byte leastUsed = 0;
    //            bool found = false;
    //            foreach (byte b in table.Keys)
    //            {
    //                ArrayHolder ah = table[b];
    //                if (usage > ah.usage && !ah.inUse /*&& NOT IN USE!!! */)
    //                {
    //                    leastUsed = b;
    //                    usage = ah.usage;
    //                    found = true;
    //                }
    //            }
    //            if (!found)
    //            {
    //                // FIXME: There is nothing that is not currently in use.
    //                // Presumably this is a bad thing. We should do something more useful.
    //                Console.WriteLine("AAAA");
    //                foreach (ArrayHolder ah in table.Values)
    //                {
    //                    Console.WriteLine(ah.inUse);
    //                    ah.inUse = false;
    //                }
    //                return 0;
    //            }
    //            else
    //            {
    //                table.Remove(leastUsed);
    //                return leastUsed;
    //            }
    //        }
    //        else
    //        {
    //            byte result = (byte)(MIN_VALUE + counter);
    //            counter++;
    //            return result;
    //        }
    //    }

    //    private bool potentialsHas(ArrayHolder bholder)
    //    {
    //        if (potentials.Count == 0) return false;
    //        return potentials.ContainsKey(bholder);
    //        //		for(ArrayHolder ah : potentials ){
    //        //			if(ah.equals(bholder)) return true;
    //        //		}
    //        //		return false;
    //    }

    //    private ArrayHolder getHolder(ArrayHolder bholder)
    //    {
    //        return potentials[bholder];
    //        //		for(ArrayHolder bb : potentials){
    //        //			if(bb.equals(bholder)) return bb;
    //        //		}
    //        //		return null;
    //    }

    //    private bool tableHas(byte[] b)
    //    {
    //        if (table.Count == 0) { return false; }
    //        return table.ContainsValue(new ArrayHolder(b));
    //    }

    //    private Byte getPos(byte[] b)
    //    {
    //        ArrayHolder bholder = new ArrayHolder(b);
    //        foreach (byte bb in table.Keys)
    //        {
    //            ArrayHolder ah = table[bb];
    //            if (ah.Equals(bholder)) return bb;
    //        }
    //        return 0;
    //    }

    //    public byte[] Compress(byte[] pattern)
    //    {
    //        if (!active) { return pattern; }
    //        byte[] remainder = pattern;
    //        MemoryStream compressed = new MemoryStream();
    //        //System.out.println("!!");
    //        while (remainder.Length > 0)
    //        {
    //            SkipResult sr = trie.FindCode(remainder);
    //            if (sr.isEscape || sr.removed.Length < 2)
    //            {
    //                compressed.Write(sr.removed, 0, sr.removed.Length);
    //            }
    //            else if (tableHas(sr.removed))
    //            {
    //                byte b = getPos(sr.removed);
    //                ArrayHolder ah = table[b];
    //                //System.out.println("HERE");
    //                bytesSaved += sr.removed.Length - 1;
    //                ah.inUse = true;
    //                ah.usage++;
    //                compressed.WriteByte(b);
    //                fc.NoteUsage(b);
    //            }
    //            else
    //            {
    //                ArrayHolder holder = new ArrayHolder(sr.removed);
    //                if (potentialsHas(holder))
    //                {
    //                    ArrayHolder junk = getHolder(holder);
    //                    if (junk.usage > 5)
    //                    {
    //                        //System.out.println("YAY");
    //                        //byte b = fc.addSkipAnnounce(sr.removed);
    //                        byte b = AllocateHashIndex();
    //                        //if (AnnounceNewSkip != null)
    //                        //{
    //                        //    AnnounceNewSkip(templateId, b, sr.removed);
    //                        //}
    //                        /*								System.out.println("BYTE VALUE    " + b);
    //                                                        for(int k = 0; k < sr.removed.Length; k++) System.out.print(sr.removed[k] + " ");*/
    //                        //System.out.print("\n");
    //                        bytesSaved += sr.removed.Length - 1;
    //                        table.Add(b, holder);
    //                        potentials.Remove(holder);
    //                        compressed.WriteByte(b);
    //                    }
    //                    else
    //                    {
    //                        junk.usage = junk.usage + 1;
    //                        for (int i = 0; i < sr.removed.Length; i++)
    //                        {
    //                            compressed.WriteByte(sr.removed[i]);
    //                        }
    //                    }
    //                }
    //                else
    //                {
    //                    AddToList(new ArrayHolder(sr.removed));
    //                    compressed.Write(sr.removed, 0, sr.removed.Length);
    //                }
    //            }
    //            remainder = sr.remainder;

    //        }
    //        //	System.out.println("AAAA");
    //        foreach (ArrayHolder ah in table.Values)
    //        {
    //            //	System.out.println(ah.inUse);
    //            ah.inUse = false;
    //        }

    //        //if(pattern.Length>result.Length)System.out.println("start size " + pattern.Length + " end size" + result.Length);
    //        /*		System.out.print("=-=-=-=-=-=-\n");
    //                for(int k = 0; k < pattern.Length; k++) System.out.print(pattern[k] + " ");
    //                System.out.print("\n");
    //                for(int k = 0; k < result.Length; k++) System.out.print(result[k] + " ");
    //                System.out.print("\n"); */
    //        return compressed.ToArray();
    //    }

    //    private void AddToList(ArrayHolder holder)
    //    {
    //        if (potentials.Count == listSize)
    //        {
    //            ArrayHolder smallest = null;
    //            foreach (ArrayHolder ah in potentials.Keys)
    //            {
    //                if (smallest == null) { smallest = ah; }
    //                else if (ah.usage < smallest.usage) { smallest = ah; }
    //            }
    //            potentials.Remove(smallest);
    //        }
    //        potentials.Add(holder, holder);
    //    }

    //    public class ArrayHolder : IComparable<ArrayHolder>
    //    {
    //        public byte[] values;
    //        internal bool inUse;
    //        internal int usage;
    //        public ArrayHolder(byte[] newvalues)
    //        {
    //            values = newvalues;
    //            usage = 1;
    //            inUse = false;
    //        }

    //        public override bool Equals(Object c)
    //        {
    //            byte[] tester = ((ArrayHolder)c).values;
    //            if (tester.Length != values.Length) { return false; }
    //            for (int i = 0; i < tester.Length; i++)
    //            {
    //                if (tester[i] != values[i]) return false;
    //            }
    //            return true;
    //        }

    //        public override int GetHashCode()
    //        {
    //            int result = 0;
    //            foreach(byte b in values) { result += b; }
    //            return result * values.Length;
    //        }

    //        public int CompareTo(ArrayHolder c)
    //        {
    //            byte[] tester = c.values;
    //            if (tester.Length != values.Length) { return tester.Length - values.Length; }
    //            for (int i = 0; i < tester.Length; i++)
    //            {
    //                if (tester[i] != values[i]) { return tester[i] - values[i]; }
    //            }
    //            return 0;
    //        }
    //    }


    //    // DANE: I just tossed this in here because I was in a hurry, should be part of the decode method of GeneralMessageCompressor
    //    public byte[] expandMessage(byte[] message)
    //    {
    //        //if(!active) return message;
    //        byte[] temp = new byte[message.Length * 5];
    //        byte[] result;
    //        int pos = 0;
    //        for (int i = 0; i < message.Length; i++)
    //        {
    //            if (message[i] == Byte.MinValue)
    //            {
    //                //System.out.println("Not HERE");
    //                temp[pos] = message[i];
    //                pos++;
    //                i++;
    //                temp[pos] = message[i];
    //                pos++;
    //            }
    //            else if (message[i] == Byte.MinValue + 1)
    //            {

    //                //	System.out.println("HERE?");

    //                temp[pos] = message[i];
    //                pos++;
    //                i++;
    //                while (message[i] != Byte.MinValue + 1)
    //                {
    //                    temp[pos] = message[i];
    //                    pos++;
    //                    i++;
    //                }
    //                temp[pos] = message[i];
    //                pos++;
    //            }
    //            else if (message[i] == Byte.MinValue + 2)
    //            {
    //                //	System.out.println("HERE???");
    //                temp[pos] = message[i];
    //                pos++;
    //                i++;
    //                int size = message[i];
    //                temp[pos] = message[i];
    //                pos++;
    //                i++;
    //                for (int j = 0; j < size; j++)
    //                {
    //                    temp[pos] = message[i];
    //                    pos++;
    //                    i++;
    //                }
    //                i--;
    //            }
    //            else if (!table.ContainsKey(message[i]))
    //            {
    //                temp[pos] = message[i];
    //                pos++;
    //            }
    //            else
    //            {
    //                //	System.out.println("HERE");
    //                byte[] value = table[message[i]].values;
    //                for (int j = 0; j < value.Length; j++)
    //                {
    //                    temp[pos] = value[j];
    //                    pos++;
    //                }
    //            }
    //        }

    //        result = new byte[pos];
    //        Array.Copy(temp, 0, result, 0, pos);

    //        /*	
    //            for(int k = 0; k < message.Length; k++) System.out.print(message[k] + " ");
    //            System.out.print("\n");
    //            for(int k = 0; k < result.Length; k++) System.out.print(result[k] + " ");
    //            System.out.print("\n");
    //            System.out.print("=-=-=-=-=-=-\n"); */

    //        return result;
    //    }

    //}
}
