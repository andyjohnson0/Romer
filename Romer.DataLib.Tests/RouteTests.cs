using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Romer.DataLib.Tests
{
    [TestClass]
    public class RouteTests
    {
        [TestMethod]
        public void Json_RoundTripsRouteWithoutIds()
        {
            var route = new Route() { Id = 7, Label = "Arthur's \"Seat\"", Notes = "n", CreationTime = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc) };
            route.Add(new Waypoint() { Id = 3, RouteId = 7, Label = "a", Latitude = 55.94, Longitude = -3.16, Altitude = 251 });
            route.Add(new Waypoint() { Label = "b", Latitude = 55.95, Longitude = -3.17, Ticked = true });

            var copy = Route.FromJson(route.ToJson());

            Assert.IsNotNull(copy);
            Assert.AreEqual(RouteManager.IdNull, copy.Id);
            Assert.AreEqual(route.Label, copy.Label);
            Assert.AreEqual(route.CreationTime, copy.CreationTime);
            Assert.AreEqual(2, copy.Waypoints.Count());
            var wp = copy.Waypoints.First();
            Assert.AreEqual(RouteManager.IdNull, wp.Id);
            Assert.AreEqual(RouteManager.IdNull, wp.RouteId);
            Assert.AreEqual(251D, wp.Altitude);
            Assert.IsNull(copy.Waypoints.Last().Altitude);
            Assert.IsTrue(copy.Waypoints.Last().Ticked);
        }


        [TestMethod]
        public void SettingWaypoints_ReplacesExistingWaypoints()
        {
            var route = new Route();
            route.Add(new Waypoint() { Label = "old" });

            route.Waypoints = new[] { new Waypoint() { Label = "new" } };

            CollectionAssert.AreEqual(new[] { "new" }, route.Waypoints.Select(w => w.Label).ToArray());
        }


        [TestMethod]
        public void LocalTimes_AreConvertedToUtc()
        {
            var local = new DateTime(2024, 7, 1, 12, 0, 0, DateTimeKind.Local);

            var route = new Route() { CreationTime = local };

            Assert.AreEqual(DateTimeKind.Utc, route.CreationTime.Kind);
            Assert.AreEqual(local.ToUniversalTime(), route.CreationTime);
        }
    }
}
