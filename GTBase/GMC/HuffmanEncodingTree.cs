using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Diagnostics;
using GT.Utils;

namespace GT.GMC
{
    internal class HuffmanEncodingTreeNode : IComparable<HuffmanEncodingTreeNode>
    {
        internal byte value;
        internal long weight;
        internal HuffmanEncodingTreeNode left, right, parent;

        public int CompareTo(HuffmanEncodingTreeNode other)
        {
            return (int)(weight - other.weight);
        }
    }

    /// <summary>
    /// Simple implementation of a huffman encoding tree.  A tree must first be
    /// configured with a byte frequency table before the tree can be used for
    /// encoding and decoding <see cref="HuffmanEncodingTree.GenerateFromFrequencyTable"/>.
    /// </summary>
    public class HuffmanEncodingTree
    {
        private HuffmanEncodingTreeNode root;
        private BitArray[] encodingTable;

        /// <summary>
        /// Create a new, uninitialized huffman encoding tree.  Callers must explicitly
        /// provide a frequency table before performing any encoding / decoding.
        /// </summary>
        public HuffmanEncodingTree()
        {
            encodingTable = new BitArray[256];
        }

        /// <summary>
        /// Create a new huffman encoding tree initialized with the provided
        /// frequency table.  The resulting encoding tree is ready for encoding/decoding.
        /// </summary>
        public HuffmanEncodingTree(uint[] frequencies)
            : this()
        {
            GenerateFromFrequencyTable(frequencies);
        }

        /// <summary>
        /// Encode the provided bytes using this tree.  We refer to the result as
        /// a "huffed byte array".
        /// </summary>
        /// <param name="input">the byte array to be encoded using this tree</param>
        /// <returns>the huffed byte array</returns>
        public byte[] EncodeArray(byte[] input)
        {
            BitTuple bt = EncodeArrayToBitArray(input);
            return bt.ToArray();
        }

        /// <summary>
        /// Turns a byte array into a huffed BitArray
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public BitTuple EncodeArrayToBitArray(byte[] input)
        {
            if (input.Length == 0) { return new BitTuple(0); }

            BitTuple result = new BitTuple(0, input.Length);   // guestimate
            int pos = 0;
            for (int i = 0; i < input.Length; i++)
            {
                BitArray encoded = encodingTable[input[i]];
                result.AddAll(encoded);
                pos += encoded.Length;
            }

            // If the output is short of a full byte, then find an input that is longer 
            // and write out part of it to pad the output to be byte aligned.  When 
            // decoding, this will trace an invalid path and so won't be decoded.
            if (pos % 8 != 0)
            {
                int remainingBits = (8 - (pos % 8));
                int size = pos;
                for (int counter = 0; counter < 256; counter++)
                {
                    if (encodingTable[counter].Length > remainingBits)
                    {
                        for (int i = 0; i < remainingBits; i++)
                        {
                            result.Add(encodingTable[counter].Get(i));
                            pos++;
                        }
                        break;
                    }
                }
            }
            return result;
        }


        /** Decode a Huffed BitArray into a byte array*/
        public byte[] DecodeBitArray(BitTuple bt)
        {
            int size = bt.Length;
            HuffmanEncodingTreeNode currentNode;
            currentNode = root;
            List<byte> bytes = new List<byte>(size / 8); // guesstimate
            // go left if the bit is 0, right if it is 1... until we hit a leaf
            for (int i = 0; i < size; i++)
            {
                if (bt[i]) { currentNode = currentNode.right; }
                else { currentNode = currentNode.left; }
                if (currentNode.left == null && currentNode.right == null)
                {
                    bytes.Add(currentNode.value);
                    currentNode = root;
                }
            }
            byte[] result = new byte[bytes.Count];
            bytes.CopyTo(result, 0);
            return result;
        }


        /** Decodes a huffman encoded byte array into a (hopefully larger) byte array.*/
        public byte[] DecodeArray(byte[] input)
        {
            return DecodeBitArray(new BitTuple(input));
        }


