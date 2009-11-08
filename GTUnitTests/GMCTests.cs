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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Collections.Generic;
using NUnit.Framework;
using GT.Net;
using GT.Utils;
using GT.UnitTests;

namespace GT.GMC
{
    [TestFixture]
    public class GMCTests
    {
        byte[][] bytes = new byte[][] {
                new byte[] { 0 },
                new byte[] { 1 },
                new byte[] { 170 },
                new byte[] { 255 },
                new byte[] { 0, 0, 0, 0 },
                new byte[] { 1, 0, 1, 0 },
                new byte[] { 170, 195, 60, 85 },
                new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                new byte[] { 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 },
                new byte[] { 10, 10, 10, 10, 12, 12, 12, 12 },
                new byte[] { 121, 0, 121, 121, 0, 0, 121, 121, 0 } };

        [Test]
        public void AAATestSorting()
        {
            List<int> values = new List<int>();
            Random rnd = new Random();
            for (int i = 0; i < 255; i++) { values.Add(rnd.Next()); }
            values.Sort();
            AssertIsSorted(values);
            HuffmanEncodingTree.InsertNodeIntoSortedList<int>(25, values);
            HuffmanEncodingTree.InsertNodeIntoSortedList<int>(-100, values);
            HuffmanEncodingTree.InsertNodeIntoSortedList<int>(0, values);
            AssertIsSorted(values);
        }

