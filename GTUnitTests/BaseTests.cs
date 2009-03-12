using System;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using NUnit.Framework;
using GT.Net;
using GT.Utils;
using ThreadState=System.Threading.ThreadState;

namespace GT.UnitTests
{
    /// <remarks>Test basic converter functionality</remarks>
    [TestFixture]
    public class BaseStatisticsTests
    {
        #region "Tests"

        [Test]
        public void TestEmptyNaN()
        {
            StatisticalMoments sm = new StatisticalMoments();
            Assert.AreEqual(0, sm.Count());
            Assert.AreEqual(0, sm.Average());
            Assert.AreEqual(0, sm.UnnormalizedVariance());
            Assert.IsNaN(sm.Variance());
            Assert.IsNaN(sm.Skewness());
            Assert.IsNaN(sm.Kurtosis());
        }

        [Test]
        public void TestZeros()
        {
            StatisticalMoments sm = new StatisticalMoments();
            for (int i = 0; i < 5; i++) { sm.Accumulate(0); }
            Assert.AreEqual(5, sm.Count());
            Assert.AreEqual(0, sm.Average());
            Assert.AreEqual(0, sm.UnnormalizedVariance());
            Assert.AreEqual(0, sm.Variance());
            Assert.IsNaN(sm.Skewness());    // must be NaN if variance is 0
            Assert.IsNaN(sm.Kurtosis());    // must be NaN if variance is 0
        }

        [Test]
        public void TestSomeData()
        {
            StatisticalMoments sm = new StatisticalMoments();
            for (int i = 0; i < 10; i++) { sm.Accumulate(i); }
            Assert.AreEqual(10, sm.Count());
            Assert.AreEqual(4.5, sm.Average());
            Assert.AreEqual(82.5, sm.UnnormalizedVariance());
            Assert.Greater(9.16667, sm.Variance());
            Assert.Less(9.16666, sm.Variance());
            Assert.AreEqual(0, sm.Skewness());
            Assert.Greater(-1.19, sm.Kurtosis());
            Assert.Less(-1.2, sm.Kurtosis());
        }

        #endregion

    }


    /// <summary>
    /// Test wrapped streams
    /// </summary>
    [TestFixture]
    public class BaseWrappedStreamTests
    {
        MemoryStream source;
        WrappedStream ws;
        uint sourceSize = 30;
        uint wrappedStartPosition = 10;
        uint wrappedSize = 10;

        [SetUp]
        public void SetUp()
        {
            source = new MemoryStream(30);
            for (int i = 0; i < source.Capacity; i++) { source.WriteByte(0x55); }
            source.Position = wrappedStartPosition;
            ws = new WrappedStream(source, wrappedSize);
        }

        public void VerifyBounds()
        {
            Assert.AreEqual(sourceSize, source.Capacity);
            Assert.AreEqual(sourceSize, source.Length);
            byte[] bytes = source.ToArray();
            for (uint i = 0; i < wrappedStartPosition; i++)
            {
                Assert.AreEqual(0x55, bytes[i]);
            }
            for (uint i = wrappedStartPosition + wrappedSize; i < bytes.Length; i++)
            {
                Assert.AreEqual(0x55, bytes[i]);
            }
        }

        [Test]
        public void TestWritingCapacity()
        {
            Assert.AreEqual(0, ws.Position);
            for (int i = 0; i < wrappedSize; i++)
            {
                Assert.AreEqual(i, ws.Position);
                ws.WriteByte((byte)i);
                Assert.AreEqual(i + 1, ws.Position);
            }
            try
            {
                ws.WriteByte((byte)255);
                Assert.Fail("Wrote over end of wrapped stream!");
            }
            catch (ArgumentException)
            {
                /* should have thrown exception */
            }

            ws.Position = 0;    // rewind
            for (int i = 0; i < wrappedSize; i++)
            {
                Assert.AreEqual(i, ws.Position);
                Assert.AreEqual(i, ws.ReadByte());
                Assert.AreEqual(i + 1, ws.Position);
            }
            Assert.AreEqual(-1, ws.ReadByte());

            VerifyBounds();
        }

        [Test]
        public void TestBounds()
        {
            try
            {
                ws.Position = wrappedSize + 1;
                Assert.Fail("should have thrown an exception");
            }
            catch (ArgumentException) { }
            Assert.AreEqual(0, ws.Position);    // shouldn't have moved

            try
            {
                ws.Seek(-1, SeekOrigin.Begin);
                Assert.Fail("should have thrown an exception");
            }
            catch (ArgumentException) { }
            Assert.AreEqual(0, ws.Position);    // shouldn't have moved

            try
            {
                ws.Seek(1, SeekOrigin.End);
                Assert.Fail("should have thrown an exception");
            }
            catch (ArgumentException) { }
            Assert.AreEqual(0, ws.Position);    // shouldn't have moved

            try
            {
                ws.Seek(20, SeekOrigin.Current);
                Assert.Fail("should have thrown an exception");
            }
            catch (ArgumentException) { }
            Assert.AreEqual(0, ws.Position);    // shouldn't have moved

            VerifyBounds();
        }

