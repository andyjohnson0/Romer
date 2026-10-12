using System;
using System.Reflection;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Romer.GeoLib;



namespace Romer.GeoLib.Tests.UtmGridSystemProvider
{
    [TestClass]
    public class ProviderTests
    {
        private const int precisionMetres = 100;  // 1M => 6 digits per part, 10M => 5 digits per part, etc


        [TestMethod]
        public void TestInArea()
        {
            var provider = new GeoLib.UtmGridSystemProvider() as IGridSystemProvider;

            Assert.IsTrue(provider.IsInArea(new Location { Latitude = 0D, Longitude = 0D }));

            Assert.IsFalse(provider.IsInArea(new Location { Latitude = -80.1D, Longitude = 0D }));
            Assert.IsFalse(provider.IsInArea(new Location { Latitude = 84.1D, Longitude = 0D }));
            Assert.IsFalse(provider.IsInArea(new Location { Latitude = 0D, Longitude = -180.1D }));
            Assert.IsFalse(provider.IsInArea(new Location { Latitude = 0D, Longitude = 180.1D }));

            Assert.IsTrue(provider.IsInArea(new Location { Latitude = -80D, Longitude = 0D }));
            Assert.IsTrue(provider.IsInArea(new Location { Latitude = 84D, Longitude = 0D }));
            Assert.IsTrue(provider.IsInArea(new Location { Latitude = 0D, Longitude = -180D }));
            Assert.IsTrue(provider.IsInArea(new Location { Latitude = 0D, Longitude = 180D }));
        }


        [TestMethod]
        public void TestGridRef()
        {
            using (var stm = Assembly.GetExecutingAssembly().GetManifestResourceStream("Romer.GeoLib.Tests.UtmGridSystemProvider.TestData.csv"))
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
                        var provider = new GeoLib.UtmGridSystemProvider() as IGridSystemProvider;
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
            var parts = gridRef.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var x = double.Parse(parts[1]) / precisionMetres;
            parts[1] = ((int)x).ToString();
            x = double.Parse(parts[2]) / precisionMetres;
            parts[2] = ((int)x).ToString();
            return string.Join(' ', parts);
        }
    }
}