        [Test]
        public void AAATestBitTupleConversions()
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                BitTuple bt = HuffmanEncodingTree.ConvertBytes(bytes[i]);
                Assert.AreEqual(bytes[i].Length * 8, bt.Length);
                byte[] converted = bt.ToArray();
                //Console.WriteLine("Bytes {0} converted to {1} and back to {2}",
                //    ByteUtils.DumpBytes(bytes[i]), bt.ToString(), ByteUtils.DumpBytes(converted));
                Assert.AreEqual(bytes[i], converted);
            }
        }

        public void AssertIsSorted<T>(IList<T> list) where T : IComparable<T>
        {
            for(int i = 0; i < list.Count - 1; i++) {
                Assert.IsTrue(list[i].CompareTo(list[i+1]) <= 0, "list is not in sorted order!");
            }
        }

        [Test]
        public void AABTestHuffmanEncoding()
        {
            TestHuffEncoding(new uint[256]); // treat all equally
            uint[] frequencies = new uint[256];
            Random rnd = new Random();
            for (int i = 0; i < frequencies.Length; i++) { frequencies[i] = (uint)rnd.Next(); }
            TestHuffEncoding(frequencies);
        }

        protected void TestHuffEncoding(uint[] frequencies)
        {
            HuffmanEncodingTree ht = new HuffmanEncodingTree(frequencies);
            for (int i = 0; i < bytes.Length; i++)
            {
                //byte[] output = ht.EncodeArray(bytes[i]);
                //byte[] second = ht.DecodeArray(output);
                BitTuple encoded = ht.EncodeArrayToBitArray(bytes[i]);
                byte[] decoded = ht.DecodeBitArray(encoded);
                Assert.AreEqual(bytes[i], decoded);
            }
        }

        [Test]
        public void AABTestHuffmanEncodingIsMinimal()
        {
            // Verify that we're selecting the right paths --
            // Huffman with perfect frequencies should produce a smaller encoding.
            for (int i = 0; i < bytes.Length; i++)
            {
                uint[] frequencies = new uint[256];
                for (int index = 0; index < bytes[i].Length; index++)
                {
                    frequencies[bytes[i][index]]++;
                }
                HuffmanEncodingTree ht = new HuffmanEncodingTree(frequencies);
                BitTuple encoded = ht.EncodeArrayToBitArray(bytes[i]);
                byte[] decoded = ht.DecodeBitArray(encoded);
                Assert.AreEqual(bytes[i], decoded);
                Assert.GreaterOrEqual(bytes[i].Length * 8, encoded.Length);
            }
        }

        [Test]
        public void ABATestSuffixTrie()
        {
            SuffixTrie trie = new SuffixTrie();
            trie.Add(new byte[] { 0, 1, 2, 3, 4, 5, 6 });
            int start = 0;
            int escaped = 0;
            uint code = trie.GetCode(new byte[] { 3, 4, 5 }, ref start, out escaped);
            Assert.Less(0, start);
            Assert.LessOrEqual(0, code);    // should be >= 0
            Assert.LessOrEqual(escaped, start);    // no escaped chars there!
            Assert.AreEqual(new byte[] { 3, 4, 5 }, trie.GenerateEncodingTable()[(int)code]);

            start = 0;
            code = trie.GetCode(new byte[] { 0, 1, 2, 10, 222, 111 }, ref start, out escaped);
            Assert.AreEqual(3, start);
            Assert.LessOrEqual(0, code);
            Assert.AreEqual(3, escaped - start);
            Assert.AreEqual(new byte[] { 0, 1, 2 }, trie.GenerateEncodingTable()[(int)code]);

            start = 0;
            code = trie.GetCode(new byte[] { 4, 5, 10, 222, 111 }, ref start, out escaped);
            Assert.AreEqual(2, start);
            Assert.LessOrEqual(0, code);
            Assert.AreEqual(new byte[] { 4, 5 }, trie.GenerateEncodingTable()[(int)code]);
            Assert.AreEqual(3, escaped - start);
        }

        [Test]
        public void ABATestEncodingTable()
        {
            SuffixTrie trie = new SuffixTrie();
            trie.Add(new byte[] { 0, 1, 2, 3, 4, 5, 6 });
            IList<byte[]> table = trie.GenerateEncodingTable();
            for (int i = 0; i < table.Count; i++)
            {
                int startIndex = 0;
                int escapedIndex;
                uint code = trie.GetCode(table[i], ref startIndex, out escapedIndex);
                Assert.AreEqual(table[i].Length, startIndex);
                Assert.LessOrEqual(escapedIndex, startIndex);
                Assert.AreEqual(i, (int)code);
            }
        }

        [Test]
        public void ACATestIntegerCompressor()
        {
            TrieCompressor ic = new TrieCompressor((short)0, new byte[] { 0, 1, 2, 3, 4 });
            for (uint i = 0; i < 10; i++)
            {
                byte b = ic.WriteShortcut(i, MemoryStream.Null);
                Assert.AreEqual(2 + i, b, "shortcuts should be assigned sequentially");
            }
            for (uint i = 0; i < 10; i++)
            {
                byte b = ic.WriteShortcut(i, MemoryStream.Null);
                Assert.AreEqual(2 + i, b, "shortcuts should be assigned the same byte");
            }
        }

        [Test]
        public void ADATestTrieCompressor()
        {
            string[] testStrings = { "ROCKANDROLLLIVES", "i1001234123412341234123412341234123",
                "Roses are red", "Violets are blue", "GMC is great", "and so are you" };

            MemoryStream template = new MemoryStream();
            foreach (string s in testStrings)
            {
                byte[] message = UTF8Encoding.UTF8.GetBytes(s);
                template.Write(message, 0, message.Length);
            }

            TrieCompressor tc = new TrieCompressor(0, template.ToArray());
            foreach (string s in testStrings)
            {
                byte[] message = UTF8Encoding.UTF8.GetBytes(s);
                byte[] encoded = tc.Encode(message);
                Assert.Greater(encoded.Length, 0);
                byte[] decoded = tc.Decode(encoded);
                ByteUtils.ShowDiffs("Difference in bytes", message, decoded);
                Assert.AreEqual(message, decoded);
            }
        }


        [Test]
        public void ADDTestTBC()
        {
            string[] testStrings = { "ROCKANDROLLLIVES", "i1001234123412341234123412341234123",
                "Roses are red", "Violets are blue", "GMC is great", "and so are you" };

            MemoryStream template = new MemoryStream();
            foreach(string s in testStrings) {
                    byte[] message = UTF8Encoding.UTF8.GetBytes(s);
                template.Write(message, 0, message.Length);
            }

            TemplateBasedCompressor tbc = new TemplateBasedCompressor(0, template.ToArray(), false);
            foreach(string s in testStrings) {
                byte[] message = UTF8Encoding.UTF8.GetBytes(s);
                CompressedMessagePackage cmp = tbc.Encode(message);
                byte[] result = tbc.Decode(cmp);
                ByteUtils.ShowDiffs("Difference in bytes", message, result);
                Assert.AreEqual(message, result);
            }

        }

        [Test]
        public void AZATestDecreasingMessageSizes()
        {
            GMCMarshaller gmc = new GMCMarshaller(new LightweightDotNetSerializingMarshaller());
            string[] testStrings = { "ROCKANDROLLLIVES", "i1001234123412341234123412341234123",
                "Roses are red", "Violets are blue", "GMC is great", "and so are you" };
            int[] sizes = new int[testStrings.Length];

            for (int i = 0; i < 20; i++)
            {
                for (int j = 0; j < testStrings.Length; j++)
                {
                    byte[] message = UTF8Encoding.UTF8.GetBytes(testStrings[j]);
                    byte[] result = gmc.Encode(message);
                    if (sizes[j] == 0)
                    {
                        sizes[j] = result.Length;
                    }
                    else
                    {
                        if (result.Length > sizes[j])
                        {
                            Debug.WriteLine(String.Format("GMC'ed message #{0} expanded since last time!", j));
                        }
                        sizes[j] = result.Length;
                    }

                    byte[] decoded = gmc.Decode(0, result);
                    ByteUtils.ShowDiffs("GMC encoding/decoding difference", message, decoded);
                    Assert.AreEqual(message, decoded);
                }
            }            
        }

        [Test]
        public void BBBTestUnicode()
        {
            GMCMarshaller gmc = new GMCMarshaller(new LightweightDotNetSerializingMarshaller());
            string[] testStrings = { "We also need some normal content", 
                "ROCKANDROLLLIVES", "i1001234123412341234123412341234123",
                "Roses are red", "Violets are blue", "GMC is great", "and so are you",
                "เ, แ, โ, ใ, ไ", "\u005C\uFF5E\u301C" };
            int[] sizes = new int[testStrings.Length];

            for (int i = 0; i < 20; i++)
            {
                for (int j = 0; j < testStrings.Length; j++)
                {
                    byte[] message = UTF8Encoding.UTF8.GetBytes(testStrings[j]);
                    byte[] result = gmc.Encode(message);
                    if (sizes[j] == 0)
                    {
                        sizes[j] = result.Length;
                    }
                    else
                    {
                        if (result.Length > sizes[j])
                        {
                            Debug.WriteLine(String.Format("GMC'ed message #{0} expanded since last time!", j));
                        }
                        sizes[j] = result.Length;
                    }

                    byte[] decoded = gmc.Decode(0, result);
                    ByteUtils.ShowDiffs("GMC encoding/decoding difference", message, decoded);
                    Assert.AreEqual(message, decoded);
                }
            }
        }

        [Test]
        public void CCCTestXML()
        {
            //DebugUtils.Verbose = true;
            // DebugUtils.writer = new StreamWriter("report.txt", false);
            TestCompressionOfFileContent("..\\..\\test.xml", Encoding.UTF8);
            //DebugUtils.Verbose = false;
        }

        [Test]
        public void CCCTestUnicode()
        {
            TestCompressionOfFileContent("..\\..\\UTF-8-demo.txt", UTF8Encoding.UTF8);
        }

        protected void TestCompressionOfFileContent(string filename, Encoding encoding)
        {
            GMCMarshaller gmc = new GMCMarshaller(new LightweightDotNetSerializingMarshaller());

            char[] delim = { '\n' };
            string s = File.ReadAllText(filename, encoding);
            string[] sList = s.Split(delim);

            int count = 0;
            Console.WriteLine(String.Format("GMC: compressing {0} lines in {1}", sList.Length, filename));
            Stopwatch stopwatch = Stopwatch.StartNew();
            foreach (string line in sList)
            {
                count++;
                byte[] messageToEncode = UTF8Encoding.UTF8.GetBytes(line);
                try
                {
                    byte[] encodedMessage = gmc.Encode(messageToEncode);

                    if (encodedMessage.Length > messageToEncode.Length)
                    {
                        Debug.WriteLine(String.Format("GMC'ing message #{0} has grown from {1} bytes to {2} bytes",
                            count, messageToEncode.Length, encodedMessage.Length));
                    }

                    byte[] decoded = gmc.Decode(0, encodedMessage);

                    ByteUtils.ShowDiffs("Line " + count, messageToEncode, decoded);
                    Assert.AreEqual(messageToEncode, decoded);
                    if ((count % (sList.Length / 100)) == 0)
                    {
                        Console.Write(count + " ");
                    }
                }
                catch (ShortcutsExhaustedException e)
                {
                    Console.WriteLine("***Shortcuts Exhausted: line {0} of {1}: {2}", count, filename, line);
                    throw e;
                }
            }

            Console.WriteLine("GMC: Encoding/Decoding {0} messages took {1}ms (avg {2}ms/msg)", 
                sList.Length, stopwatch.ElapsedMilliseconds,
                (float)stopwatch.ElapsedMilliseconds / (float)sList.Length);
            Console.WriteLine("  uncompressed {0} bytes, compressed text {1} bytes", 
                gmc.totalUncompressedBytes, gmc.totalCompressedBytes);
            Console.WriteLine("  templates = {0} bytes, announcements = {1} bytes, frequencies = {2} bytes",
                gmc.totalTemplateBytes, gmc.totalAnnouncementBytes, gmc.totalFrequenciesBytes);
        }

        [Test]
        public void XTestGMCMarshaller()
        {
            IMarshaller submrsh = new DotNetSerializingMarshaller();
            GMCMarshaller mrsh = new GMCMarshaller(submrsh);

            Assert.AreEqual(mrsh.Descriptor.Length, submrsh.Descriptor.Length);
            Assert.AreNotEqual(mrsh.Descriptor, submrsh.Descriptor);

            new MarshallerTestsHelper().CheckMarshaller(mrsh);
        }
    }
}
