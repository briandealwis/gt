using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GT.Utils;
using NUnit.Framework;
using GT.Net;

namespace GT.UnitTests
{
    [TestFixture]
    public class AZPacketTests
    {
        uint _OriginalMinSegSize;
        uint _OriginalMaxSegSize;

        [SetUp]
        public void SetUp()
        {
            // We need to set the min segment size as otherwise our tiny byte arrays
            // are amalgamated into giant packets.
            _OriginalMinSegSize = TransportPacket.MinSegmentSize;
            _OriginalMaxSegSize = TransportPacket.MaxSegmentSize;
            TransportPacket.TestingDiscardPools();
            TransportPacket.MinSegmentSize = 4;
        }

        [TearDown]
        public void TearDown()
        {
            TransportPacket.TestingDiscardPools();
            TransportPacket.MinSegmentSize = _OriginalMinSegSize;
            TransportPacket.MaxSegmentSize = _OriginalMaxSegSize;
        }

        private void CheckForUndisposedSegments()
        {
            Pool<byte[]>[] pools = TransportPacket.TestingDiscardPools();
            if (pools != null)
            {
                foreach (Pool<byte[]> pool in pools)
                {
                    Assert.AreEqual(0, pool.Out,
                        "there are non-disposed segments: some TransportPackets remain undisposed");
                }
            }
        }


        private void CheckDisposed(params TransportPacket[] packets)
        {
            List<ArraySegment<byte>> segs = new List<ArraySegment<byte>>();
            foreach (TransportPacket p in packets) { segs.AddRange(p); }
            foreach (ArraySegment<byte> seg in segs)
            {
                Assert.IsTrue(TransportPacket.IsValidSegment(seg));
            }
            foreach (TransportPacket p in packets) { p.Dispose(); }
            foreach (ArraySegment<byte> seg in segs)
            {
                Assert.IsFalse(TransportPacket.IsValidSegment(seg));
            }
        }

        // This packet is a subset of another, and so none of the segments should be disposed
        private void CheckNotDisposed(TransportPacket subset)
        {
            List<ArraySegment<byte>> segs = new List<ArraySegment<byte>>(subset);
            foreach (ArraySegment<byte> seg in segs)
            {
                Assert.IsTrue(TransportPacket.IsValidSegment(seg));
            }
            subset.Dispose();
            foreach (ArraySegment<byte> seg in segs)
            {
                Assert.IsTrue(TransportPacket.IsValidSegment(seg));
            }
        }

        [Test]
        public void TestPoolIndexCalculations()
        {
            /// Ensure that the pool-figuring  code generates the right pools
            /// for provided lengths.  We use a simple power-of-2 scheme where
            /// we round lengths up to their nearest power of 2.
            Assert.AreEqual(4, TransportPacket.MinSegmentSize, 
                "This test is assuming a different value for the MinSegmentSize value provided in [SetUp]");
            Assert.AreEqual(0, TransportPacket.TestingPoolIndex(1));
            Assert.AreEqual(0, TransportPacket.TestingPoolIndex(2));
            Assert.AreEqual(0, TransportPacket.TestingPoolIndex(3));
            Assert.AreEqual(0, TransportPacket.TestingPoolIndex(4));
            Assert.AreEqual(1, TransportPacket.TestingPoolIndex(5));
            Assert.AreEqual(1, TransportPacket.TestingPoolIndex(6));
            Assert.AreEqual(1, TransportPacket.TestingPoolIndex(7));
            Assert.AreEqual(1, TransportPacket.TestingPoolIndex(8));
            Assert.AreEqual(2, TransportPacket.TestingPoolIndex(9));
            Assert.AreEqual(2, TransportPacket.TestingPoolIndex(16));
            Assert.AreEqual(3, TransportPacket.TestingPoolIndex(17));
            Assert.AreEqual(3, TransportPacket.TestingPoolIndex(32));
            Assert.AreEqual(4, TransportPacket.TestingPoolIndex(33));
            Assert.AreEqual(4, TransportPacket.TestingPoolIndex(64));
            Assert.AreEqual(5, TransportPacket.TestingPoolIndex(65));
            Assert.AreEqual(5, TransportPacket.TestingPoolIndex(128));
            Assert.AreEqual(6, TransportPacket.TestingPoolIndex(129));
            Assert.AreEqual(6, TransportPacket.TestingPoolIndex(256));
            Assert.AreEqual(7, TransportPacket.TestingPoolIndex(257));
            Assert.AreEqual(7, TransportPacket.TestingPoolIndex(512));

            int numBits = BitUtils.HighestBitSet(TransportPacket.MaxSegmentSize) -
                BitUtils.HighestBitSet(TransportPacket.MinSegmentSize);
            for (int i = 0; i < numBits; i++)
            {
                uint start = TransportPacket.MinSegmentSize * (i == 0 ? 0 : 1u << (i-1)) + 1;
                uint stop = TransportPacket.MinSegmentSize * (1u << i);
                Assert.AreEqual(i, TransportPacket.TestingPoolIndex(start));
                Assert.AreEqual(i, TransportPacket.TestingPoolIndex(stop));
            }
        }

