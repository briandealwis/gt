using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using GT.Net;

namespace GT.UnitTests
{
    [TestFixture]
    public class PacketTests
    {
        [Test]
        public void TestBasics()
        {
            byte[] source = new byte[] { 0, 1, 2, 3, 4 };
            TransportPacket p = new TransportPacket(source, 1, 4);
            Assert.AreEqual(4, p.Length);
            Assert.AreEqual(1, ((IList<ArraySegment<byte>>)p).Count);
            Assert.IsTrue(source == ((IList<ArraySegment<byte>>)p)[0].Array);
            Assert.AreEqual(1, ((IList<ArraySegment<byte>>)p)[0].Offset);
            Assert.AreEqual(4, ((IList<ArraySegment<byte>>)p)[0].Count);

            byte[] result = p.ToArray();
            Assert.AreEqual(4, result.Length);
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(source[1 + i], result[i]);
            }

            TransportPacket p2 = p.SplitAt(2);
            Assert.AreEqual(2, p.Length);
            Assert.AreEqual(2, p2.Length);
            Assert.IsTrue(source == ((IList<ArraySegment<byte>>)p)[0].Array);
            Assert.IsTrue(source != ((IList<ArraySegment<byte>>)p2)[0].Array);
            Assert.AreEqual(new byte[] { 1, 2 }, p.ToArray());
            Assert.AreEqual(new byte[] { 3, 4 }, p2.ToArray());
        }

        [Test]
        public void TestMultiSegments()
        {
            byte[] source = new byte[] { 0, 1, 2, 3, 4 };
            TransportPacket p = new TransportPacket(source, 1, 4);
            Assert.AreEqual(4, p.Length);
            Assert.AreEqual(1, ((IList<ArraySegment<byte>>)p).Count);

            for (int i = 0; i < 10; i++)
            {
                p.Add(source, 1, 4);
            }
            Assert.AreEqual(11, ((IList<ArraySegment<byte>>)p).Count);
            Assert.AreEqual(4 * 11, p.Length);

            for (int i = 0; i < ((IList<ArraySegment<byte>>)p).Count; i++)
            {
                Assert.IsTrue(source == ((IList<ArraySegment<byte>>)p)[i].Array);
                Assert.AreEqual(1, ((IList<ArraySegment<byte>>)p)[i].Offset);
                Assert.AreEqual(4, ((IList<ArraySegment<byte>>)p)[i].Count);
            }

            byte[] result = p.ToArray();
            Assert.AreEqual(4 * 11, result.Length);
            for (int j = 0; j < 11; j++)
            {
                for (int i = 0; i < 4; i++)
                {
                    Assert.AreEqual(source[1 + i], result[4 * j + i]);
                }
            }
        }

        [Test]
        public void TestSubset()
        {
            byte[] source = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            // We test for various subsets that overlap partially or fully with the different segments
            for (int sourceStart = 0; sourceStart < source.Length / 2; sourceStart++)
            {
                for(int sourceEnd = source.Length - 1; sourceEnd - sourceStart > 0; sourceEnd--)
                {
                    int sourceCount = sourceEnd - sourceStart + 1;
                    TransportPacket p = new TransportPacket(source, sourceStart, sourceCount);
                    Assert.AreEqual(sourceCount, p.Length);
                    Assert.AreEqual(1, ((IList<ArraySegment<byte>>)p).Count);

                    for(int i = 0; i < 10; i++)
                    {
                        p.Add(source, sourceStart, sourceCount);
                    }
                    Assert.AreEqual(11, ((IList<ArraySegment<byte>>)p).Count);
                    Assert.AreEqual(sourceCount * 11, p.Length);

                    int subsetStart = 4;
                    int subsetCount = Math.Min(17, p.Length - 2);
                    TransportPacket subset = p.Subset(subsetStart, subsetCount);
                    Assert.AreEqual(subsetCount, subset.Length);
                    byte[] result = subset.ToArray();
                    Assert.AreEqual(subsetCount, result.Length);
                    for(int i = 0; i < result.Length; i++)
                    {
                        Assert.AreEqual(source[sourceStart + 
                            ((subsetStart + i) % sourceCount)], result[i]);
                    }

                    // And ensure the backing byte array is still referenced 
                    byte index = (byte)(sourceStart + (subsetStart % sourceCount));
                    Assert.AreEqual(index, source[index]);
                    source[index] = 255;
                    for (int j = 0; j < subset.Length; j += sourceCount)
                    {
                        Assert.AreEqual(source[index], subset.ToArray()[j]);
                    }
                    source[index] = index;
                    for (int j = 0; j < subset.Length; j += sourceCount)
                    {
                        Assert.AreEqual(source[index], subset.ToArray()[j]);
                    }
                }
            }
        }

        [Test]
        public void TestReadStream()
        {
            byte[] source = new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9};
            int sourceStart = 1;
            int sourceCount = 6;
            TransportPacket packet = new TransportPacket();
            packet.Add(source, sourceStart, sourceCount);
            packet.Add(source, sourceStart, sourceCount);
            packet.Add(source, sourceStart, sourceCount);

            Stream s = packet.AsReadStream();
            int packetLength = packet.Length;
            Assert.AreEqual(3 * sourceCount, packetLength);
            for (int i = 0; i < packetLength; i++)
            {
                Assert.AreEqual(source[sourceStart + (i % sourceCount)], packet.ToArray()[0]);
                Assert.AreEqual(source[sourceStart + (i % sourceCount)], s.ReadByte());
                Assert.AreEqual(packetLength - i - 1, s.Length);
                Assert.AreEqual(packetLength - i - 1, packet.Length);
            }
            Assert.AreEqual(0, s.Length);
        }

        [Test]
        public void TestToArray()
        {
            TransportPacket packet = new TransportPacket();
            packet.Add(new byte[] {0, 1, 2, 3});
            packet.Add(new byte[] {4, 5, 6, 7, 8});
            packet.Add(new byte[] {9});

            byte[] original = new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9};
            Assert.AreEqual(original, packet.ToArray());

            for(int i = 0; i < packet.Length; i++)
            {
                for(int count = 0; count < packet.Length - i; count++)
                {
                    byte[] sub = packet.ToArray(i, count);
                    for(int j = 0; j < count; j++)
                    {
                        Assert.AreEqual(original[i + j], sub[j]);
                    }
                }
            }

        }
    }
}