using System;
using System.Reflection;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Romer.GeoLib;



namespace Romer.GeoLib.Tests
{
    [TestClass]
    public class UnitConverterTests
    {
        [TestMethod]
        public void TestMetersTo()
        {
            Assert.AreEqual(0M, UnitConverter.Convert(0M, LengthUnit.Metre, LengthUnit.Metre));
            Assert.AreEqual(1M, UnitConverter.Convert(1M, LengthUnit.Metre, LengthUnit.Metre));
            Assert.AreEqual(3.6M, UnitConverter.Convert(3.6M, LengthUnit.Metre, LengthUnit.Metre));
            Assert.AreEqual(776655.387M, UnitConverter.Convert(776655.387M, LengthUnit.Metre, LengthUnit.Metre));

            Assert.AreEqual(0M, UnitConverter.Convert(0M, LengthUnit.Metre, LengthUnit.Kilometre));
            Assert.AreEqual(0.001M, UnitConverter.Convert(1M, LengthUnit.Metre, LengthUnit.Kilometre));
            Assert.AreEqual(0.0036M, UnitConverter.Convert(3.6M, LengthUnit.Metre, LengthUnit.Kilometre));
            Assert.AreEqual(776.655387M, UnitConverter.Convert(776655.387M, LengthUnit.Metre, LengthUnit.Kilometre));

            Assert.AreEqual(0M, UnitConverter.Convert(0M, LengthUnit.Metre, LengthUnit.Foot));
            Assert.AreEqual(3.2808399M, UnitConverter.Convert(1M, LengthUnit.Metre, LengthUnit.Foot));
            Assert.AreEqual(11.81102364M, UnitConverter.Convert(3.6M, LengthUnit.Metre, LengthUnit.Foot));
            Assert.AreEqual(2548081.9822195413M, UnitConverter.Convert(776655.387M, LengthUnit.Metre, LengthUnit.Foot));

            Assert.AreEqual(0M, UnitConverter.Convert(0M, LengthUnit.Metre, LengthUnit.Mile));
            Assert.AreEqual(0.00062137M, UnitConverter.Convert(1M, LengthUnit.Metre, LengthUnit.Mile));
            Assert.AreEqual(0.002236932M, UnitConverter.Convert(3.6M, LengthUnit.Metre, LengthUnit.Mile));
            Assert.AreEqual(482.59035782019M, UnitConverter.Convert(776655.387M, LengthUnit.Metre, LengthUnit.Mile));
        }


        [TestMethod]
        public void TestKilometersTo()
        {
            Assert.AreEqual(0M, UnitConverter.Convert(0M, LengthUnit.Kilometre, LengthUnit.Metre));
            Assert.AreEqual(1000M, UnitConverter.Convert(1M, LengthUnit.Kilometre, LengthUnit.Metre));
            Assert.AreEqual(3600M, UnitConverter.Convert(3.6M, LengthUnit.Kilometre, LengthUnit.Metre));
            Assert.AreEqual(776655387M, UnitConverter.Convert(776655.387M, LengthUnit.Kilometre, LengthUnit.Metre));

            Assert.AreEqual(0M, UnitConverter.Convert(0M, LengthUnit.Kilometre, LengthUnit.Kilometre));
            Assert.AreEqual(1M, UnitConverter.Convert(1M, LengthUnit.Kilometre, LengthUnit.Kilometre));
            Assert.AreEqual(3.6M, UnitConverter.Convert(3.6M, LengthUnit.Kilometre, LengthUnit.Kilometre));
            Assert.AreEqual(776655.387M, UnitConverter.Convert(776655.387M, LengthUnit.Kilometre, LengthUnit.Kilometre));

            Assert.AreEqual(0M, UnitConverter.Convert(0M, LengthUnit.Kilometre, LengthUnit.Foot));
            Assert.AreEqual(3280.8399M, UnitConverter.Convert(1M, LengthUnit.Kilometre, LengthUnit.Foot));
            Assert.AreEqual(11811.02364M, UnitConverter.Convert(3.6M, LengthUnit.Kilometre, LengthUnit.Foot));
            Assert.AreEqual(2548081982.2195413M, UnitConverter.Convert(776655.387M, LengthUnit.Kilometre, LengthUnit.Foot));

            Assert.AreEqual(0M, UnitConverter.Convert(0M, LengthUnit.Kilometre, LengthUnit.Mile));
            Assert.AreEqual(0.62137119M, UnitConverter.Convert(1M, LengthUnit.Kilometre, LengthUnit.Mile));
            Assert.AreEqual(2.236936284M, UnitConverter.Convert(3.6M, LengthUnit.Kilometre, LengthUnit.Mile));
            Assert.AreEqual(482591.28204010053M, UnitConverter.Convert(776655.387M, LengthUnit.Kilometre, LengthUnit.Mile));
        }