        [Test]
        public void TestAllocation() {
            /// This test is specific to the TransportPacket pool allocation scheme
            /// We go up in power-of-2 checking that allocating just before and 
            /// on the boundary return the proper size, and that allocating just
            /// after the boundary allocates the next up.
            int numBits = BitUtils.HighestBitSet(TransportPacket.MaxSegmentSize) -
                BitUtils.HighestBitSet(TransportPacket.MinSegmentSize);
            for (int i = 0; i <= numBits; i++)
            {
                uint size = TransportPacket.MinSegmentSize * (1u << i);
                TransportPacket p;
                ArraySegment<byte> segment;

                p = new TransportPacket(size-1);
                Assert.AreEqual(size-1, p.Length);
                Assert.AreEqual(1, ((IList<ArraySegment<byte>>)p).Count);
                segment = ((IList<ArraySegment<byte>>)p)[0];
                Assert.AreEqual(size - 1, segment.Count);
                Assert.AreEqual(size, segment.Array.Length - segment.Offset);
                p.Dispose();


                p = new TransportPacket(size);
                Assert.AreEqual(size, p.Length);
                Assert.AreEqual(1, ((IList<ArraySegment<byte>>)p).Count);
                segment = ((IList<ArraySegment<byte>>)p)[0];
                Assert.AreEqual(size, segment.Count);
                Assert.AreEqual(size, segment.Array.Length - segment.Offset);
                p.Dispose();

                p = new TransportPacket(size+1);
                Assert.AreEqual(size + 1, p.Length);
                if (i < numBits)
                {
                    Assert.AreEqual(1, ((IList<ArraySegment<byte>>)p).Count);
                    segment = ((IList<ArraySegment<byte>>)p)[0];
                    Assert.AreEqual(size + 1, segment.Count);
                    Assert.IsTrue(size < segment.Array.Length - segment.Offset,
                        "Should have been in the next bin size");
                }
                else
                {
                    // we're outside of the maximum allocation size, and so 
                    // the allocation should be split across multiple segments!
                    Assert.AreEqual(2, ((IList<ArraySegment<byte>>)p).Count);
                    segment = ((IList<ArraySegment<byte>>)p)[0];
                    Assert.AreEqual(size, segment.Count, "Should have been this bin size");
                    Assert.AreEqual(size, segment.Array.Length - segment.Offset,
                        "Should have been in this last bin size");
                }
                p.Dispose();
            }
            CheckForUndisposedSegments();
        }

        [Test]
        public void TestBasics()
        {
            byte[] source = new byte[] { 0, 1, 2, 3, 4 };
            TransportPacket p = new TransportPacket(source, 1, 4);
            Assert.AreEqual(4, p.Length);
            Assert.AreEqual(1, ((IList<ArraySegment<byte>>)p).Count);
            Assert.AreEqual(4, ((IList<ArraySegment<byte>>)p)[0].Count);

            byte[] result = p.ToArray();
            Assert.AreEqual(4, result.Length);
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(source[1 + i], result[i]);
            }

            CheckDisposed(p);
            CheckForUndisposedSegments();
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
            CheckDisposed(packet);
            CheckForUndisposedSegments();
        }

