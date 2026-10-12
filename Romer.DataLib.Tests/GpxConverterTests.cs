using System.Globalization;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Romer.DataLib.Tests
{
    [TestClass]
    public class GpxConverterTests
    {
        private const string DefaultRouteName = "New Route";
        private const string DefaultWaypointName = "Waypoint";


        private static Route FromFile(string fileName)
        {
            using (var stm = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "TestData", "Gpx", fileName)))
            {
                return GpxConverter.FromGpx(stm, DefaultRouteName, DefaultWaypointName);
            }
        }


        private static Route FromString(string gpxBody)
        {
            var gpx = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                      "<gpx version=\"1.1\" creator=\"test\" xmlns=\"http://www.topografix.com/GPX/1/1\">" + gpxBody + "</gpx>";
            return GpxConverter.FromGpx(gpx, DefaultRouteName, DefaultWaypointName);
        }


        private static string Track(int pointCount)
        {
            var sb = new StringBuilder("<trk><name>Generated</name><trkseg>");
            for (var i = 0; i < pointCount; i++)
            {
                sb.Append(string.Format(CultureInfo.InvariantCulture, "<trkpt lat=\"{0:F4}\" lon=\"{1:F4}\"><ele>{2}</ele></trkpt>",
                                        50.0 + i * 0.0001, -1.0 - i * 0.0001, i));
            }
            sb.Append("</trkseg></trk>");
            return sb.ToString();
        }


        #region Choice of points and names

        [TestMethod]
        public void FromGpx_Route_UsesRoutePointsAndName()
        {
            var route = FromFile("malham-loop-route.gpx");

            Assert.AreEqual("Malham Loop", route.Label);
            Assert.AreEqual(string.Empty, route.Notes);
            CollectionAssert.AreEqual(new[] { "Malham village", "Malham Cove", "Gordale Scar" }, route.Waypoints.Select(w => w.Label).ToArray());
            var first = route.Waypoints.First();
            Assert.AreEqual(54.0610, first.Latitude);
            Assert.AreEqual(-2.1530, first.Longitude);
            Assert.AreEqual(230D, first.Altitude);
        }


        [TestMethod]
        public void FromGpx_Waypoints_UsesMetadataAndWaypointDetails()
        {
            var route = FromFile("waypoints.gpx");

            Assert.AreEqual("Edinburgh Viewpoints", route.Label);
            Assert.AreEqual("Hills with a view & a café", route.Notes);
            Assert.AreEqual(new DateTime(2024, 5, 1, 9, 0, 0, DateTimeKind.Utc), route.CreationTime);
            Assert.AreEqual(3, route.Waypoints.Count());

            var seat = route.Waypoints.First();
            Assert.AreEqual("Arthur's Seat", seat.Label);
            Assert.AreEqual("Summit trig point", seat.Notes);
            Assert.AreEqual(251D, seat.Altitude);
            Assert.AreEqual(new DateTime(2024, 5, 1, 10, 15, 0, DateTimeKind.Utc), seat.CreationTime);

            var esplanade = route.Waypoints.Last();
            Assert.IsNull(esplanade.Altitude);
            Assert.AreEqual(string.Empty, esplanade.Notes);
        }


        [TestMethod]
        public void FromGpx_PrefersWaypointsToRoute()
        {
            var route = FromString(
                "<wpt lat=\"1\" lon=\"1\"><name>wpt</name></wpt>" +
                "<rte><name>The Route</name><rtept lat=\"2\" lon=\"2\"><name>rtept</name></rtept></rte>");

            CollectionAssert.AreEqual(new[] { "wpt" }, route.Waypoints.Select(w => w.Label).ToArray());
            Assert.AreEqual(DefaultRouteName, route.Label);  // The route's name isn't used, because its points aren't
        }


        [TestMethod]
        public void FromGpx_PrefersRouteToTrack()
        {
            var route = FromString(
                "<rte><name>The Route</name><rtept lat=\"2\" lon=\"2\"/></rte>" +
                "<trk><name>The Track</name><trkseg><trkpt lat=\"3\" lon=\"3\"/></trkseg></trk>");

            Assert.AreEqual("The Route", route.Label);
            Assert.AreEqual(2D, route.Waypoints.Single().Latitude);
        }


        [TestMethod]
        public void FromGpx_SkipsRoutesAndTracksWithoutPoints()
        {
            var route = FromString(
                "<rte><name>Empty Route</name></rte>" +
                "<trk><name>Empty Track</name><trkseg/></trk>" +
                "<trk><name>Second Track</name><trkseg><trkpt lat=\"3\" lon=\"3\"/></trkseg></trk>");

            Assert.AreEqual("Second Track", route.Label);
            Assert.AreEqual(1, route.Waypoints.Count());
        }


        [TestMethod]
        public void FromGpx_MetadataNameOverridesRouteName()
        {
            var route = FromString(
                "<metadata><name>File Name</name></metadata>" +
                "<rte><name>Route Name</name><rtept lat=\"2\" lon=\"2\"/></rte>");

            Assert.AreEqual("File Name", route.Label);
        }


        [TestMethod]
        public void FromGpx_MissingOrBlankNames_UseDefaults()
        {
            var route = FromString(
                "<metadata><name>   </name></metadata>" +
                "<rte><rtept lat=\"1\" lon=\"1\"/><rtept lat=\"2\" lon=\"2\"><name> </name></rtept></rte>");

            Assert.AreEqual(DefaultRouteName, route.Label);
            CollectionAssert.AreEqual(new[] { DefaultWaypointName, DefaultWaypointName }, route.Waypoints.Select(w => w.Label).ToArray());
        }


        [TestMethod]
        public void FromGpx_NoPoints_ReturnsRouteWithoutWaypoints()
        {
            var route = FromString("<metadata><name>Nothing Here</name></metadata>");

            Assert.AreEqual("Nothing Here", route.Label);
            Assert.AreEqual(0, route.Waypoints.Count());
        }


        [TestMethod]
        public void FromGpx_Gpx10_ReadsWaypoints()
        {
            var route = FromFile("gpx-1.0.gpx");

            CollectionAssert.AreEqual(new[] { "Westminster", "Tower" }, route.Waypoints.Select(w => w.Label).ToArray());
            Assert.AreEqual(5D, route.Waypoints.First().Altitude);
            Assert.IsNull(route.Waypoints.Last().Altitude);
        }


        [TestMethod]
        public void FromGpx_Malformed_Throws()
        {
            Assert.Throws<Exception>(() => FromFile("malformed.gpx"));
        }

        #endregion Choice of points and names


        #region Tracks

        [TestMethod]
        public void FromGpx_Track_JoinsSegments()
        {
            var route = FromFile("track-two-segments.gpx");

            Assert.AreEqual("Ridge Walk", route.Label);
            CollectionAssert.AreEqual(new[] { 600D, 620D, 640D }, route.Waypoints.Select(w => w.Altitude!.Value).ToArray());
            Assert.IsTrue(route.Waypoints.All(w => w.Label == DefaultWaypointName));
        }


        [TestMethod]
        [DataRow(1)]
        [DataRow(GpxConverter.MaxTrackPoints)]
        public void FromGpx_TrackWithinLimit_KeepsAllPoints(int pointCount)
        {
            var route = FromString(Track(pointCount));

            Assert.AreEqual(pointCount, route.Waypoints.Count());
        }


        [TestMethod]
        [DataRow(GpxConverter.MaxTrackPoints + 1)]
        [DataRow(1000)]
        [DataRow(10007)]
        public void FromGpx_LongTrack_IsThinnedKeepingFirstAndLast(int pointCount)
        {
            var route = FromString(Track(pointCount));

            var altitudes = route.Waypoints.Select(w => (int)w.Altitude!.Value).ToArray();  // Altitude is the original point's index
            Assert.HasCount(GpxConverter.MaxTrackPoints, altitudes);
            Assert.AreEqual(0, altitudes.First());
            Assert.AreEqual(pointCount - 1, altitudes.Last());
            CollectionAssert.AllItemsAreUnique(altitudes);
            CollectionAssert.AreEqual(altitudes.Order().ToArray(), altitudes);  // In the original order
        }

        #endregion Tracks


        #region Writing

        [TestMethod]
        public void ToGpx_ThenFromGpx_RoundTripsRoute()
        {
            var route = new Route()
            {
                Label = "Round Trip <&>",
                Notes = "Notes \"quoted\"",
                CreationTime = new DateTime(2024, 7, 1, 8, 30, 0, DateTimeKind.Utc)
            };
            route.Add(new Waypoint() { Label = "A", Notes = "first", Latitude = 54.1234567, Longitude = -2.7654321, Altitude = 123.5,
                                       CreationTime = new DateTime(2024, 7, 1, 9, 0, 0, DateTimeKind.Utc) });
            route.Add(new Waypoint() { Label = "B", Latitude = 54.2, Longitude = -2.8,
                                       CreationTime = new DateTime(2024, 7, 1, 10, 0, 0, DateTimeKind.Utc) });

            using var stm = new MemoryStream();
            GpxConverter.ToGpx(route, stm, "Romer test");
            stm.Position = 0;
            var copy = GpxConverter.FromGpx(stm, DefaultRouteName, DefaultWaypointName);

            Assert.AreEqual(route.Label, copy.Label);
            Assert.AreEqual(route.Notes, copy.Notes);
            Assert.AreEqual(route.CreationTime, copy.CreationTime);
            Assert.AreEqual(2, copy.Waypoints.Count());
            foreach (var (original, roundTripped) in route.Waypoints.Zip(copy.Waypoints))
            {
                Assert.AreEqual(original.Label, roundTripped.Label);
                Assert.AreEqual(original.Notes, roundTripped.Notes);
                Assert.AreEqual(original.Latitude, roundTripped.Latitude);
                Assert.AreEqual(original.Longitude, roundTripped.Longitude);
                Assert.AreEqual(original.Altitude, roundTripped.Altitude);
                Assert.AreEqual(original.CreationTime, roundTripped.CreationTime);
            }
        }


        [TestMethod]
        public void ToGpx_WritesCreatorAndWaypoints()
        {
            var route = new Route() { Label = "R" };
            route.Add(new Waypoint() { Label = "A", Latitude = 1, Longitude = 2 });

            using var stm = new MemoryStream();
            GpxConverter.ToGpx(route, stm, "Romer v9.9");
            var xml = Encoding.UTF8.GetString(stm.ToArray());

            StringAssert.Contains(xml, "creator=\"Romer v9.9\"");
            StringAssert.Contains(xml, "<wpt ");
            Assert.DoesNotContain("<rte", xml);
        }


        [TestMethod]
        public void ToGpx_WaypointWithoutAltitude_WritesNoElevation()
        {
            var route = new Route() { Label = "R" };
            route.Add(new Waypoint() { Label = "A", Latitude = 1, Longitude = 2, CreationTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

            using var stm = new MemoryStream();
            GpxConverter.ToGpx(route, stm, "test");
            var xml = Encoding.UTF8.GetString(stm.ToArray());

            Assert.DoesNotContain("<ele>", xml);  // Not <ele>0</ele>
        }


        [TestMethod]
        public void ToGpx_WithWaypointIds_WritesOnlyThoseWaypoints()
        {
            var route = new Route() { Label = "R" };
            route.Add(new Waypoint() { Id = 1, Label = "A" });
            route.Add(new Waypoint() { Id = 2, Label = "B" });
            route.Add(new Waypoint() { Id = 3, Label = "C" });

            using var stm = new MemoryStream();
            GpxConverter.ToGpx(route, stm, "test", waypointIds: new[] { 1, 3 });
            stm.Position = 0;
            var copy = GpxConverter.FromGpx(stm, DefaultRouteName, DefaultWaypointName);

            CollectionAssert.AreEqual(new[] { "A", "C" }, copy.Waypoints.Select(w => w.Label).ToArray());
        }

        #endregion Writing
    }
}
