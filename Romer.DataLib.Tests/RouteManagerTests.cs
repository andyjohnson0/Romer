using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Romer.DataLib.Tests
{
    [TestClass]
    public class RouteManagerTests
    {
        private string _dataDir = null!;
        private RouteManager _mgr = null!;


        [TestInitialize]
        public void Initialise()
        {
            this._dataDir = Path.Combine(Path.GetTempPath(), "Romer.DataLib.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this._dataDir);
            this._mgr = new RouteManager(this._dataDir);
        }


        [TestCleanup]
        public void Cleanup()
        {
            // sqlite may hold the file briefly after the last connection closes.
            try { Directory.Delete(this._dataDir, recursive: true); } catch (IOException) { }
        }


        private static Route CreateRoute(string label, int numWaypoints)
        {
            var route = new Route() { Label = label, Notes = label + " notes", CreationTime = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc) };
            for (var i = 0; i < numWaypoints; i++)
            {
                route.Add(new Waypoint() { Label = $"wp{i}", Latitude = 57D + i / 100D, Longitude = -3D - i / 100D, Altitude = 100D * i });
            }
            return route;
        }


        [TestMethod]
        public void CreateAndGetRoute_RoundTripsRouteAndWaypointsInOrder()
        {
            var routeId = _mgr.CreateRoute(CreateRoute("Route A", 5));

            var route = _mgr.GetRoute(routeId);

            Assert.IsNotNull(route);
            Assert.AreEqual(routeId, route.Id);
            Assert.AreEqual("Route A", route.Label);
            Assert.AreEqual("Route A notes", route.Notes);
            CollectionAssert.AreEqual(new[] { "wp0", "wp1", "wp2", "wp3", "wp4" }, route.Waypoints.Select(w => w.Label).ToArray());
            Assert.IsTrue(route.Waypoints.All(w => w.RouteId == routeId));
            Assert.AreEqual(5, _mgr.GetWaypointsCount(routeId));
        }


        [TestMethod]
        public void GetRoute_ReturnsNullForUnknownRoute()
        {
            Assert.IsNull(_mgr.GetRoute(12345));
        }


        [TestMethod]
        public void GetOrCreateRoute_HandlesLabelsContainingQuotes()
        {
            var route = _mgr.GetOrCreateRoute(1, overwrite: false, label: "Arthur's Seat");

            Assert.AreEqual(1, route.Id);
            Assert.AreEqual("Arthur's Seat", route.Label);
        }


        [TestMethod]
        public void GetOrCreateRoute_SetsCurrentCreationTime()
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            var route = _mgr.GetOrCreateRoute(1);
            var after = DateTime.UtcNow.AddSeconds(1);

            Assert.IsTrue(route.CreationTime >= before && route.CreationTime <= after, $"Unexpected creation time {route.CreationTime:o}");
        }


        [TestMethod]
        public void GetOrCreateRoute_KeepsExistingRouteUnlessOverwriting()
        {
            var route = _mgr.GetOrCreateRoute(1, label: "Original");
            _mgr.AddWaypoint(route.Id, new Waypoint() { Label = "wp" });

            var again = _mgr.GetOrCreateRoute(1, label: "Ignored");
            Assert.AreEqual("Original", again.Label);
            Assert.AreEqual(1, again.Waypoints.Count());

            var replaced = _mgr.GetOrCreateRoute(1, overwrite: true, label: "Replacement");
            Assert.AreEqual("Replacement", replaced.Label);
            Assert.AreEqual(0, replaced.Waypoints.Count());
        }


        [TestMethod]
        public void UpdateRoute_PersistsReversedWaypointOrder()
        {
            var routeId = _mgr.CreateRoute(CreateRoute("Route", 4));

            var route = _mgr.GetRoute(routeId)!;
            route.ReverseWaypoints();
            _mgr.UpdateRoute(route);

            var reloaded = _mgr.GetRoute(routeId)!;
            CollectionAssert.AreEqual(new[] { "wp3", "wp2", "wp1", "wp0" }, reloaded.Waypoints.Select(w => w.Label).ToArray());
            Assert.AreEqual(4, _mgr.GetWaypointsCount(routeId));
        }


        [TestMethod]
        public void UpdateRoute_DeletesWaypointsRemovedFromTheRoute()
        {
            var routeId = _mgr.CreateRoute(CreateRoute("Route", 3));

            var route = _mgr.GetRoute(routeId)!;
            route.Waypoints = route.Waypoints.Where(w => w.Label != "wp1").ToList();
            _mgr.UpdateRoute(route);

            var reloaded = _mgr.GetRoute(routeId)!;
            CollectionAssert.AreEqual(new[] { "wp0", "wp2" }, reloaded.Waypoints.Select(w => w.Label).ToArray());
        }


        [TestMethod]
        public void UpdateRoute_AppendsNewWaypoints()
        {
            var routeId = _mgr.CreateRoute(CreateRoute("Route", 2));

            var route = _mgr.GetRoute(routeId)!;
            route.Add(new Waypoint() { Label = "new" });
            _mgr.UpdateRoute(route);

            var reloaded = _mgr.GetRoute(routeId)!;
            CollectionAssert.AreEqual(new[] { "wp0", "wp1", "new" }, reloaded.Waypoints.Select(w => w.Label).ToArray());
        }


        [TestMethod]
        public void UpdateRoute_CreatesRouteThatDoesNotExist()
        {
            var route = CreateRoute("New", 2);

            var routeId = _mgr.UpdateRoute(route);

            Assert.AreEqual(routeId, route.Id);
            Assert.AreEqual(2, _mgr.GetRoute(routeId)!.Waypoints.Count());
        }


        [TestMethod]
        public void DeleteRoute_DeletesRouteAndItsWaypointsOnly()
        {
            var id1 = _mgr.CreateRoute(CreateRoute("One", 2));
            var id2 = _mgr.CreateRoute(CreateRoute("Two", 3));

            _mgr.DeleteRoute(id1);

            Assert.IsNull(_mgr.GetRoute(id1));
            Assert.AreEqual(0, _mgr.GetWaypointsCount(id1));
            Assert.AreEqual(3, _mgr.GetWaypointsCount(id2));
            CollectionAssert.AreEqual(new[] { id2 }, _mgr.GetRouteIds());
        }


        [TestMethod]
        public void WaypointQueries_RespectRouteAndOrder()
        {
            var routeId = _mgr.CreateRoute(CreateRoute("Route", 3));
            var otherId = _mgr.CreateRoute(CreateRoute("Other", 2));
            var ids = _mgr.GetWaypointIds(routeId);

            Assert.AreEqual(3, ids.Length);
            Assert.AreEqual("wp1", _mgr.GetWaypointAt(routeId, 1).Label);
            Assert.AreEqual("wp2", _mgr.GetWaypoint(routeId, ids[2])!.Label);
            Assert.IsNull(_mgr.GetWaypoint(otherId, ids[2]));
        }


        [TestMethod]
        public void TickedWaypointCount_CountsOnlyTickedWaypoints()
        {
            var routeId = _mgr.CreateRoute(CreateRoute("Route", 3));
            var wp = _mgr.GetWaypointAt(routeId, 2);
            wp.Ticked = true;
            _mgr.UpdateWaypoint(wp);

            Assert.AreEqual(1, _mgr.GetTickedWaypointsCount(routeId));
        }


        [TestMethod]
        public void DeleteWaypointOperations()
        {
            var routeId = _mgr.CreateRoute(CreateRoute("Route", 3));

            _mgr.DeleteLastWaypoint(routeId);
            CollectionAssert.AreEqual(new[] { "wp0", "wp1" }, _mgr.GetRoute(routeId)!.Waypoints.Select(w => w.Label).ToArray());

            _mgr.DeleteWaypoint(routeId, _mgr.GetWaypointIds(routeId)[0]);
            CollectionAssert.AreEqual(new[] { "wp1" }, _mgr.GetRoute(routeId)!.Waypoints.Select(w => w.Label).ToArray());

            _mgr.DeleteAllWaypoints(routeId);
            Assert.AreEqual(0, _mgr.GetWaypointsCount(routeId));
            Assert.IsNotNull(_mgr.GetRoute(routeId));

            // Deleting from an empty route is a no-op
            _mgr.DeleteLastWaypoint(routeId);
        }


        [TestMethod]
        public void FindRoutes_MatchesLabel()
        {
            _mgr.CreateRoute(CreateRoute("Same", 0));
            _mgr.CreateRoute(CreateRoute("Same", 0));
            _mgr.CreateRoute(CreateRoute("Different", 0));

            Assert.AreEqual(2, _mgr.FindRoutes("Same").Length);
            Assert.AreEqual(0, _mgr.FindRoutes("None").Length);
        }


        [TestMethod]
        public void Times_AreReturnedAsUtc()
        {
            var created = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);
            var routeId = _mgr.CreateRoute(CreateRoute("Route", 1));

            var route = _mgr.GetRoute(routeId)!;

            Assert.AreEqual(DateTimeKind.Utc, route.CreationTime.Kind);
            Assert.AreEqual(created, route.CreationTime);
        }
    }
}