        [Test]
        public void TestNewTransportPacket()
        {
            TransportPacket.MaxSegmentSize = TransportPacket.MinSegmentSize;

            TransportPacket packet = new TransportPacket();
            packet.Add(new byte[] { 0, 1, 2, 3 });
            packet.Add(new byte[] { 4, 5, 6, 7, 8 });
            packet.Add(new byte[] { 9 });

            // Ensure that new TransportPacket() properly copies out
            TransportPacket copy = new TransportPacket(packet, 1, 8);
            Assert.AreEqual(packet.ToArray(1, 8), copy.ToArray());
            CheckDisposed(packet, copy);
            CheckForUndisposedSegments();
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
            Assert.AreEqual(4 * 11, p.Length);

            byte[] result = p.ToArray();
            Assert.AreEqual(4 * 11, result.Length);
            for (int j = 0; j < 11; j++)
            {
                for (int i = 0; i < 4; i++)
                {
                    Assert.AreEqual(source[1 + i], result[4 * j + i]);
                }
            }

            CheckDisposed(p);
            CheckForUndisposedSegments();
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
                    TransportPacket packet = new TransportPacket(source, sourceStart, sourceCount);
                    Assert.AreEqual(sourceCount, packet.Length);
                    Assert.AreEqual(1, ((IList<ArraySegment<byte>>)packet).Count);

                    for (int i = 0; i < 10; i++)
                    {
                        packet.Add(source, sourceStart, sourceCount);
                    }
                    Assert.AreEqual(sourceCount * 11, packet.Length);

                    const int subsetStart = 4;
                    int subsetCount = Math.Min(17, packet.Length - 2);

                    TransportPacket subset = packet.Subset(subsetStart, subsetCount);
                    Assert.AreEqual(subsetCount, subset.Length);
                    byte[] result = subset.ToArray();
                    Assert.AreEqual(subsetCount, result.Length);
                    for (int i = 0; i < result.Length; i++)
                    {
                        Assert.AreEqual(source[sourceStart +
                            ((subsetStart + i) % sourceCount)], result[i]);
                        Assert.AreEqual(source[sourceStart +
                            ((subsetStart + i) % sourceCount)], subset.ByteAt(i));
                        Assert.AreEqual(packet.ByteAt(subsetStart + i), subset.ByteAt(i));
                    }

                    // And ensure the subset has the same backing byte array is still referenced 
                    // sourceEquivIndex = the equivalent index in source to subset[0]
                    int sourceEquivIndex = sourceStart + (subsetStart % sourceCount);
                    Assert.AreEqual(source[sourceEquivIndex], packet.ByteAt(subsetStart));
                    Assert.AreEqual(source[sourceEquivIndex], subset.ByteAt(0));
                    packet.Replace(subsetStart, new byte[] { 255 }, 0, 1);
                    Assert.AreEqual(255, packet.ByteAt(subsetStart));
                    Assert.AreEqual(255, subset.ByteAt(0));
                    packet.Replace(subsetStart, source, sourceEquivIndex, 1);
                    Assert.AreEqual(source[sourceEquivIndex], packet.ByteAt(subsetStart));
                    Assert.AreEqual(source[sourceEquivIndex], subset.ByteAt(0));

                    CheckNotDisposed(subset);

                    /// Ensure that disposing of the subset doesn't dispose the parent packet
                    for (int i = 0; i < packet.Length; i++)
                    {
                        Assert.AreEqual(source[sourceStart + (i % sourceCount)], packet.ByteAt(i));
                    }
                    CheckDisposed(packet);
                }
            }
            CheckForUndisposedSegments();
        }

        [Test]
        public void TestSplitAt()
        {
            TransportPacket packet = new TransportPacket();
            for (int i = 0; i < 256;) { 
                byte[] source = new byte[8];
                for (int j = 0; j < source.Length; j++, i++)
                {
                    source[j] = (byte)i;
                }
                packet.Add(source);
            }
            Assert.AreEqual(256, packet.Length);
            Assert.IsTrue(((IList<ArraySegment<byte>>)packet).Count > 1);

            for (int splitPoint = 0; splitPoint < packet.Length; splitPoint++)
            {
                TransportPacket front = packet.Copy();
                TransportPacket back = front.SplitAt(splitPoint);
                Assert.AreEqual(splitPoint, front.Length);
                Assert.AreEqual(packet.Length - splitPoint, back.Length);

                int index = 0;
                for(; index < front.Length; index++)
                {
                    Assert.AreEqual((byte)index, front.ByteAt(index));
                }
                for(int i = 0; i < back.Length; i++)
                {
                    Assert.AreEqual((byte)(index + i), back.ByteAt(i));
                }
                CheckNotDisposed(front);
                CheckNotDisposed(back);
            }
            CheckDisposed(packet);
            CheckForUndisposedSegments();
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
            CheckDisposed(packet);
            CheckForUndisposedSegments();
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
            CheckDisposed(packet);
            CheckForUndisposedSegments();
        }

        [Test]
        public void TestWriteStream()
        {
            /// This tests ReplaceBytes too.
            byte[] bytes = new byte[255];
            for (int i = 0; i < bytes.Length; i++) { bytes[i] = (byte)(i % 256); }

            TransportPacket tp = new TransportPacket();
            Stream stream = tp.AsWriteStream();
            long initialPosition = stream.Position;
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

            stream.Position = initialPosition;
            stream.Position = stream.Length;
            CheckDisposed(tp);
            CheckForUndisposedSegments();
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

            CheckDisposed(tp);
            CheckForUndisposedSegments();
        }

        [Test]
        public void TestPrepending()
        {
            // Ensure small packets
            TransportPacket.MaxSegmentSize = TransportPacket.MinSegmentSize;

            TransportPacket packet = new TransportPacket(new byte[] { 9 });
            packet.Prepend(new byte[] { 4, 5, 6, 7, 8 });
            packet.Prepend(new byte[] { 0, 1, 2, 3 });

            Assert.AreEqual(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, packet.ToArray());
            CheckDisposed(packet);
            CheckForUndisposedSegments();
        }

    }
}