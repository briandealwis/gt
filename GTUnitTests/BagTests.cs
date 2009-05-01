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
            catch (ArgumentException e) { /* expected result */ }
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


}
