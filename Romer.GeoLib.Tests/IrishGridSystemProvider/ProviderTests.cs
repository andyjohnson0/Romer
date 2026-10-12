using System;
using System.Reflection;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Romer.GeoLib;



namespace Romer.GeoLib.Tests.IrishGridSystemProvider
{
    [TestClass]
    public class ProviderTests
    {
        // The test data is from Tailte Éireann's official converter, truncated to the metre, and Romer's grid references
        // are truncated too. So two positions a few centimetres apart can differ by 1 m, and this is the smallest
        // tolerance that allows for that.
        private const int toleranceMetres = 1;

        [TestMethod]
        public void TestInArea()
        {
            var provider = new GeoLib.IrishGridSystemProvider() as IGridSystemProvider;

            Assert.IsFalse(provider.IsInArea(new Location { Latitude = 51D, Longitude = -8D }));
            Assert.IsFalse(provider.IsInArea(new Location { Latitude = 56D, Longitude = -8D }));
            Assert.IsFalse(provider.IsInArea(new Location { Latitude = 53D, Longitude = 11D }));
            Assert.IsFalse(provider.IsInArea(new Location { Latitude = 56D, Longitude = -6D }));

            Assert.IsTrue(provider.IsInArea(new Location { Latitude = 52D, Longitude = -8D }));
            Assert.IsTrue(provider.IsInArea(new Location { Latitude = 55D, Longitude = -8D }));
            Assert.IsTrue(provider.IsInArea(new Location { Latitude = 53D, Longitude = -10D }));
            Assert.IsTrue(provider.IsInArea(new Location { Latitude = 53D, Longitude = -6D }));

            //return (location.Latitude >= 51.39D && location.Latitude <= 55.43D) &&
            //       (location.Longitude >= -10.56D && location.Longitude <= -5.34D);
        }


        [TestMethod]
        public void TestGridRef()
        {
            using (var stm = Assembly.GetExecutingAssembly().GetManifestResourceStream("Romer.GeoLib.Tests.IrishGridSystemProvider.TestData.csv"))
            {
                using(var rdr = new CsvStreamReader(stm!))
                {
                    while(!rdr.EndOfStream)
                    {
                        var vals = rdr.ReadValues();
                        if (vals == null)
                            break;
                        Assert.AreEqual(3, vals.Length);
                        Assert.AreEqual(13, vals[2].Length);

                        var location = new Location()
                        {
                            Latitude = double.Parse(vals[0]),
                            Longitude = double.Parse(vals[1])
                        };
                        var provider = new GeoLib.IrishGridSystemProvider() as IGridSystemProvider;
                        var gridRef = provider.GetGridReference(location, 1);

                        var (expectedEasting, expectedNorthing) = GridRefToMetres(vals[2]);
                        var (easting, northing) = GridRefToMetres(gridRef);
                        Assert.AreEqual(expectedEasting, easting, toleranceMetres, $"Easting of {gridRef}, expected {vals[2]}");
                        Assert.AreEqual(expectedNorthing, northing, toleranceMetres, $"Northing of {gridRef}, expected {vals[2]}");
                    }
                }
            }
        }


        /// <summary>
        /// Convert a 10-figure Irish Grid reference, such as "C 39872 58812", to eastings and northings in metres.
        /// </summary>
        private static (int easting, int northing) GridRefToMetres(string gridRef)
        {
            // Each 100 km square's letter, by column (easting) then row (northing) from the south-west.
            string[] columns = { "VQLFA", "WRMGB", "XSNHC", "YTOJD", "ZUPKE" };
            var parts = gridRef.Split(' ');
            int column = Array.FindIndex(columns, c => c.Contains(parts[0]));
            int row = columns[column].IndexOf(parts[0]);
            return (column * 100000 + int.Parse(parts[1]), row * 100000 + int.Parse(parts[2]));
        }
    }
}
