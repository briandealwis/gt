using System;
using NUnit.Framework;
using GT.Net;
using GT.Utils;

namespace GT.UnitTests
{
    [TestFixture]
    public class BagTests
    {
        [Test]
        public void TestNewBag()
        {
            Bag<string> b = new Bag<string>();
            Assert.AreEqual(0, b.Count);
            Assert.IsFalse(b.IsReadOnly);
            Assert.AreEqual(0, b.Count);
            Assert.IsFalse(b.Remove("foo"));
            Assert.IsFalse(b.Contains("bar"));
            try
            {
                b.CopyTo(new string[0], 0);
            }
            catch (Exception e)
            {
                Assert.Fail("CopyTo() shouldn't touch parameter array if nothing to copy");
            }
        }

        [Test]
        public void TestAddAndRemove()
        {
            Bag<string> b = new Bag<string>();
            Assert.AreEqual(0, b.Count);
            b.Add("foo");
            Assert.AreEqual(1, b.Count);
            b.Add("bar");
            Assert.AreEqual(2, b.Count);
            b.Add("foo");
            Assert.AreEqual(3, b.Count);
            Assert.IsTrue(b.Contains("foo"));
            Assert.IsTrue(b.Contains("bar"));
            Assert.IsFalse(b.Contains("baz"));

            Assert.IsTrue(b.Remove("foo"));
            Assert.AreEqual(2, b.Count);
            Assert.IsTrue(b.Contains("foo"));

            Assert.IsTrue(b.Remove("foo"));
            Assert.AreEqual(1, b.Count);
            Assert.IsFalse(b.Contains("foo"));

            Assert.IsFalse(b.Remove("foo"));
            Assert.AreEqual(1, b.Count);
            Assert.IsFalse(b.Contains("foo"));

            b.Clear();
            Assert.AreEqual(0, b.Count);
            Assert.IsFalse(b.Contains("foo"));
        }

        [Test]
        public void TestCopyTo()
        {
            Bag<string> b = new Bag<string>();
            try { b.CopyTo(new string[0], 0); }
            catch (Exception) { Assert.Fail("shouldn't access array unnecessarily"); }

            b.Add("foo");
                string[] values = new string[1];
            try { b.CopyTo(values, 0); }
            catch (Exception) { Assert.Fail("shouldn't go over array length"); }
            Assert.AreEqual("foo", values[0]);
        }

        [Test]
        public void TestCopyToOOBI() {
            Bag<string> b = new Bag<string>();
            b.Add("foo");
            try { b.CopyTo(new string[0], 0); }
            catch (IndexOutOfRangeException) { return; }
            Assert.Fail(); 
        }
    }
}
