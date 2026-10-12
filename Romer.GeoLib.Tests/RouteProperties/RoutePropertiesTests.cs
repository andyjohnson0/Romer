using System;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Globalization;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Romer.GeoLib;
using Romer.DataLib;





namespace Romer.GeoLib.Tests.RouteProperties
{
    [TestClass]
    public class RoutePropertiesTests
    {
        private const decimal distanceDelta = 0.001M; // KM

        [TestMethod]
        public void TestEmptyRoute()
        {
            var route = new Route();
            var locations = route.Waypoints.Select(wp => wp.GetLocation()).ToArray();
            var props = GeoLib.RouteProperties.CreateFrom(locations);
            Assert.IsNotNull(props);
            Assert.IsNull(props.TotalAscent);
            Assert.IsNull(props.TotalDescent);
            Assert.IsNull(props.TotalDistance);
            Assert.IsNull(props.TotalWalkTime);
            Assert.AreEqual(0, props.LegProperties.Count());
        }

        [TestMethod]
        public void TestSingleWaypointRoute()
        {
            var route = new Route();
            route.Add(new Waypoint() { Latitude = 57.133278D, Longitude = -3.670674D, Altitude = 702D });
            var locations = route.Waypoints.Select(wp => wp.GetLocation()).ToArray();
            var props = GeoLib.RouteProperties.CreateFrom(locations);
            Assert.IsNotNull(props);
            Assert.IsNull(props.TotalAscent);
            Assert.IsNull(props.TotalDescent);
            Assert.IsNull(props.TotalDistance);
            Assert.IsNull(props.TotalWalkTime);
            Assert.AreEqual(0, props.LegProperties.Count());  // Was null, which crashed the route info card
        }


        [TestMethod]
        public void TestSingleLegAscentOnlyRoute()
        {
            // Route with ascent adds extra to walk time
            var route = new Route();
            route.Add(new Waypoint() { Latitude = 57.133278D, Longitude = -3.670674D, Altitude = 202D });
            route.Add(new Waypoint() { Latitude = 57.119159D, Longitude = -3.687067D, Altitude = 790D });
            var locations = route.Waypoints.Select(wp => wp.GetLocation()).ToArray();
            var props = GeoLib.RouteProperties.CreateFrom(locations);
            Assert.IsNotNull(props);
            Assert.AreEqual(588M, props.TotalAscent);
            Assert.IsNull(props.TotalDescent);
            Assert.IsNotNull(props.TotalDistance);
            Assert.AreEqual(1.855M/*72253399609*/, props.TotalDistance.GetValueOrDefault(), distanceDelta);
            Assert.AreEqual(new TimeSpan(01, 21, 04), props.TotalWalkTime); 
        }


        [TestMethod]
        public void TestSingleLegDescentOnlyRoute()
        {
            // Route with descent adds no extra to walk time
            var route = new Route();
            route.Add(new Waypoint() { Latitude = 57.119159D, Longitude = -3.687067D, Altitude = 790D });
            route.Add(new Waypoint() { Latitude = 57.133278D, Longitude = -3.670674D, Altitude = 202D });
            var locations = route.Waypoints.Select(wp => wp.GetLocation()).ToArray();
            var props = GeoLib.RouteProperties.CreateFrom(locations);
            Assert.IsNotNull(props);
            Assert.IsNull(props.TotalAscent);
            Assert.AreEqual(588M, props.TotalDescent);
            Assert.IsNotNull(props.TotalDistance);
            Assert.AreEqual(1.855M/*72253399609*/, props.TotalDistance.GetValueOrDefault(), distanceDelta);
            Assert.AreEqual(new TimeSpan(00, 22, 16), props.TotalWalkTime); 
        }


        [TestMethod]
        public void TestSingleLegNoDistanceRoute()
        {
            var route = new Route();
            var wp = new Waypoint() { Latitude = 57.119159D, Longitude = -3.687067D, Altitude = 790D };
            route.Add(wp);
            route.Add(wp);
            var locations = route.Waypoints.Select(wp => wp.GetLocation()).ToArray();
            var props = GeoLib.RouteProperties.CreateFrom(locations);
            Assert.IsNotNull(props);
            Assert.IsNull(props.TotalAscent);
            Assert.IsNull(props.TotalDescent);
            Assert.AreEqual(0M, props.TotalDistance);
            Assert.AreEqual(TimeSpan.Zero, props.TotalWalkTime); 
        }


        [TestMethod]
        public void TestRoute1()
        {
            // Expected values are worked out in Route1.csv
            var route = ReadRoute("Route1.csv");
            Assert.AreEqual(9, route.Waypoints.Count());

            var locations = route.Waypoints.Select(wp => wp.GetLocation()).ToArray();
            var props = GeoLib.RouteProperties.CreateFrom(locations);
            Assert.IsNotNull(props);
            Assert.AreEqual(374M, props.TotalAscent);
            Assert.AreEqual(394M, props.TotalDescent);
            Assert.IsNotNull(props.TotalDistance);
            Assert.AreEqual(11.8014M, props.TotalDistance.GetValueOrDefault(), distanceDelta);
            Assert.AreEqual(new TimeSpan(02, 59, 01), props.TotalWalkTime);

            var expectedLegDistances = new double[] { 1.4440, 1.5320, 1.1359, 1.9008, 1.1032, 0.0, 2.9105, 1.7752 };
            var legDistances = props.LegProperties.Select(lp => lp.Distance).ToArray();
            Assert.AreEqual(expectedLegDistances.Length, legDistances.Length);
            for (int i = 0; i < expectedLegDistances.Length; i++)
            {
                Assert.AreEqual(expectedLegDistances[i], legDistances[i], (double)distanceDelta, $"Leg {i + 1}");
            }
        }




        private Route ReadRoute(string filename)
        {
            var route = new Route();
            using (var stm = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Romer.GeoLib.Tests.RouteProperties.{filename}"))
            {
                using (var rdr = new CsvStreamReader(stm!))
                {
                    while (!rdr.EndOfStream)
                    {
                        var vals = rdr.ReadValues();
                        if (vals == null)
                            break;

                        var waypoint = new Waypoint()
                        {
                            CreationTime = DateTime.Parse(vals[0], styles: DateTimeStyles.AssumeLocal),
                            Latitude = double.Parse(vals[1]),
                            Longitude = double.Parse(vals[2]),
                            Altitude = double.Parse(vals[3])
                        };
                        route.Add(waypoint);
                    }
                }
            }
            return route;
        }
    }


    internal static class WaypointExt
    {
        public static GeoLib.Location GetLocation(
            this Waypoint self)
        {
            return new GeoLib.Location()
            {
                Time = self.CreationTime,
                Latitude = self.Latitude,
                Longitude = self.Longitude,
                Altitude = self.Altitude
            };
        }
    }
}