        [Test]
        public void TestBounds2()
        {
            try
            {
                ws.Write(new byte[wrappedSize + 1], 0, (int)wrappedSize + 1);
                Assert.Fail("should have thrown an exception");
            }
            catch (ArgumentException) { }
            Assert.AreEqual(0, ws.Position);    // shouldn't have moved

            Assert.AreEqual(wrappedSize, ws.Read(new byte[wrappedSize + 1], 0, (int)wrappedSize + 1));
            Assert.AreEqual(wrappedSize, ws.Position);
        }

        [Test]
        public void TestBounds3()
        {
            try
            {
                ws.Write(new byte[wrappedSize], 0, (int)wrappedSize);
            }
            catch (Exception)
            {
                Assert.Fail("should not have thrown an exception");
            }
            Assert.AreEqual(wrappedSize, ws.Position);

            Assert.AreEqual(0, ws.Read(new byte[wrappedSize], 0, (int)wrappedSize));
            Assert.AreEqual(wrappedSize, ws.Position);
            VerifyBounds();
        }

        [Test]
        public void TestBounds4()
        {
            try
            {
                ws.Seek(0, SeekOrigin.Begin);
            }
            catch (ArgumentException)
            {
                Assert.Fail("should not have thrown an exception");
            }
            Assert.AreEqual(0, ws.Position);    // shouldn't have moved

            try
            {
                ws.Seek(0, SeekOrigin.End);
            }
            catch (ArgumentException)
            {
                Assert.Fail("should not have thrown an exception");
            }
            Assert.AreEqual(wrappedSize, ws.Position);
        }
    }

    [TestFixture]
    public class BaseProcessorQueueTests
    {
        [Test]
        public void TestAAASingleElementProcessor()
        {
            IProcessingQueue<object> processor = new SingleElementProcessor<object>(new object());
            Assert.IsFalse(processor.Empty);
            Assert.IsNotNull(processor.Current);
            processor.Remove();
            Assert.IsTrue(processor.Empty);
            Assert.IsNull(processor.Current);
        }

        [Test]
        public void TestAAARoundRobinProcessor()
        {
            Dictionary<int, Queue<object>> queues = new Dictionary<int, Queue<object>>();
            queues[0] = new Queue<object>();
            queues[0].Enqueue("this");
            queues[0].Enqueue("is");
            queues[0].Enqueue("a");
            queues[0].Enqueue("test");
            queues[0].Enqueue("of");
            queues[0].Enqueue("faith");
            queues[1] = new Queue<object>();
            queues[1].Enqueue(1);
            queues[1].Enqueue(2);
            queues[1].Enqueue(3);
            queues[1].Enqueue(4);
            queues[1].Enqueue(5);
            queues[1].Enqueue(6);

            RoundRobinProcessor<int, object> rrp = new RoundRobinProcessor<int, object>(queues);
            Assert.AreEqual(rrp.Current, rrp.Current);
            Assert.IsFalse(rrp.Empty);

            Type firstType = rrp.Current.GetType();
            Type currentType = firstType;
            rrp.Remove();
            Assert.IsFalse(rrp.Empty);
            int count = 1;
            while (!rrp.Empty)
            {
                Assert.IsNotNull(rrp.Current.GetType());
                Assert.AreNotEqual(currentType, rrp.Current.GetType());
                currentType = rrp.Current.GetType();
                rrp.Remove();
                count++;
            }
            Assert.AreEqual(12, count);
        }

        [Test]
        public void TestChainedProcessor()
        {
            IProcessingQueue<object> processor = new ProcessorChain<object>(
                new SingleElementProcessor<object>(new object()),
                new SingleElementProcessor<object>(new object()));
            Assert.IsFalse(processor.Empty);
            Assert.IsNotNull(processor.Current);
            processor.Remove();
            Assert.IsFalse(processor.Empty);
            Assert.IsNotNull(processor.Current);
            processor.Remove();
            Assert.IsTrue(processor.Empty);
            Assert.IsNull(processor.Current);
        }

