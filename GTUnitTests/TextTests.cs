using GT.UI;
using NUnit.Framework;

namespace GT.UnitTests.UI
{
    [TestFixture]
    public class FloatingPrecisionTests
    {
        [Test]
        public void TestValues()
        {
            Assert.AreEqual(0, Slider.DeterminePrecision(0));
            Assert.AreEqual(0, Slider.DeterminePrecision(10));
            Assert.AreEqual(1, Slider.DeterminePrecision(0.1f));
            Assert.AreEqual(2, Slider.DeterminePrecision(0.01f));
            Assert.AreEqual(2, Slider.DeterminePrecision(0.15f));
            Assert.AreEqual(2, Slider.DeterminePrecision(-0.15f));
            Assert.AreEqual(3, Slider.DeterminePrecision(-0.915f));
        }

        //[NUnit.Framework.Ignore()]
        [Test]
        public void TestUnrepresentable()
        {
            Assert.IsTrue(Slider.DeterminePrecision(1f/7f) > 2);
        }
        
    }

}