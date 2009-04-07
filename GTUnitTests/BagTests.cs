using System;
using System.Collections.Generic;
using GT.Weak;
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

            Assert.IsFalse(b.Remove("bar"));
            Assert.AreEqual(1, b.Count);

            try
            {
                b.Add("bar");
                Assert.Fail("SingleItem can only hold a single item");
            }
            catch (NotSupportedException)
            {
            }

            try
            {
                b.Remove("foo");
            }
            catch(NotSupportedException)
            {
                Assert.Fail("SingleItem should allow the item to be removed");
            }
            Assert.AreEqual(0, b.Count);
            try
            {
                string foo = b[0];
                Assert.Fail("SingleItem no longer has anything");
            }
            catch (ArgumentOutOfRangeException) { /* expected */ }
            try
            {
                b.Add("bar");
            }
            catch (NotSupportedException)
            {
                Assert.Fail("SingleItem should be able to add to an empty list");
            }
            Assert.AreEqual(1, b.Count);
            Assert.IsFalse(b.Contains("foo"));
            Assert.IsTrue(b.Contains("bar"));
        }

        [Test]
        public void TestCopyToZeroLengthArray()
        {
            SingleItem<string> b = new SingleItem<string>("foo");

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

    [TestFixture]
    public class tWeakKeyDictionaryTests
    {
        private WeakKeyDictionary<object, string> PopulateDictionary()
        {
            WeakKeyDictionary<object, string> dictionary =
                new WeakKeyDictionary<object, string>();
            dictionary[new object()] = "test";
            dictionary[new object()] = "foo";
            dictionary[new object()] = "bar";
            return dictionary;
        }

        [Test]
        public void TestWeakReferenceCollection() 
        {
            // as per <http://msdn.microsoft.com/en-us/library/aa903910(VS.71).aspx>
            WeakReference wr = new WeakReference(new List<object>());
            WeakReference<object> wr2 = new WeakReference<object>(new object());
            GC.Collect();
            try
            {
                Console.WriteLine("WkRef to obj is in generation {0}", GC.GetGeneration(wr));
                Assert.Fail("weak reference should not have survived collection");
            }
            catch (Exception e) { /* ignore: expected */ }
            Assert.IsFalse(wr.IsAlive);
            try
            {
                Console.WriteLine("WkRef to obj is in generation {0}", GC.GetGeneration(wr2));
                Assert.Fail("weak reference should not have survived collection");
            }
            catch (Exception e) { /* ignore: expected */ }
            Assert.IsFalse(wr2.IsAlive);
        }

        [Test]
        public void TestWeakRefsInDictionary()
        {
            Dictionary<WeakReference<object>, string> dictionary = 
                new Dictionary<WeakReference<object>, string>();
            dictionary[new WeakReference<object>(new object())] = "foo";
            dictionary[new WeakReference<object>(new object())] = "bar";
            dictionary[new WeakReference<object>(new object())] = "baz";
            Assert.AreEqual(3, dictionary.Count);
            GC.Collect();
            foreach (WeakReference<object> wr in dictionary.Keys)
            {
                try
                {
                    Console.WriteLine("WkRef to obj is in generation {0}", GC.GetGeneration(wr));
                    Assert.Fail("weak reference should not have survived collection");
                }
                catch(Exception e)
                {
                    Console.WriteLine("Couldn't get WeakRef generation for {0}", wr);
                    /* ignore: expected */
                }
                Assert.IsFalse(wr.IsAlive);
            }
        }

        [Test]
        public void TestBasics()
        {
            WeakKeyDictionary<object, string> dictionary = PopulateDictionary();
            //Assert.AreEqual(3, dictionary.Count);
            //Assert.AreEqual(3, dictionary.Keys.Count);
            //foreach (object key in dictionary.Keys)
            //{
            //    Assert.IsTrue(dictionary.ContainsKey(key));
            //}
            //Assert.IsFalse(dictionary.ContainsKey("blah"));
            //Assert.IsFalse(dictionary.ContainsKey(new object()));
            //Assert.AreEqual(3, dictionary.Values.Count);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            dictionary.Flush();
            Assert.AreEqual(0, dictionary.Keys.Count);
            Assert.AreEqual(0, dictionary.Values.Count);
            Assert.AreEqual(0, dictionary.Count);
        }

        [Test]
        public void TestRemove()
        {
            WeakKeyDictionary<object, string> dictionary =
                new WeakKeyDictionary<object, string>();
            dictionary[new object()] = "test";
            dictionary[new object()] = "foo";
            dictionary[new object()] = "bar";
            Assert.AreEqual(3, dictionary.Count);
            Assert.AreEqual(3, dictionary.Keys.Count);

            object testObject = new object();
            dictionary[testObject] = "blah";
            Assert.AreEqual(4, dictionary.Count);
            Assert.AreEqual(4, dictionary.Keys.Count);
            Assert.IsTrue(dictionary.ContainsKey(testObject));
            Assert.IsTrue(dictionary.Remove(testObject));
            Assert.AreEqual(3, dictionary.Count);
            Assert.AreEqual(3, dictionary.Keys.Count);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            foreach (object key in dictionary.Keys)
            {
                Console.WriteLine("gen[{0}] = {1}", key, GC.GetGeneration(key));
            }
            Assert.AreEqual(0, dictionary.Count);
            Assert.AreEqual(0, dictionary.Keys.Count);
            Assert.AreEqual(0, dictionary.Values.Count);
        }

        [Test]
        public void TestHold()
        {
            WeakKeyDictionary<object, string> dictionary =
                new WeakKeyDictionary<object, string>();
            dictionary[new object()] = "test";
            dictionary[new object()] = "foo";
            dictionary[new object()] = "bar";
            object testObject = new object();
            dictionary[testObject] = "blah";
            Assert.AreEqual(4, dictionary.Count);
            Assert.AreEqual(4, dictionary.Keys.Count);
            Assert.IsTrue(dictionary.ContainsKey(testObject));

            GC.Collect();
            GC.WaitForPendingFinalizers();
            Assert.AreEqual(1, dictionary.Count);
            Assert.AreEqual(1, dictionary.Keys.Count);
            Assert.AreEqual(1, dictionary.Values.Count);
            Assert.IsTrue(dictionary.ContainsKey(testObject));
        }

    }
}
