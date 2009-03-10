using System;
using System.Collections;
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
                for (int sourceEnd = source.Length - 1; sourceEnd - sourceStart > 0; sourceEnd--)
                {
                    int sourceCount = sourceEnd - sourceStart + 1;
                    TransportPacket p = new TransportPacket(source, sourceStart, sourceCount);
                    Assert.AreEqual(sourceCount, p.Length);
                    Assert.AreEqual(1, ((IList<ArraySegment<byte>>)p).Count);

                    for (int i = 0; i < 10; i++)
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
                    for (int i = 0; i < result.Length; i++)
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
        public void TestSplitAt()
        {
            byte[] source = new byte[256];
            for (int i = 0; i < source.Length; i++) { source[i] = (byte)i; }
            TransportPacket packet = new TransportPacket();
            packet.Add(source);
            Assert.AreEqual(source.Length, packet.Length);
            TransportPacket end = packet.SplitAt(128);
            Assert.AreEqual(128, packet.Length);
            Assert.AreEqual(source.Length - 128, end.Length);
            Assert.IsTrue(((IList<ArraySegment<byte>>)packet)[0].Array != ((IList<ArraySegment<byte>>)end)[0].Array);
            for (int i = 0; i < packet.Length; i++)
            {
                Assert.AreEqual(source[i], packet.ByteAt(i));
            }
            for (int i = 0; i < end.Length; i++)
            {
                Assert.AreEqual(source[128 + i], end.ByteAt(i));
            }
        }

        [Test]
        public void TestReadStream()
        {
            byte[] source = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
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
        public void TestByteAt()
        {
            TransportPacket packet = new TransportPacket();
            packet.Add(new byte[] {0, 1, 2, 3});
            packet.Add(new byte[] {4, 5, 6, 7, 8});
            packet.Add(new byte[] {9});

            for(int i = 0; i < 10; i++)
            {
                Assert.AreEqual(i, packet.ByteAt(i));
                packet.BytesAt(i, 1, (b,offset) => Assert.AreEqual(i, b[offset]));
            }

            packet.BytesAt(0, 10, (bytes, offset) => {
                for(int i = 0; i < 10; i++) { Assert.AreEqual(i, bytes[i]); }
            });

            try
            {
                packet.ByteAt(10);
                Assert.Fail("Should have thrown ArgumentOutOfRange");
            }
            catch (ArgumentOutOfRangeException) { /*ignore*/ }

            try
            {
                packet.BytesAt(10,1, (b,o) => Assert.Fail("should have thrown AOOR"));
                Assert.Fail("Should have thrown ArgumentOutOfRange");
            }
            catch (ArgumentOutOfRangeException) { /*ignore*/ }

            try
            {
                packet.BytesAt(8, 8, (b, o) => Assert.Fail("should have thrown AOOR"));
                Assert.Fail("Should have thrown ArgumentOutOfRange");
            }
            catch (ArgumentOutOfRangeException) { /*ignore*/ }
        }

        [Test]
        public void TestToArray()
        {
            TransportPacket packet = new TransportPacket();
            packet.Add(new byte[] { 0, 1, 2, 3 });
            packet.Add(new byte[] { 4, 5, 6, 7, 8 });
            packet.Add(new byte[] { 9 });

            byte[] original = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            Assert.AreEqual(original, packet.ToArray());

            for (int i = 0; i < packet.Length; i++)
            {
                for (int count = 0; count < packet.Length - i; count++)
                {
                    byte[] sub = packet.ToArray(i, count);
                    for (int j = 0; j < count; j++)
                    {
                        Assert.AreEqual(original[i + j], sub[j]);
                    }
                }
            }

        }

        [Test]
        public void TestWriteStream()
        {
            /// This tests ReplaceBytes too.
            byte[] bytes = new byte[255];
            for (int i = 0; i < bytes.Length; i++) { bytes[i] = (byte)(i % 256); }

            TransportPacket tp = new TransportPacket();
            Stream stream = tp.AsWriteStream();
            for (int i = 0; i < 255; i++)
            {
                stream.Write(bytes, i, bytes.Length - i);
                stream.Write(bytes, 0, i);
            }
            stream.Flush();

            Assert.AreEqual(bytes.Length * 255, tp.Length);
            byte[] copy = tp.ToArray();
            Assert.AreEqual(bytes.Length * 255, copy.Length);
            for (int i = 0; i < 255; i++)
            {
                for (int j = 0; j < bytes.Length; j++)
                {
                    Assert.AreEqual(bytes[(i + j) % bytes.Length], copy[i * bytes.Length + j]);
                }
            }
        }

        [Test]
        public void TestRemoveBytes() {
            byte[] bytes = new byte[256];
            for (int i = 0; i < bytes.Length; i++) { bytes[i] = (byte)(i % 256); }
            byte[] reversed = new byte[bytes.Length];
            Array.Copy(bytes, reversed, bytes.Length);
            Array.Reverse(reversed);

            TransportPacket tp = new TransportPacket();
            Stream stream = tp.AsWriteStream();
            // we'll make 256 copies of [0,1,...,254,255,255,254,...,1,0]
            for (int i = 0; i < 256; i++)
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Write(reversed, 0, reversed.Length);
            }
            stream.Flush();
            Assert.AreEqual((bytes.Length + reversed.Length) * 256, tp.Length);

            byte[] copy = tp.ToArray();
            Assert.AreEqual((bytes.Length + reversed.Length) * 256, tp.Length);

            // Now remove successively larger chunks from between the bytes/reversed 
            // boundary points
            int nextIndex = 0;  // the start into the next bytes/reverse pair
            for (int i = 0; i < bytes.Length / 2; i++)
            {
                Assert.AreEqual(0, tp.ByteAt(nextIndex));
                Assert.AreEqual(1, tp.ByteAt(nextIndex + 1));
                Assert.AreEqual(bytes.Length - 2, tp.ByteAt(nextIndex + bytes.Length - 2));
                Assert.AreEqual(bytes.Length - 1, tp.ByteAt(nextIndex + bytes.Length - 1));
                Assert.AreEqual(bytes.Length - 1, tp.ByteAt(nextIndex + bytes.Length));
                Assert.AreEqual(bytes.Length - 2, tp.ByteAt(nextIndex + bytes.Length +1));
                Assert.AreEqual(1,
                    tp.ByteAt(nextIndex + bytes.Length + reversed.Length - 2));
                Assert.AreEqual(0,
                    tp.ByteAt(nextIndex + bytes.Length + reversed.Length - 1));

                // remove 2i bytes from the end of the bytes copy extending
                tp.RemoveBytes(nextIndex + bytes.Length - i, 2 * i);
                Assert.AreEqual((bytes.Length + reversed.Length) * 256 - 2 * i * (i+1) / 2, tp.Length);

                Assert.AreEqual(0, tp.ByteAt(nextIndex));
                Assert.AreEqual(bytes.Length - i - 1, tp.ByteAt(nextIndex + bytes.Length - i - 1));
                Assert.AreEqual(bytes.Length - i - 1, tp.ByteAt(nextIndex + bytes.Length - i));
                Assert.AreEqual(0, tp.ByteAt(nextIndex + bytes.Length + reversed.Length - 2*i - 1));

                nextIndex += bytes.Length + reversed.Length - 2 * i;
            }
        }
    }
}