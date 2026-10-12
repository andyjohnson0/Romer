using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Romer.GeoLib.Tests
{
    [TestClass]
    public class BearingTests
    {
        private const double delta = 1e-9;


        [TestMethod]
        [DataRow(0D, 0D)]
        [DataRow(360D, 0D)]
        [DataRow(-90D, 270D)]
        [DataRow(725D, 5D)]
        [DataRow(-725D, 355D)]
        [DataRow(359.5D, 359.5D)]
        public void Normalise(double input, double expected)
        {
            Assert.AreEqual(expected, Bearing.Normalise(input), delta);
        }


        [TestMethod]
        [DataRow(10D, 350D, -20D)]   // Across north, anticlockwise. Was -340 in CompassView.
        [DataRow(350D, 10D, 20D)]    // Across north, clockwise
        [DataRow(0D, 90D, 90D)]
        [DataRow(90D, 0D, -90D)]
        [DataRow(0D, 180D, -180D)]   // Either way is shortest; result range is [-180, 180)
        [DataRow(45D, 45D, 0D)]
        [DataRow(-30D, 400D, 70D)]   // Unnormalised inputs
        public void ShortestRotation(double from, double to, double expected)
        {
            Assert.AreEqual(expected, Bearing.ShortestRotation(from, to), delta);
        }


        [TestMethod]
        public void Smoother_AveragesAcrossNorthCorrectly()
        {
            // An arithmetic mean of these is 180, i.e. due south.
            var smoother = new BearingSmoother(alpha: 0.5);
            smoother.Add(358D);
            var value = smoother.Add(2D);

            Assert.IsTrue(value < 1D || value > 359D, $"Expected about north but got {value}");
        }


        [TestMethod]
        public void Smoother_FirstReadingIsReturnedUnchanged()
        {
            var smoother = new BearingSmoother(alpha: 0.1);
            Assert.IsNull(smoother.Value);

            Assert.AreEqual(123D, smoother.Add(123D), 1e-6);
        }


        [TestMethod]
        public void Smoother_ConvergesOnSteadyReading()
        {
            var smoother = new BearingSmoother(alpha: 0.3);
            smoother.Add(10D);
            double value = 0;
            for (var i = 0; i < 50; i++)
                value = smoother.Add(80D);

            Assert.AreEqual(80D, value, 0.01);
        }


        [TestMethod]
        public void Smoother_ResetDiscardsReadings()
        {
            var smoother = new BearingSmoother(alpha: 0.3);
            smoother.Add(10D);
            smoother.Reset();

            Assert.IsNull(smoother.Value);
            Assert.AreEqual(200D, smoother.Add(200D), 1e-6);
        }


        [TestMethod]
        public void Smoother_RejectsInvalidAlpha()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BearingSmoother(0D));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BearingSmoother(1.5D));
        }
    }
}