        [TestMethod]
        public void TestFeetTo()
        {
            Assert.AreEqual(0M, UnitConverter.Convert(0M, LengthUnit.Foot, LengthUnit.Metre));
            Assert.AreEqual(0.3048M, UnitConverter.Convert(1M, LengthUnit.Foot, LengthUnit.Metre));
            Assert.AreEqual(1.09728M, UnitConverter.Convert(3.6M, LengthUnit.Foot, LengthUnit.Metre));
            Assert.AreEqual(236724.5619576M, UnitConverter.Convert(776655.387M, LengthUnit.Foot, LengthUnit.Metre));

            Assert.AreEqual(0M, UnitConverter.Convert(0M, LengthUnit.Foot, LengthUnit.Kilometre));
            Assert.AreEqual(0.0003048M, UnitConverter.Convert(1M, LengthUnit.Foot, LengthUnit.Kilometre));
            Assert.AreEqual(0.00109728M, UnitConverter.Convert(3.6M, LengthUnit.Foot, LengthUnit.Kilometre));
            Assert.AreEqual(236.7245619576M, UnitConverter.Convert(776655.387M, LengthUnit.Foot, LengthUnit.Kilometre));

            Assert.AreEqual(0M, UnitConverter.Convert(0M, LengthUnit.Foot, LengthUnit.Foot));
            Assert.AreEqual(1M, UnitConverter.Convert(1M, LengthUnit.Foot, LengthUnit.Foot));
            Assert.AreEqual(3.6M, UnitConverter.Convert(3.6M, LengthUnit.Foot, LengthUnit.Foot));
            Assert.AreEqual(776655.387M, UnitConverter.Convert(776655.387M, LengthUnit.Foot, LengthUnit.Foot));

            Assert.AreEqual(0M, UnitConverter.Convert(0M, LengthUnit.Foot, LengthUnit.Mile));
            Assert.AreEqual(0.00018939M, UnitConverter.Convert(1M, LengthUnit.Foot, LengthUnit.Mile));
            Assert.AreEqual(0.000681804M, UnitConverter.Convert(3.6M, LengthUnit.Foot, LengthUnit.Mile));
            Assert.AreEqual(147.09076374393M, UnitConverter.Convert(776655.387M, LengthUnit.Foot, LengthUnit.Mile));
        }


        [TestMethod]
        public void TestMilesTo()
        {
            Assert.AreEqual(0M, UnitConverter.Convert(0M, LengthUnit.Mile, LengthUnit.Metre));
            Assert.AreEqual(1609.344M, UnitConverter.Convert(1M, LengthUnit.Mile, LengthUnit.Metre));
            Assert.AreEqual(5793.6384M, UnitConverter.Convert(3.6M, LengthUnit.Mile, LengthUnit.Metre));
            Assert.AreEqual(1249905687.136128M, UnitConverter.Convert(776655.387M, LengthUnit.Mile, LengthUnit.Metre));

            Assert.AreEqual(0M, UnitConverter.Convert(0M, LengthUnit.Mile, LengthUnit.Kilometre));
            Assert.AreEqual(1.609344M, UnitConverter.Convert(1M, LengthUnit.Mile, LengthUnit.Kilometre));
            Assert.AreEqual(5.7936384M, UnitConverter.Convert(3.6M, LengthUnit.Mile, LengthUnit.Kilometre));
            Assert.AreEqual(1249905.687136128M, UnitConverter.Convert(776655.387M, LengthUnit.Mile, LengthUnit.Kilometre));

            Assert.AreEqual(0M, UnitConverter.Convert(0M, LengthUnit.Mile, LengthUnit.Foot));
            Assert.AreEqual(5280M, UnitConverter.Convert(1M, LengthUnit.Mile, LengthUnit.Foot));
            Assert.AreEqual(19008M, UnitConverter.Convert(3.6M, LengthUnit.Mile, LengthUnit.Foot));
            Assert.AreEqual(4100740443.360M, UnitConverter.Convert(776655.387M, LengthUnit.Mile, LengthUnit.Foot));

            Assert.AreEqual(0M, UnitConverter.Convert(0M, LengthUnit.Mile, LengthUnit.Mile));
            Assert.AreEqual(1M, UnitConverter.Convert(1M, LengthUnit.Mile, LengthUnit.Mile));
            Assert.AreEqual(3.6M, UnitConverter.Convert(3.6M, LengthUnit.Mile, LengthUnit.Mile));
            Assert.AreEqual(776655.387M, UnitConverter.Convert(776655.387M, LengthUnit.Mile, LengthUnit.Mile));
        }
    }
}