        [Test]
        public void TestPredicateProcessor()
        {
            IList<object> list = new List<object>();
            list.Add("this is a test");
            list.Add(new Hashtable());
            list.Add("this is a test");
            list.Add(new Hashtable());
            list.Add("this is a test");
            list.Add(new Hashtable());
            list.Add("this is a test");
            IProcessingQueue<object> processor = new PredicateListProcessor<object>(list,
                delegate(object o) { return o is string; });
            Assert.IsFalse(processor.Empty);
            Assert.IsNotNull(processor.Current);
            Assert.IsInstanceOfType(typeof(string), processor.Current);
            processor.Remove();
            Assert.IsFalse(processor.Empty);
            Assert.IsNotNull(processor.Current);
            Assert.IsInstanceOfType(typeof(string), processor.Current);
            processor.Remove();
            Assert.IsFalse(processor.Empty);
            Assert.IsNotNull(processor.Current);
            Assert.IsInstanceOfType(typeof(string), processor.Current);
            processor.Remove();
            Assert.IsFalse(processor.Empty);
            Assert.IsNotNull(processor.Current);
            Assert.IsInstanceOfType(typeof(string), processor.Current);
            processor.Remove();
            Assert.IsTrue(processor.Empty);
            Assert.IsNull(processor.Current);

            Assert.AreEqual(3, list.Count);
        }


    }

    [TestFixture]
    public class BaseAssumptions
    {
        [Test]
        public void TestSeverityOrdering()
        {
            Assert.IsTrue(Severity.Fatal > Severity.Error);
            Assert.IsTrue(Severity.Error > Severity.Warning);
            Assert.IsTrue(Severity.Warning > Severity.Information);
        }
    }

    [TestFixture]
    public class BaseHPTimerTests
    {
        [Test]
        public void TestNoChangeOnUpdate()
        {
            HPTimer timer = new HPTimer();
            timer.Start();
            long ms = timer.TimeInMilliseconds;
            long ticks = timer.Ticks;
            // sleep for at least for 100 rounds of ticks
            Thread.Sleep((int)Math.Max(1, 100 * 1000 / timer.Frequency));
            Assert.AreEqual(ms, timer.TimeInMilliseconds);
            Assert.AreEqual(ticks, timer.Ticks);
        }

        [Test]
        public void TestChangesOnUpdate()
        {
            HPTimer timer = new HPTimer();
            timer.Start();
            long ms = timer.TimeInMilliseconds;
            long ticks = timer.Ticks;
            // sleep for at least for 100 rounds of ticks
            Thread.Sleep((int)Math.Max(5, 100 * 1000 / timer.Frequency));
            timer.Update();
            Assert.Less(ms, timer.TimeInMilliseconds);
            Assert.Less(ticks, timer.Ticks);
            Assert.Greater(timer.Frequency / (100 * 1000), timer.ElapsedInMilliseconds);
        }
    }

    [TestFixture]
    public class BaseByteUtilsTests
    {

        #region Adaptive Length Encoding tests
        #region Helper methods
        protected void CheckEncoding(int value, byte[] expectedResult)
        {
            MemoryStream ms = new MemoryStream();
            Assert.AreEqual(0, ms.Length);
            ByteUtils.EncodeLength(value, ms);
            ms.Position = 0;
            Assert.AreEqual(expectedResult.Length, ms.Length);
            Assert.AreEqual(expectedResult, ms.ToArray());
            Assert.AreEqual(expectedResult, ByteUtils.EncodeLength(value));
            Assert.AreEqual(value, ByteUtils.DecodeLength(expectedResult));
            Assert.AreEqual(value, ByteUtils.DecodeLength(ms));
            int index = 0;
            Assert.AreEqual(value, ByteUtils.DecodeLength(ms.ToArray(), ref index));
            Assert.AreEqual(ms.Length, index);
            Assert.IsTrue(ms.Position == ms.Length);    // check that no turds left on the stream
        }
        #endregion
        #region Tests
        [Test]
        public void CheckLengthEncodingBoundaries()
        {
            CheckEncoding(0, new byte[] { 0 });
            CheckEncoding(63, new byte[] { 63 });
            CheckEncoding(64, new byte[] { 64, 64 });
            CheckEncoding(16383, new byte[] { 127, 255 });
            CheckEncoding(16384, new byte[] { 128, 64, 0 });
            CheckEncoding(4194303, new byte[] { 191, 255, 255 });
            CheckEncoding(4194304, new byte[] { 192, 64, 0, 0 });
            CheckEncoding(1073741823, new byte[] { 255, 255, 255, 255 });

            // bit pattern: 0b10101010 0b10101010 0b10101010
            CheckEncoding(2796202, new byte[] { 170, 170, 170 });
            try
            {
                CheckEncoding(1073741824, new byte[] { 0, 0, 0, 0, 0 });
                Assert.Fail("Encoding should have raised exception: too great to be encoded");
            }
            catch (Exception) { /* success */ }
        }

        [Test]
        public void CheckLengthEncodingPatterns()
        {
            // bit pattern: 0b10101010 0b10101010 0b10101010
            CheckEncoding(2796202, new byte[] { 170, 170, 170 });

            // bit pattern: 0b10101010 0b01010101 0b10101010
            CheckEncoding(2796202, new byte[] { 170, 170, 170 });

            // bit pattern: 0b11110000 0b11001100 0b00110011 0b00001111
            CheckEncoding(818688783, new byte[] { 240, 204, 51, 15 });

        }

