using System;
using System.Reflection;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Romer.GeoLib;



namespace Romer.GeoLib.Tests.BritishGridSystemProvider
{
    [TestClass]
    public class ProviderTests
    {
        private const int precisionMetres = 100;  // Precision used for comparisons. 100M == 3 digits

        [TestMethod]
        public void TestInArea()
        {
            // Latitude >= 49.75D && <= 61.01D, Longitude >= -9.0D && <= 2.01D
            var provider = new GeoLib.BritishGridSystemProvider() as IGridSystemProvider;

            Assert.IsTrue(provider.IsInArea(new Location { Latitude = 55D, Longitude = 0D }));

            Assert.IsFalse(provider.IsInArea(new Location { Latitude = 49D, Longitude = 0D }));
            Assert.IsFalse(provider.IsInArea(new Location { Latitude = 62D, Longitude = 0D }));

            Assert.IsFalse(provider.IsInArea(new Location { Latitude = 55D, Longitude = -10D }));
            Assert.IsFalse(provider.IsInArea(new Location { Latitude = 55D, Longitude = 3D }));
        }


        [TestMethod]
        public void TestGridRef()
        {
            using (var stm = Assembly.GetExecutingAssembly().GetManifestResourceStream("Romer.GeoLib.Tests.BritishGridSystemProvider.TestData.csv"))
            {
                using(var rdr = new CsvStreamReader(stm!))
                {
                    while(!rdr.EndOfStream)
                    {
                        var vals = rdr.ReadValues();
                        if (vals == null)
                            break;
                        Assert.AreEqual(3, vals.Length);
                        Assert.AreEqual(14, vals[2].Length);
                        vals[2] = GridRefToPrecision(vals[2], precisionMetres);

                        var location = new Location()
                        {
                            Latitude = double.Parse(vals[0]),
                            Longitude = double.Parse(vals[1])
                        };
                        var provider = new GeoLib.BritishGridSystemProvider() as IGridSystemProvider;
                        var gridRef = provider.GetGridReference(location, precisionMetres);

                        Assert.AreEqual(vals[2], gridRef);
                    }
                }
            }
        }


        private string GridRefToPrecision(
            string gridRef, // 10 digits with spaces
            int precisionMetres)
        {
            var numDigits = precisionMetres switch
            {
                1 => 5,
                10 => 4,
                100 => 3,
                1000 => 2,
                _ => throw new Exception()
            };
            return gridRef.Substring(0, 2) +
                   " " +
                   gridRef.Substring(3, numDigits) +
                   " " +
                   gridRef.Substring(9, numDigits);
        }
    }
}
