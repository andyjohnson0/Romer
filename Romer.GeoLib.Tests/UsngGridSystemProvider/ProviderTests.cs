using System;
using System.Reflection;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Romer.GeoLib;



namespace Romer.GeoLib.Tests.UsngGridSystemProvider
{
    [TestClass]
    public class ProviderTests
    {
        private const int precisionMetres = 10;  // 1M => 6 digits per part, 10M => 5 digits per part, etc


        [TestMethod]
        public void TestInArea()
        {
            var provider = new GeoLib.UsngGridSystemProvider() as IGridSystemProvider;

            // The provider doesn't support the southern hemisphere, so it must not claim it.
            Assert.IsFalse(provider.IsInArea(new Location { Latitude = -80D, Longitude = -100D }));
            Assert.IsFalse(provider.IsInArea(new Location { Latitude = -33.4D, Longitude = -70.7D }));  // Santiago
            Assert.IsFalse(provider.IsInArea(new Location { Latitude = 0D, Longitude = -80D }));
            Assert.IsFalse(provider.IsInArea(new Location { Latitude = 84D, Longitude = -100D }));
            Assert.IsFalse(provider.IsInArea(new Location { Latitude = 51.5D, Longitude = -0.1D }));    // London

            Assert.IsTrue(provider.IsInArea(new Location { Latitude = 40.7D, Longitude = -74.0D }));    // New York
            Assert.IsTrue(provider.IsInArea(new Location { Latitude = 34.0D, Longitude = -118.2D }));   // Los Angeles
            Assert.IsTrue(provider.IsInArea(new Location { Latitude = 61.2D, Longitude = -149.9D }));   // Anchorage
            Assert.IsTrue(provider.IsInArea(new Location { Latitude = 21.3D, Longitude = -157.9D }));   // Honolulu
        }


        [TestMethod]
        public void TestGridRef()
        {
            using (var stm = Assembly.GetExecutingAssembly().GetManifestResourceStream("Romer.GeoLib.Tests.UsngGridSystemProvider.TestData.csv"))
            {
                using(var rdr = new CsvStreamReader(stm!))
                {
                    while(!rdr.EndOfStream)
                    {
                        var vals = rdr.ReadValues();
                        if (vals == null)
                            break;
                        Assert.AreEqual(3, vals.Length);
                        vals[2] = GridRefToPrecision(vals[2], precisionMetres);

                        var location = new Location()
                        {
                            Latitude = double.Parse(vals[0]),
                            Longitude = double.Parse(vals[1])
                        };
                        var provider = new GeoLib.UsngGridSystemProvider() as IGridSystemProvider;
                        var gridRef = provider.GetGridReference(location, precisionMetres);

                        Assert.AreEqual(vals[2], gridRef);
                    }
                }
            }
        }


        private string GridRefToPrecision(
            string gridRef,
            int precisionMetres)
        {
            var numDigitsPerPart = (10000 / precisionMetres).ToString().Length;
            var fmt = $"D{numDigitsPerPart}";

            var parts = gridRef.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var x = double.Parse(parts[2]) / precisionMetres;
            parts[2] = ((int)x).ToString(fmt);
            x = double.Parse(parts[3]) / precisionMetres;
            parts[3] = ((int)x).ToString(fmt);
            return string.Join(' ', parts);
        }
    }
}