        [Test]
        public void TestEOF()
        {
            MemoryStream ms = new MemoryStream(new byte[] { 64 });
            try
            {
                ByteUtils.DecodeLength(ms);
                Assert.Fail("Should throw exception on EOF");
            }
            catch (InvalidDataException) { }
        }


        #endregion
        #endregion

        #region Encoded String-String Dictionary Tests

        [Test]
        public void TestDictionaryEncoding()
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            dict["foo"] = "bar";
            dict["nog"] = Guid.NewGuid().ToString("N");

            // # bytes
            // 1    1 byte to represent 2 keys
            // 4    'foo': 1 byte for length + 3 bytes for key 'foo'
            // 4    'bar': 1 byte for length + 3 bytes for value 'bar'
            // 4    'nog': 1 byte for length + 3 bytes for key 'nog'
            // 33   guid: 1 byte for length + 32 bytes for guid ("N" is most compact)
            // 46 bytes total
            MemoryStream ms = new MemoryStream();
            ByteUtils.EncodeDictionary(dict, ms);
            Assert.AreEqual(46, ms.Length);
            Assert.AreEqual(46, ByteUtils.EncodedDictionaryByteCount(dict));

            ms.Position = 0;
            Dictionary<string, string> decoded = ByteUtils.DecodeDictionary(ms);
            Assert.AreEqual(dict, decoded);
        }
        #endregion

    }

    [TestFixture]
    public class BitUtilsTests
    {
        [Test]
        public void TestIsPowerOf2() {
            for (int i = 0; i < 32; i++)
            {
                Assert.IsTrue(BitUtils.IsPowerOf2(1u << i));
            }
            Assert.IsFalse(BitUtils.IsPowerOf2(3));
            Assert.IsFalse(BitUtils.IsPowerOf2(100));
            Assert.IsFalse(BitUtils.IsPowerOf2(255));
        }

        [Test]
        public void TestRoundUpToPowerOf2()
        {
            for (int i = 0; i < 32; i++)
            {
                Assert.IsTrue(BitUtils.IsPowerOf2(BitUtils.RoundUpToPowerOf2(1u << i)));
                if(!BitUtils.IsPowerOf2((1u << i) - 1)) 
                {
                    Assert.AreEqual(1u << i, BitUtils.RoundUpToPowerOf2((1u << i) - 1));
                }
            }
        }

        [Test]
        public void TestHighestBitSet() {
            Assert.AreEqual(-1, BitUtils.HighestBitSet((byte)0));
            Assert.AreEqual(-1, BitUtils.HighestBitSet((uint)0));
            for (int i = 0; i < 32; i++)
            {
                Assert.AreEqual(i, BitUtils.HighestBitSet(1u << i));
                if (i < 8)
                {
                    Assert.AreEqual(i, BitUtils.HighestBitSet((byte)(1u << i)));
                }
                if (i > 0)
                {
                    Assert.AreEqual(i - 1, BitUtils.HighestBitSet((1u << i) - 1));
                }
            }
        }
    }

    [TestFixture]
    public class BaseBitTupleTests
    {
        bool[] bits = new bool[] { 
                true, true, false, false, true, true, false, false,
                false, false, true, true, false, false, true, true, 
                false, false, false, false, true, true, true, true,
                true, true, true, true, false, false, false, false, 
                true, false, false, true
            };

        [Test]
        public void Test00Indexer()
        {
            BitTuple bt = new BitTuple(8);
            Assert.AreEqual(8, bt.Length);
            for (int i = 0; i < 8; i++) { Assert.AreEqual(false, bt[i]); }
            for (int i = 0; i < 8; i++)
            {
                bt[i] = (i % 2) == 0;
                Assert.AreEqual((i % 2) == 0, bt[i]);
            }
        }

        [Test]
        public void Test1AddAll()
        {
            BitTuple bt = new BitTuple(bits);
            bt.AddAll(bits);
            Assert.AreEqual(2 * bits.Length, bt.Length);
            for (int i = 0; i < bt.Length; i++) { Assert.AreEqual(bits[i % bits.Length], bt[i]); }
        }

        [Test]
        public void TestIndexerBounds()
        {
            BitTuple bt = new BitTuple(8);
            Assert.AreEqual(8, bt.Length);
            try
            {
                bool t = bt[9];
                Assert.Fail("should have thrown ArgumentException");
            }
            catch (ArgumentException) { /* ignore */ }

            try
            {
                bool t = bt[-1];
                Assert.Fail("should have thrown ArgumentException");
            }
            catch (ArgumentException) { /* ignore */ }
        }

        [Test]
        public void TestToArray()
        {
            BitTuple bt = new BitTuple(8);
            Assert.AreEqual(8, bt.Length);
            for (int i = 0; i < 8; i++) { Assert.AreEqual(false, bt[i]); }
            byte[] bytes = bt.ToArray();
            Assert.AreEqual(1, bytes.Length);
            Assert.AreEqual(0, bytes[0]);

            bt.Add(true);
            Assert.AreEqual(9, bt.Length);
            for (int i = 0; i < 8; i++) { Assert.AreEqual(false, bt[i]); }
            Assert.AreEqual(true, bt[8]);
            bytes = bt.ToArray();
            Assert.AreEqual(2, bytes.Length);
            Assert.AreEqual(0, bytes[0]);
            Assert.AreEqual(1, bytes[1]);

            try
            {
                bool t = bt[9];
                Assert.Fail("should have thrown ArgumentException");
            }
            catch (ArgumentException) { /* ignore */ }
        }

        [Test]
        public void TestToArray2()
        {
            BitTuple bt = new BitTuple(bits);
            Assert.AreEqual(bits.Length, bt.Length);

            bt = new BitTuple(bt.ToArray(), bt.Length);
            Assert.AreEqual(bits.Length, bt.Length);
            for (int i = 0; i < bits.Length; i++) { Assert.AreEqual(bits[i], bt[i]); }
            bt = new BitTuple(bt.ToArray());
            Assert.LessOrEqual(bits.Length, bt.Length);
            for (int i = 0; i < bits.Length; i++) { Assert.AreEqual(bits[i], bt[i]); }
        }

        [Test]
        public void TestSubset()
        {
            BitTuple bt = new BitTuple(bits, 8);
            Assert.AreEqual(8, bt.Length);
            for (int i = 0; i < bt.Length; i++) { Assert.AreEqual(bits[i], bt[i]); }
        }

        [Test]
        public void TestToBitArray()
        {
            BitTuple bt = new BitTuple(0);
            bt.AddAll(bits);
            Assert.AreEqual(bits.Length, bt.Length);
            for (int i = 0; i < bits.Length; i++) { Assert.AreEqual(bits[i], bt[i]); }

            BitArray t = bt.ToBitArray();
            for (int i = 0; i < bits.Length; i++) { Assert.AreEqual(bt[i], t[i]); }

            t = new BitArray(bt.ToArray());
            for (int i = 0; i < bits.Length; i++) { Assert.AreEqual(bt[i], t[i]); }

            t = new BitArray(bits);
            bt = new BitTuple(t);
            for (int i = 0; i < bits.Length; i++) { Assert.AreEqual(bt[i], t[i]); }
        }
    }

    /// <summary>
    ///  Utility class for marshaller tests
    /// </summary>
    public class MarshallerTestsHelper
    {
        protected void AssertAreEquivalent(Message original, Message created)
        {
            Assert.AreEqual(original.GetType(), created.GetType());
            Assert.AreEqual(original.Channel, created.Channel);
            Assert.AreEqual(original.MessageType, created.MessageType);
            switch (original.MessageType)
            {
                case MessageType.Binary:
                    Assert.AreEqual(((BinaryMessage)original).Bytes, ((BinaryMessage)created).Bytes);
                    return;
                case MessageType.Object:
                    Assert.AreEqual(((ObjectMessage)original).Object, ((ObjectMessage)created).Object);
                    return;
                case MessageType.String:
                    Assert.AreEqual(((StringMessage)original).Text, ((StringMessage)created).Text);
                    return;

                case MessageType.System:
                    Assert.AreEqual(((SystemMessage)original).data, ((SystemMessage)created).data);
                    return;

                case MessageType.Session:
                case MessageType.Tuple1D:
                case MessageType.Tuple2D:
                case MessageType.Tuple3D:
                    throw new NotImplementedException();
            }
        }

        public byte[] RandomBytes(int len)
        {
            byte[] bytes = new byte[len];
            for (int i = 0; i < len; i++)
            {
                bytes[i] = (byte)i;
            }
            return bytes;
        }

        public void CheckMarshaller(IMarshaller m)
        {
            Message[] messages = new Message[] {
                new BinaryMessage(0, RandomBytes(0)),
                new BinaryMessage(0, RandomBytes(10)),
                new BinaryMessage(0, RandomBytes(100)),
                new ObjectMessage(33, new List<object>()),
                new ObjectMessage(5, new List<int>()),
                new ObjectMessage(2, new Dictionary<byte,object>()),
                new StringMessage(156, "this is a test of faith"),
                new StringMessage(53, "เ, แ, โ, ใ, ไ"), 
                new StringMessage(24, "\u005C\uFF5E\u301C"),
                new SystemMessage(SystemMessageType.IdentityRequest, new byte[4])
            };
            foreach (Message original in messages)
            {
                bool seen = false;
                MarshalledResult mr = m.Marshal(0, original, new NullTransport());
                while (mr.HasPackets)
                {
                    m.Unmarshal(mr.RemovePacket(), new NullTransport(),
                        (sender, mea) => {
                            seen = true;
                            AssertAreEquivalent(original, mea.Message);
                        });
                }
                Assert.IsTrue(seen, "Marshaller has not reported a message available");
            }
        }
    }

    /// <remarks>Test basic marshalling functionality</remarks>
    [TestFixture]
    public class BaseDotNetSerializingMarshallerTests
    {
        #region DotNetSerializingMarshaller tests
        [Test]
        public void TestMarshaller()
        {
            new MarshallerTestsHelper().CheckMarshaller(new DotNetSerializingMarshaller());
        }



        #endregion

        #region Lightweight Marshalling Tests
        [Test]
        public void TestRawMessage()
        {
            IMarshaller m = new DotNetSerializingMarshaller();
            string s = "this is a test of faith";
            MarshalledResult mr = m.Marshal(0, new StringMessage(0, s), new NullTransport());
            Assert.AreEqual(1, mr.Packets.Count);
            byte[] originalBytes = mr.Packets[0].ToArray();

            IMarshaller rawm = new LightweightDotNetSerializingMarshaller();
            Message msg = null;
            while(mr.HasPackets)
            {
                Assert.IsNull(msg); // should only oop out on the last packet
                rawm.Unmarshal(mr.RemovePacket(), new NullTransport(),
                    (sender, mea) => msg = mea.Message);
            }
            Assert.IsInstanceOfType(typeof(LightweightDotNetSerializingMarshaller.RawMessage), msg);
            Assert.AreEqual(0, msg.Channel);
            Assert.AreEqual(MessageType.String, msg.MessageType);

            MarshalledResult mr2 = m.Marshal(0, msg, new NullTransport());
            Assert.AreEqual(originalBytes, mr2.Packets[0].ToArray());

            msg = null;
            while (mr2.HasPackets)
            {
                Assert.IsNull(msg);
                m.Unmarshal(mr2.RemovePacket(), new NullTransport(),
                    (sender, mea) => msg = mea.Message);
            }
            Assert.IsInstanceOfType(typeof(StringMessage), msg);
            Assert.AreEqual(0, msg.Channel);
            Assert.AreEqual(MessageType.String, msg.MessageType);
            Assert.AreEqual(s, ((StringMessage)msg).Text);
        }

        #endregion
    }

    [TestFixture]
    public class BaseGetOptTests
    {
        [Test]
        public void TestBasic()
        {
            GetOpt getopt = new GetOpt(new[] { "-a", "-b", "-c", "c-arg", "rest" }, "abc:");

            Option opt;

            opt = getopt.NextOption();
            Assert.IsNotNull(opt, "should have had a valid option");
            Assert.AreEqual('a', opt.Character);
            Assert.IsNull(opt.Argument, "doesn't have an argument");

            opt = getopt.NextOption();
            Assert.IsNotNull(opt, "should have had a valid option");
            Assert.AreEqual('b', opt.Character);
            Assert.IsNull(opt.Argument, "doesn't have an argument");

            opt = getopt.NextOption();
            Assert.IsNotNull(opt, "should have had a valid option");
            Assert.AreEqual('c', opt.Character);
            Assert.IsNotNull(opt.Argument, "should have an argument");
            Assert.AreEqual("c-arg", opt.Argument, "argument isn't quite right!");

            opt = getopt.NextOption();
            Assert.IsNull(opt, "should have no more options");

            string[] remainder = getopt.RemainingArguments();
            Assert.IsNotNull(remainder);
            Assert.AreEqual(1, remainder.Length);
            Assert.AreEqual("rest", remainder[0]);
        }

        [Test]
        public void TestStackedOptions()
        {
            GetOpt getopt = new GetOpt(new[] { "-abab", "-ab", "rest" }, "abc:");

            Option opt;

            for (int i = 0; i < 3; i++)
            {
                opt = getopt.NextOption();
                Assert.IsNotNull(opt, "should have had a valid option");
                Assert.AreEqual('a', opt.Character);
                Assert.IsNull(opt.Argument, "doesn't have an argument");

                opt = getopt.NextOption();
                Assert.IsNotNull(opt, "should have had a valid option");
                Assert.AreEqual('b', opt.Character);
                Assert.IsNull(opt.Argument, "doesn't have an argument");
            }

            opt = getopt.NextOption();
            Assert.IsNull(opt, "should have no more options");

            string[] remainder = getopt.RemainingArguments();
            Assert.IsNotNull(remainder);
            Assert.AreEqual(1, remainder.Length);
            Assert.AreEqual("rest", remainder[0]);
        }

        [Test]
        public void TestStackedOptionsWithArgs()
        {
            GetOpt getopt = new GetOpt(new[] { "-abcc-arg", "-c", "c-arg", "-cc-arg", "rest" }, "abc:");
            Option opt;

            opt = getopt.NextOption();
            Assert.IsNotNull(opt, "should have had a valid option");
            Assert.AreEqual('a', opt.Character);
            Assert.IsNull(opt.Argument, "doesn't have an argument");

            opt = getopt.NextOption();
            Assert.IsNotNull(opt, "should have had a valid option");
            Assert.AreEqual('b', opt.Character);
            Assert.IsNull(opt.Argument, "doesn't have an argument");

            for (int i = 0; i < 3; i++)
            {
                opt = getopt.NextOption();
                Assert.IsNotNull(opt, "should have had a valid option");
                Assert.AreEqual('c', opt.Character);
                Assert.IsNotNull(opt.Argument, "should have an argument");
                Assert.AreEqual("c-arg", opt.Argument, "argument isn't quite right!");
            }

            opt = getopt.NextOption();
            Assert.IsNull(opt, "should have no more options");

            string[] remainder = getopt.RemainingArguments();
            Assert.IsNotNull(remainder);
            Assert.AreEqual(1, remainder.Length);
            Assert.AreEqual("rest", remainder[0]);
        }

        [Test]
        public void TestZZMissingArgument()
        {
            GetOpt getopt = new GetOpt(new[] { "-abcc-arg", "-c" }, "abc:");
            Option opt;

            opt = getopt.NextOption();
            Assert.IsNotNull(opt, "should have had a valid option");
            Assert.AreEqual('a', opt.Character);
            Assert.IsNull(opt.Argument, "doesn't have an argument");

            opt = getopt.NextOption();
            Assert.IsNotNull(opt, "should have had a valid option");
            Assert.AreEqual('b', opt.Character);
            Assert.IsNull(opt.Argument, "doesn't have an argument");

            opt = getopt.NextOption();
            Assert.IsNotNull(opt, "should have had a valid option");
            Assert.AreEqual('c', opt.Character);
            Assert.IsNotNull(opt.Argument, "should have an argument");
            Assert.AreEqual("c-arg", opt.Argument, "argument isn't quite right!");

            try
            {
                opt = getopt.NextOption();
                Assert.Fail("Should have thrown " + typeof(MissingOptionException));
            }
            catch (MissingOptionException)
            {
                /* ignore */
            }
        }
    }

    [TestFixture]
    public class BaseObjectPoolTests
    {
        [Test]
        public void TestBasicsSimple()
        {
            List<object> allocated = new List<object>();
            int rehabed = 0;
            int destroyed = 0;
            Pool<object> op = new Pool<object>(1, 3,
                () => new object(), o => rehabed++, o => destroyed++);
            Assert.AreEqual(0, op.Count, "non-strict pools do not have to meet low-water");
            Assert.IsTrue(op.Available);

            allocated.Add(op.Obtain());
            Assert.IsTrue(op.Available);
            Assert.AreEqual(1, op.Count);

            allocated.Add(op.Obtain());
            Assert.IsTrue(op.Available);
            Assert.AreEqual(2, op.Count);

            allocated.Add(op.Obtain());
            Assert.IsTrue(op.Available);
            Assert.AreEqual(3, op.Count);

            allocated.Add(op.Obtain());
            Assert.IsTrue(op.Available);
            Assert.AreEqual(4, op.Count);

            foreach (object o in allocated)
            {
                op.Return(o);
            }
            Assert.AreEqual(3, rehabed);
            Assert.AreEqual(1, destroyed);
            Assert.AreEqual(3, op.Count);

            op.Dispose();
            Assert.AreEqual(4, destroyed);
            Assert.AreEqual(0, op.Count);
        }

        [Test]
        public void TestBasicsManaged()
        {
            List<object> allocated = new List<object>();
            int rehabed = 0;
            int destroyed = 0;
            Pool<object> op = new ManagedPool<object>(1, 3,
                () => new object(), o => rehabed++, o => destroyed++);
            Assert.AreEqual(0, op.Count, "non-strict pools do not have to meet low-water");
            Assert.IsTrue(op.Available);

            allocated.Add(op.Obtain());
            Assert.IsTrue(op.Available);
            Assert.AreEqual(1, op.Count);

            allocated.Add(op.Obtain());
            Assert.IsTrue(op.Available);
            Assert.AreEqual(2, op.Count);

            allocated.Add(op.Obtain());
            Assert.IsTrue(op.Available);
            Assert.AreEqual(3, op.Count);

            allocated.Add(op.Obtain());
            Assert.IsTrue(op.Available);
            Assert.AreEqual(4, op.Count);

            foreach (object o in allocated)
            {
                op.Return(o);
            }
            Assert.AreEqual(op.High, op.Count);
            Assert.AreEqual(0, op.Out);
            Assert.AreEqual(3, rehabed);
            Assert.AreEqual(1, destroyed);

            try
            {
                op.Return(new object());
                Assert.Fail("should have thrown ArgumentException");
            }
            catch (ArgumentException) { /* ignore */ }
            op.Dispose();
            Assert.AreEqual(4, destroyed);
            Assert.AreEqual(0, op.Out);
            Assert.AreEqual(0, op.Count);
        }

        [Test]
        public void TestBasicsStrict()
        {
            List<object> allocated = new List<object>();
            int created = 0;
            int rehabed = 0;
            int destroyed = 0;
            Pool<object> op = new StrictPool<object>(2, 3,
                delegate() { created++; return new object(); },
                o => rehabed++, o => destroyed++);
            Assert.AreEqual(2, op.Count, "strict pools should immediately meet low-water");
            Assert.AreEqual(2, created);
            Assert.IsTrue(op.Available);

            allocated.Add(op.Obtain());
            Assert.IsTrue(op.Available);
            Assert.AreEqual(2, op.Count);
            Assert.AreEqual(2, created);

            allocated.Add(op.Obtain());
            Assert.IsTrue(op.Available);
            Assert.AreEqual(2, op.Count);
            Assert.AreEqual(2, created);

            allocated.Add(op.Obtain());
            Assert.IsFalse(op.Available);
            Assert.AreEqual(3, op.Count);
            Assert.AreEqual(3, created);

            Assert.IsNull(op.TryObtain());

            object obtained = null;
            Thread obtainer = new Thread(new ThreadStart(delegate { obtained = op.Obtain(); }));
            obtainer.IsBackground = true;
            obtainer.Start();
            Thread.Sleep(200);     // ensure obtainer has a chance to start and block
            Assert.IsTrue(obtainer.IsAlive);
            Assert.IsNull(obtained);
            Assert.IsFalse(obtainer.ThreadState == ThreadState.Running);
            Assert.AreEqual(3, created, "shouldn't have created a new object");

            Assert.IsFalse(op.Available);
            op.Return(allocated[0]);
            allocated.RemoveAt(0);
            Assert.AreEqual(3, created, "shouldn't have created a new object");
            Assert.AreEqual(1, rehabed);

            obtainer.Join(500);     // ensure obtainer has a chance to start and block
            Assert.IsFalse(obtainer.IsAlive);
            Assert.IsNotNull(obtained); ;
            Assert.AreEqual(3, created, "shouldn't have created a new object");

            op.Dispose();
            Assert.AreEqual(3, destroyed);
            Assert.AreEqual(0, op.Out);
            Assert.AreEqual(0, op.Count);
        }

        [Test]
        public void TestStrictAndRuined()
        {
            int created = 0;
            int rehabed = 0;
            int destroyed = 0;
            Pool<object> op = new StrictPool<object>(1, 1,
                delegate()
                {
                    created++;
                    return new object();
                }, o => rehabed++, o => destroyed++);
            Assert.AreEqual(1, op.Count, "strict pools should immediately meet low-water");
            Assert.AreEqual(1, created);
            Assert.IsTrue(op.Available);

            object allocated = op.Obtain();
            Assert.IsNotNull(allocated);

            Assert.IsFalse(op.Available);
            Assert.IsNull(op.TryObtain());

            object obtained = null;
            // ensure Ruined works too
            Thread obtainer = new Thread(new ThreadStart(delegate { obtained = op.Obtain(); }));
            obtainer.IsBackground = true;
            obtainer.Start();
            Thread.Sleep(200);     // ensure obtainer has a chance to start and block
            Assert.IsNull(obtained);
            Assert.IsTrue(obtainer.IsAlive);
            Assert.IsFalse(obtainer.ThreadState == ThreadState.Running);

            Assert.IsFalse(op.Available);
            Assert.AreEqual(0, rehabed);
            Assert.AreEqual(0, destroyed);
            op.Ruined(allocated);
            Assert.AreEqual(2, created, "returning a ruined object should create a new one");
            Assert.AreEqual(0, rehabed);
            Assert.AreEqual(1, destroyed);

            // ensure obtainer has a chance to start and block
            obtainer.Join(500);     // ensure obtainer has a chance to start and block
            Assert.IsFalse(obtainer.IsAlive, "waiting thread should be dead");
            Assert.IsNotNull(obtained);
            Assert.IsFalse(allocated == obtained, "should not be the same object");

            op.Dispose();
            Assert.AreEqual(0, rehabed);
            Assert.AreEqual(2, destroyed);
            Assert.AreEqual(0, op.Count);
        }

    }
}