        /** builds the huffman tree frequencies should be 256 values long */
        public void GenerateFromFrequencyTable(uint[] frequencies)
        {
            Debug.Assert(frequencies.Length == 256, "Frequency table must have exactly 256 entries");
            HuffmanEncodingTreeNode[] leafList = new HuffmanEncodingTreeNode[256];
            List<HuffmanEncodingTreeNode> huffmanEncodingTreeNodeList = new List<HuffmanEncodingTreeNode>();

            // set the weight of every node
            // if the frequency was "0" before, we will set it to a minimum of "1"	
            for (int i = 0; i < 256; i++)
            {
                HuffmanEncodingTreeNode node = new HuffmanEncodingTreeNode();
                node.left = null;
                node.right = null;
                node.value = (byte)i;
                node.weight = (long)frequencies[i];
                if (node.weight == 0)
                {
                    node.weight = 1;
                }
                leafList[i] = node;
                huffmanEncodingTreeNodeList.Add(node);
            }
            huffmanEncodingTreeNodeList.Sort();

            // build the huffman tree based in a clever fashion
            // keep mashing the trees together until there are none left
            while (true)
            {
                //huffmanEncodingTreeNodeList;
                HuffmanEncodingTreeNode lesser, greater;
                lesser = huffmanEncodingTreeNodeList[0];
                huffmanEncodingTreeNodeList.RemoveAt(0);
                greater = huffmanEncodingTreeNodeList[0];
                huffmanEncodingTreeNodeList.RemoveAt(0);

                HuffmanEncodingTreeNode node = new HuffmanEncodingTreeNode();
                node.left = lesser;
                node.right = greater;
                node.weight = lesser.weight + greater.weight;
                lesser.parent = node; // make the tree bi-directionally linked
                greater.parent = node;
                if (huffmanEncodingTreeNodeList.Count == 0)
                {
                    // if we are out of nodes, set the current to be the root
                    root = node;
                    root.parent = null;
                    break;
                }
                InsertNodeIntoSortedList<HuffmanEncodingTreeNode>(node, huffmanEncodingTreeNodeList);
            }
            bool[] invertedPath = new bool[256];
            short invertedPathLength;
            HuffmanEncodingTreeNode currentNode;

            // set the values for each node
            for (int code = 0; code < 256; code++)
            {
                currentNode = leafList[code];

                invertedPathLength = 0;
                do
                {
                    invertedPath[invertedPathLength++] = currentNode.parent.right == currentNode;
                    currentNode = currentNode.parent;
                } while (currentNode != root);

                BitArray bs = new BitArray(invertedPathLength);
                for(int i = 0; i < invertedPathLength; i++)
                {
                    bs.Set(i, invertedPath[invertedPathLength - 1 - i]);
                }
                encodingTable[code] = bs;
            }
        }

        public static void InsertNodeIntoSortedList<T>(T node, List<T> list)
            where T: IComparable<T>
        {
            // FIXME: since list is a sorted list, we should be able to do a
            // binary search first to find the right position
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].CompareTo(node) >= 0)
                {
                    list.Insert(i, node);
                    return;
                }
            }
            list.Add(node);
        }

        #region Bit array to byte array conversions
        /** turns a byteset into a bittuple */
        public static BitTuple ConvertBytes(byte[] bytes)
        {
            BitArray bits = new BitArray(bytes);
            //int i = 0;
            //for (i = 0; i < bytes.Length * 8; i++)
            //{
            //    if ((bytes[(bytes.Length - i) / 8 - 1] & (1 << (i % 8))) > 0)
            //    {
            //        bits.Set(i,true);
            //    }
            //}
            return new BitTuple(bits, bits.Length);
        }

        #endregion

        /** Prints each value in the tree and the associated length of its encoding. */
        public void PrintTable()
        {
            for (int i = 0; i < encodingTable.Length; i++)
            {
                Console.WriteLine("Item " + i + " : " + encodingTable[i].Length);
            }
        }

    }
}
