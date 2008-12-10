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
            catch (Exception)
            {
                Assert.Fail("CopyTo() shouldn't touch parameter array if nothing to copy");
            }
        }

        [Test]
        public void TestAddAndRemove()
        {
            Bag<string> b = new Bag<string>();
            Assert.AreEqual(0, b.Count);
            Assert.IsFalse(b.Contains("foo"));
            Assert.IsFalse(b.Contains("bar"));
            Assert.AreEqual(0, b.Occurrences("foo"));
            Assert.AreEqual(0, b.Occurrences("bar"));

            b.Add("foo");
            Assert.IsTrue(b.Contains("foo"));
            Assert.AreEqual(1, b.Count);
            Assert.AreEqual(1, b.Occurrences("foo"));
            Assert.AreEqual(0, b.Occurrences("bar"));

            b.Add("bar");
            Assert.IsTrue(b.Contains("foo"));
            Assert.IsTrue(b.Contains("bar"));
            Assert.AreEqual(2, b.Count);
            Assert.AreEqual(1, b.Occurrences("foo"));
            Assert.AreEqual(1, b.Occurrences("bar"));

            b.Add("foo");
            Assert.AreEqual(3, b.Count);
            Assert.IsTrue(b.Contains("foo"));
            Assert.IsTrue(b.Contains("bar"));
            Assert.IsFalse(b.Contains("baz"));
            Assert.AreEqual(2, b.Occurrences("foo"));
            Assert.AreEqual(1, b.Occurrences("bar"));

            Assert.IsTrue(b.Remove("foo"));
            Assert.AreEqual(2, b.Count);
            Assert.IsTrue(b.Contains("foo"));
            Assert.AreEqual(1, b.Occurrences("foo"));
            Assert.AreEqual(1, b.Occurrences("bar"));

            Assert.IsTrue(b.Remove("foo"));
            Assert.AreEqual(1, b.Count);
            Assert.IsFalse(b.Contains("foo"));
            Assert.AreEqual(0, b.Occurrences("foo"));
            Assert.AreEqual(1, b.Occurrences("bar"));

            Assert.IsFalse(b.Remove("foo"));
            Assert.AreEqual(1, b.Count);
            Assert.IsFalse(b.Contains("foo"));
            Assert.AreEqual(0, b.Occurrences("foo"));
            Assert.AreEqual(1, b.Occurrences("bar"));

            b.Clear();
            Assert.AreEqual(0, b.Count);
            Assert.IsFalse(b.Contains("foo"));
            Assert.AreEqual(0, b.Occurrences("foo"));
            Assert.AreEqual(0, b.Occurrences("bar"));
        }

        [Test]
        public void TestCopyTo()
        {
            Bag<string> b = new Bag<string>();
            try { b.CopyTo(new string[0], 0); }
            catch (Exception) { Assert.Fail("shouldn't access array unnecessarily"); }

            b.Add("foo");
            b.Add("foo");
            Assert.AreEqual(2, b.Count);
            try
            {
                b.CopyTo(new string[1], 0);
                Assert.Fail("shouldn't go over array length");
            }
            catch (ArgumentException) { /* expected result */ }
            
            string[] values = new string[2];
            try
            {
                b.CopyTo(values, 0);
                Assert.AreEqual("foo", values[0]);
                Assert.AreEqual("foo", values[1]);
            }
            catch (ArgumentException)
            {
                Assert.Fail("shouldn't go over array length");
            }

            values = new string[] { "", "", "" };
            try 
            {
                b.CopyTo(values, 0);
                Assert.AreEqual("foo", values[0]);
                Assert.AreEqual("foo", values[1]);
                Assert.AreEqual("", values[2]);
            }
            catch (ArgumentException)
            {
                Assert.Fail("shouldn't go over array length");
            }
        }

        [Test]
        public void TestCopyToOOBI() {
            Bag<string> b = new Bag<string>();
            b.Add("foo");
            try { b.CopyTo(new string[0], 0); }
            catch (ArgumentException) { return; }
            Assert.Fail(); 
        }

        [Test]
        public void TestIteration()
        {
            Bag<string> b = new Bag<string>();
            b.Add("foo");
            b.Add("foo");
            b.Add("foo");
            int count = 0;
            foreach (string s in b)
            {
                count++;
                Assert.AreEqual("foo", s);
            }
            Assert.AreEqual(3, count);
        }

    }

    [TestFixture]
    public class SingleItemTests
    {
        [Test]
        public void TestNewSingleItem()
        {
            SingleItem<string> b = new SingleItem<string>("foo");
            Assert.AreEqual(1, b.Count);
            Assert.IsTrue(b.IsReadOnly);
            Assert.AreEqual(1, b.Count);
            Assert.IsTrue(b.Contains("foo"));
            Assert.IsFalse(b.Contains("bar"));
            try
            {
                b.Remove("foo");
                Assert.Fail("SingleItem does not allow the item to be removed");
            } catch (NotSupportedException) { /* expected result */ }
            try
            {
                b.CopyTo(new string[0], 0);
                Assert.Fail("CopyTo() shouldn't copy to an invalid arrayIndex");
            }
            catch (ArgumentException) { /* expected result */ }
        }

        [Test]
        public void TestIteration()
        {
            SingleItem<string> b = new SingleItem<string>("foo");
            int count = 0;
            foreach (string s in b)
            {
                count++;
                Assert.AreEqual("foo", s);
            }
            Assert.AreEqual(1, count);
        }
    }

    [TestFixture]
    public class AASequentialSetTests
    {
        [Test]
        public void TestContains()
        {
            SequentialSet<int> set = new SequentialSet<int>();
            set.Add(0);
            set.Add(1);
            set.Add(2);
            Assert.IsTrue(set.Contains(0));
            Assert.IsTrue(set.Contains(1));
            Assert.IsTrue(set.Contains(2));
            Assert.IsFalse(set.Contains(-1));
            Assert.IsFalse(set.Contains(3));
        }

        [Test]
        public void TestOrderOfAdds()
        {
            SequentialSet<int> set = new SequentialSet<int>();
            set.Add(0);
            set.Add(1);
            set.Add(2);
            int counter = 0;
            Assert.AreEqual(3, set.Count);
            foreach (int next in set)
            {
                Assert.AreEqual(counter, set[counter]);
                Assert.AreEqual(counter++, next);
            } 
            Assert.AreEqual(3, counter);
            try
            {
                int n = set[3];
                Assert.Fail("Should have thrown out-of-range exception");
            }
            catch (ArgumentOutOfRangeException)
            {
                /* ignore: index was out of range */
            }
        }

        [Test]
        public void TestOrderOfConstructorAdds()
        {
            SequentialSet<int> set = new SequentialSet<int>(new[] { 0, 1, 2});
            int counter = 0;
            Assert.AreEqual(3, set.Count);
            foreach (int next in set)
            {
                Assert.AreEqual(counter, set[counter]);
                Assert.AreEqual(counter++, next);
            }
            Assert.AreEqual(3, counter);
            try
            {
                int n = set[3];
                Assert.Fail("Should have thrown out-of-range exception");
            }
            catch (ArgumentOutOfRangeException)
            {
                /* ignore: index was out of range */
            }
            Assert.AreEqual(3, counter);
        }

        [Test]
        public void TestRemoves()
        {
            SequentialSet<int> set = new SequentialSet<int>();
            set.Add(0);
            set.Add(1);
            set.Add(2);
            Assert.AreEqual(3, set.Count);
            Assert.IsTrue(set.Remove(1), "element should be present");
            Assert.AreEqual(2, set.Count);
            Assert.IsTrue(set[0] == 0);
            Assert.IsTrue(set[1] == 2);
        }

        [Test]
        public void TestAddAll()
        {
            SequentialSet<int> set = new SequentialSet<int>(new[] {0, 1});
            Assert.AreEqual(2, set.Count);
            set.AddAll(new[] {0, 1, 2});
            Assert.AreEqual(3, set.Count);
            Assert.IsTrue(set[0] == 0);
            Assert.IsTrue(set[1] == 1);
            Assert.IsTrue(set[2] == 2);
        }
    }

}
