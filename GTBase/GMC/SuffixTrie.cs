using System;
using System.Collections.Generic;

namespace GT.GMC
{
    /// <summary>
    /// A suffix trie represents the suffixes of a given string (say <c>s</c>) in 
    /// a way allowing fast determination whether another string <c>w</c> is a
    /// substring of <c>s</c> in O(|<c>w</c>|).
    /// 
    /// Definition (from Gusfield, below): A suffix tree <c>T</c> for an <c>m</c> character 
    /// string <c>s</c> is a rooted tree with exactly <c>m</c> leaves numbers 1 through <c>m</c>.  
    /// Each internal node, other than the root, has at least two children and each edge is 
    /// labelled with a nonempty substring of <c>s</c>.  No two edges out of a node can have
    /// edge-labels beginning with the same character.  The key feature of the suffix tree
    /// is that for any leaf <c>i</c>, the concatenation of the edge-labels on the path
    /// from the root to leaf <c>i</c> spells out the suffix of <c>s</c> that starts at
    /// position <c>i</c>.  That is, it spels out <c>s[i..m]</c>
    /// 
    /// Further details: D Gusfield (1997). Algorithms on Strings, Trees and Sequences. 
    /// CUP. ISBN 0-521-58519-8 (QA76.9.A43G87)
    /// </summary>
    public class SuffixTrie
    {
        private TrieNode root;
        private uint lastCode;

        /// <summary>
        /// Creates a new suffix trie.
        /// </summary>
        public SuffixTrie()
        {
            // The root represents a special terminator character
            root = new TrieNode(0);
            lastCode = 0;
        }

        /// <summary>
        /// Adds a string to the dictionary that the templates uses.
        /// </summary>
        /// <param name="key">the string to be added</param>
        public void Add(byte[] key)
        {
            //add all suffixes to the trie
            for(int start = 0; start < key.Length; start++) {
                root.Update(key, start, ref lastCode);
            }
        }

        /// <summary>
        /// Search for and return an encoding for the maximal byte sequence found in
        /// <see cref="key"/> starting at position <see cref="startIndex"/>.  At the 
        /// conclusion of this method, the encoding for the maximal byte sequence will 
        /// be returned. <see cref="escapedIndex"/> will point to the start of bytes
        /// that do not have an encoding.  <see cref="startIndex"/> will have been 
        /// advanced to the next character after the maximal seqence and after any
        /// bytes needing to be escaped; if <see cref="startIndex"/> == <see cref="escapedIndex"/>
        /// then no bytes needed escaping.
        /// </summary>
        /// <param name="key">the bytes</param>
        /// <param name="startIndex">starting point in <see cref="key"/> for encoding</param>
        /// <param name="escapedIndex">pointer to sequence of bytes that cannot be encoded</param>
        /// <returns>the encoding</returns>
        public uint GetCode(byte[] key, ref int startIndex, out int escapedIndex)
        {
            uint code = root.GetCode(key, ref startIndex);
            escapedIndex = startIndex;
            root.SkipEscapedValues(key, ref escapedIndex);
            return code;
        }

        /// <summary>
        /// Returns a list used for decoding keys in the tables
        /// </summary>
        /// <returns></returns>
        public IList<byte[]> GenerateEncodingTable()
        {
            IList<byte[]> table = new List<byte[]>((int)lastCode + 1);
            byte[] empty = { };
            for (int i = 0; i < lastCode + 1; i++)
            {
                table.Add(empty);
            }
            root.BuildTable(table);
            return table;
        }
    }

    /// <summary>
    /// The result of looking up a key in a suffix trie representing a particular message.
    /// Code is the index of where the key begins in the represented message.
    /// If the key is not a substring of the represented message, then the unmatched suffix of 
    /// the key that was not matched will be placed in remainder.
    /// </summary>
    public class TrieNode
    {
        protected Dictionary<byte, TrieNode> children;
        protected uint content;

        public TrieNode(uint codeValue)
        {
            children = new Dictionary<byte, TrieNode>();
            content = codeValue;
        }

        /// <summary>
        /// Recursively updates the trie, given the current size (not counting the root) 
        /// of the trie.  To avoid generating unnecessary garbage, we pass a start index
        /// into key.
        /// </summary>
        /// <param name="key">the key to lookup/update, relative to <see cref="startIndex"/></param>
        /// <param name="startIndex">start location into <see cref="key"/></param>
        /// <param name="nextCode">the next code to be assigned</param>
        public void Update(byte[] key, int startIndex, ref uint nextCode)
        {
            // string is already known or a substring of a known string
            if (startIndex >= key.Length) { return; } 

            byte navigateChar = key[startIndex];
            TrieNode nextNode;

            // if the byte is not already known
            if (!children.TryGetValue(navigateChar, out nextNode))
            {
                nextNode = new TrieNode(++nextCode);
                children.Add(navigateChar, nextNode);
            }
            nextNode.Update(key, startIndex + 1, ref nextCode);
        }

        /// <summary>Recursively search for a code.</summary>
        public uint GetCode(byte[] key)
        {
            int index = 0;
            return GetCode(key, ref index);
        }

        /// <summary>
        /// Recursively search for a code. After GetCode(), startIndex will 
        /// point to the *next* character to consider. So 
        /// message[startIndex .. startIndex'-1] will be represented by the return code.
        /// </summary>
        public uint GetCode(byte[] key, ref int startIndex)
        {
            if (startIndex >= key.Length) { return content; }

            byte navigateChar = key[startIndex];
            TrieNode nextNode;
            if (children.TryGetValue(navigateChar, out nextNode))
            {
                ++startIndex;
                return nextNode.GetCode(key, ref startIndex);
            }
            return content;
        }

        public void SkipEscapedValues(byte[] key, ref int escapedIndex)
        {
            while (escapedIndex < key.Length && !children.ContainsKey(key[escapedIndex]))
            {
                escapedIndex++;
            }
        }

        /// <summary>
        /// Traverse entire tree, building a table to map code indices to key values
        /// </summary>
        /// <param name="table"></param>
        public void BuildTable(IList<byte[]> table)
        {
            BuildTable(table, new Stack<byte>(8));
        }

        private void BuildTable(IList<byte[]> table, Stack<byte> currentKey)
        {
            if (content != 0) {
                // Stack.ToArray() builds from top to bottom, so we need to reverse it
                byte[] key = currentKey.ToArray();  
                Array.Reverse(key);
                table[(int)content] = key;
            }
            foreach (byte ch in children.Keys)
            {
                TrieNode nextNode = children[ch];
                currentKey.Push(ch);
                nextNode.BuildTable(table, currentKey);
                currentKey.Pop();
            }
        }
    }

}
