using System;
using System.Globalization;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Romer.GeoLib.Tests
{
    [TestClass]
    public class GridSystemProvidersTests
    {
        private static Location At(double latitude, double longitude) => new Location() { Latitude = latitude, Longitude = longitude };


        [TestMethod]
        // Great Britain
        [DataRow("London", 51.5074, -0.1278, "OSGB")]
        [DataRow("Edinburgh", 55.9533, -3.1883, "OSGB")]
        [DataRow("Land's End", 50.0657, -5.7132, "OSGB")]
        [DataRow("Lerwick", 60.1530, -1.1493, "OSGB")]
        [DataRow("St David's", 51.8820, -5.2690, "OSGB")]
        [DataRow("Mull of Kintyre", 55.2900, -5.8000, "OSGB")]
        [DataRow("Mull of Galloway", 54.6350, -4.8570, "OSGB")]
        [DataRow("Stranraer", 54.9030, -5.0240, "OSGB")]
        [DataRow("Dover", 51.1279, 1.3134, "OSGB")]
        [DataRow("Hastings", 50.8543, 0.5735, "OSGB")]
        // Ireland, including Northern Ireland. All of these were in the British grid's box, which was checked first.
        [DataRow("Dublin", 53.3498, -6.2603, "IRELG")]
        [DataRow("Belfast", 54.5973, -5.9301, "IRELG")]
        [DataRow("Cork", 51.8985, -8.4756, "IRELG")]
        [DataRow("Donaghadee", 54.6400, -5.5300, "IRELG")]
        [DataRow("Fair Head", 55.2200, -6.1500, "IRELG")]
        [DataRow("Galway", 53.2707, -9.0568, "IRELG")]
        [DataRow("Cape Clear Island", 51.4330, -9.5000, "IRELG")]
        [DataRow("Malin Head", 55.3800, -7.3700, "IRELG")]
        // United States. Alaska and Hawaii were outside USNG's area.
        [DataRow("New York", 40.7128, -74.0060, "USNG")]
        [DataRow("Anchorage", 61.2181, -149.9003, "USNG")]
        [DataRow("Honolulu", 21.3069, -157.8583, "USNG")]
        [DataRow("San Juan", 18.4655, -66.1057, "USNG")]
        // Elsewhere. Calais and Dieppe were in the British grid's box. Santiago was "in" the USNG area, which then threw.
        [DataRow("Calais", 50.9513, 1.8587, "UTM")]
        [DataRow("Dieppe", 49.9229, 1.0775, "UTM")]
        [DataRow("Santiago", -33.4489, -70.6693, "UTM")]
        [DataRow("Lima", -12.0464, -77.0428, "UTM")]
        [DataRow("Sydney", -33.8688, 151.2093, "UTM")]
        // UTM doesn't cover the poles
        [DataRow("North Pole", 89.0, 0.0, "WGS84")]
        public void BestInArea(string place, double latitude, double longitude, string expectedTag)
        {
            var provider = GridSystemProviders.BestInArea(At(latitude, longitude));

            Assert.AreEqual(expectedTag, provider.Tag, place);
        }


        [TestMethod]
        public void AllInArea_ProvidersCanFormatEveryLocationTheyClaim()
        {
            // Every provider that claims a location must be able to format it without throwing.
            for (var lat = -79.5; lat <= 83.5; lat += 1.5)
            {
                for (var lon = -179.5; lon <= 179.5; lon += 1.5)
                {
                    var location = At(lat, lon);
                    foreach (var provider in GridSystemProviders.AllInArea(location))
                    {
                        try
                        {
                            Assert.IsFalse(string.IsNullOrWhiteSpace(provider.GetGridReference(location, 10)));
                        }
                        catch (Exception ex) when (ex is not AssertFailedException)
                        {
                            Assert.Fail($"{provider.Tag} failed at {lat},{lon}: {ex.Message}");
                        }
                    }
                }
            }
        }


        [TestMethod]
        public void FindByTag_ReturnsNullForUnknownTag()
        {
            Assert.AreEqual("OSGB", GridSystemProviders.FindByTag("OSGB")?.Tag);
            Assert.IsNull(GridSystemProviders.FindByTag("NOT-A-GRID"));
        }


        [TestMethod]
        public void GetGridReference_RejectsInvalidPrecision()
        {
            var location = At(51.5, -0.12);
            foreach (var provider in GridSystemProviders.AllInArea(location))
            {
                // Previously divided by zero
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => provider.GetGridReference(location, 0), provider.Tag);
            }
        }


        [TestMethod]
        public void LatLon_IsCultureInvariantAndKeepsLeadingZero()
        {
            IGridSystemProvider provider = new LatLonGridSystemProvider();
            var original = CultureInfo.CurrentCulture;
            try
            {
                // Uses ',' as the decimal separator, which made "51,5,-0,1" ambiguous.
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");

                Assert.AreEqual("51.50735,-0.12776", provider.GetGridReference(At(51.507351, -0.127758), 1));
                Assert.AreEqual("0.5,-0.25", provider.GetGridReference(At(0.5, -0.25), 1));
                Assert.AreEqual("51.5074,-0.1278", provider.GetGridReference(At(51.507351, -0.127758), 10));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }
    }
}
