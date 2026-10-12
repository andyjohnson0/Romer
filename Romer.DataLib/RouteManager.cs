using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using SQLite;



namespace Romer.DataLib
{
    /// <summary>
    /// Stores routes and their waypoints in a SQLite database.
    /// Each public method uses its own short-lived connection, and methods that make more than one
    /// change do so in a single transaction.
    /// </summary>
    public class RouteManager
    {
        /// <summary>
        /// ID of a route or waypoint that hasn't been stored.
        /// </summary>
        public const int IdNull = -1;

        /// <summary>
        /// Constructor. Initialise a RouteManager object.
        /// </summary>
        /// <param name="dataDir">Existing directory that holds the database file</param>
        /// <exception cref="ArgumentException">The directory isn't specified or doesn't exist</exception>
        public RouteManager(
            string dataDir)
        {
            if (string.IsNullOrEmpty(dataDir))
                throw new ArgumentException("Data directory not specified", nameof(dataDir));
            if (!Directory.Exists(dataDir))
                throw new ArgumentException($"Data directory does not exist: {dataDir}", nameof(dataDir));

            this._dbPath = Path.Combine(dataDir, "routes1.db");
        }


        private readonly string _dbPath;

        // Database paths whose tables have been created by this process
        private static readonly HashSet<string> _initialisedDbPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);


        /// <summary>
        /// Get a database connection, creating the tables if this is the first connection to the database.
        /// </summary>
        /// <returns>Database connection</returns>
        private SQLiteConnection CreateConnection()
        {
            var db = new SQLiteConnection(_dbPath);
            lock (_initialisedDbPaths)
            {
                if (!_initialisedDbPaths.Contains(_dbPath))
                {
                    db.CreateTable<Route>();
                    db.CreateTable<Waypoint>();
                    _initialisedDbPaths.Add(_dbPath);
                }
            }
            return db;
        }


        /// <summary>
        /// Get the specified route, creating it if it does not exist or if the overwrite parameter is true.
        /// Intended for use by clients that deal with only a single route.
        /// </summary>
        /// <param name="routeId">ID of route to get or create</param>
        /// <param name="overwrite">Replace the route, and delete its waypoints, if it already exists</param>
        /// <param name="label">Label for a newly created route. If null then "New Route" is used.</param>
        /// <returns>The route</returns>
        public Route GetOrCreateRoute(
            int routeId,
            bool overwrite = false,
            string? label = null)
        {
            using (var db = CreateConnection())
            {
                db.RunInTransaction(() =>
                {
                    if (overwrite)
                    {
                        DeleteRoute(db, routeId);
                    }

                    if (db.Table<Route>().Count(r => r.Id == routeId) == 0)
                    {
                        // Insert with an explicit ID. Insert() would ignore the ID because it is auto-increment.
                        db.Execute("INSERT INTO Routes (_id, CreationTime, Label, Notes) VALUES (?, ?, ?, ?)",
                                   routeId, DateTime.UtcNow, label ?? "New Route", string.Empty);
                    }
                });
                return GetRoute(db, routeId)!;
            }
        }


        #region Routes

        /// <summary>
        /// Get all route IDs
        /// </summary>
        /// <returns>Array of route IDs. Can be empty. Never null.</returns>
        public int[] GetRouteIds()
        {
            using (var db = CreateConnection())
            {
                return db.Table<Route>().OrderBy(r => r.Id).Select(r => r.Id).ToArray();
            }
        }


        /// <summary>
        /// Get a route and its waypoints
        /// </summary>
        /// <param name="routeId">route ID</param>
        /// <returns>The route, or null if the route does not exist</returns>
        public Route? GetRoute(int routeId)
        {
            using (var db = CreateConnection())
            {
                return GetRoute(db, routeId);
            }
        }


        private static Route? GetRoute(SQLiteConnection db, int routeId)
        {
            var route = db.Table<Route>().Where(r => r.Id == routeId).FirstOrDefault();
            if (route != null)
            {
                route.Waypoints = GetWaypoints(db, routeId);
            }
            return route;
        }


        /// <summary>
        /// Find all routes with labels that match the supplied label exactly.
        /// The returned routes' waypoints are not loaded.
        /// </summary>
        /// <param name="label">Label</param>
        /// <returns>Zero or more matching routes</returns>
        public Route[] FindRoutes(string label)
        {
            using (var db = CreateConnection())
            {
                return db.Table<Route>().Where(r => r.Label == label).ToArray();
            }
        }


        /// <summary>
        /// Store a new route and its waypoints.
        /// On return the Id properties of the route and its waypoints are set.
        /// </summary>
        /// <param name="route">Route to add.</param>
        /// <returns>Route ID</returns>
        public int CreateRoute(
            Route route)
        {
            using (var db = CreateConnection())
            {
                db.RunInTransaction(() => CreateRoute(db, route));
                return route.Id;
            }
        }


        private static void CreateRoute(SQLiteConnection db, Route route)
        {
            db.Insert(route);  // Sets route.Id
            foreach (var waypoint in route.Waypoints)
            {
                InsertWaypoint(db, route.Id, waypoint);
            }
        }


        /// <summary>
        /// Update a route with supplied values, including its waypoints and their order.
        /// Stored waypoints that are no longer in the route are deleted, and new waypoints are added.
        /// If the route does not exist then it is created.
        /// On return the Id properties of the route and its waypoints are set.
        /// </summary>
        /// <param name="route">Route values.</param>
        /// <returns>Route ID</returns>
        public int UpdateRoute(Route route)
        {
            using (var db = CreateConnection())
            {
                db.RunInTransaction(() =>
                {
                    var routeExists = route.Id != IdNull && db.Table<Route>().Count(r => r.Id == route.Id) > 0;
                    if (!routeExists)
                    {
                        CreateRoute(db, route);
                        return;
                    }

                    db.Update(route);

                    // Waypoints are ordered by ID, so we can only update in place if the route's stored waypoints
                    // are still in ascending ID order and any new waypoints come after them. Otherwise re-insert
                    // them all to get new IDs in the right order.
                    var storedIds = db.Table<Waypoint>().Where(w => w.RouteId == route.Id).Select(w => w.Id).ToHashSet();
                    var waypoints = route.Waypoints.ToList();
                    var canUpdateInPlace = true;
                    var prevId = int.MinValue;
                    var seenNew = false;
                    foreach (var waypoint in waypoints)
                    {
                        if (storedIds.Contains(waypoint.Id))
                        {
                            if (seenNew || waypoint.Id <= prevId)
                            {
                                canUpdateInPlace = false;
                                break;
                            }
                            prevId = waypoint.Id;
                        }
                        else
                        {
                            seenNew = true;
                        }
                    }

                    if (canUpdateInPlace)
                    {
                        var keptIds = waypoints.Select(w => w.Id).ToHashSet();
                        foreach (var id in storedIds.Where(id => !keptIds.Contains(id)))
                        {
                            db.Delete<Waypoint>(id);
                        }
                        foreach (var waypoint in waypoints)
                        {
                            if (storedIds.Contains(waypoint.Id))
                                db.Update(waypoint);
                            else
                                InsertWaypoint(db, route.Id, waypoint);
                        }
                    }
                    else
                    {
                        DeleteAllWaypoints(db, route.Id);
                        foreach (var waypoint in waypoints)
                        {
                            InsertWaypoint(db, route.Id, waypoint);
                        }
                    }
                });
            }

            return route.Id;
        }


        /// <summary>
        /// Delete a route and its waypoints
        /// </summary>
        /// <param name="routeId">Route ID</param>
        public void DeleteRoute(int routeId)
        {
            using (var db = CreateConnection())
            {
                db.RunInTransaction(() => DeleteRoute(db, routeId));
            }
        }


        private static void DeleteRoute(SQLiteConnection db, int routeId)
        {
            DeleteAllWaypoints(db, routeId);
            db.Execute("DELETE FROM Routes WHERE _id = ?", routeId);
        }

        #endregion Routes


        #region Waypoints

        private static List<Waypoint> GetWaypoints(SQLiteConnection db, int routeId)
        {
            return db.Table<Waypoint>().Where(w => w.RouteId == routeId).OrderBy(w => w.Id).ToList();
        }


        private static void InsertWaypoint(SQLiteConnection db, int routeId, Waypoint waypoint)
        {
            waypoint.RouteId = routeId;
            db.Insert(waypoint);  // Sets waypoint.Id
        }


        /// <summary>
        /// Get a waypoint
        /// </summary>
        /// <param name="routeId">Route ID</param>
        /// <param name="waypointId">Waypoint ID</param>
        /// <returns>The waypoint, or null if the route does not contain the waypoint</returns>
        public Waypoint? GetWaypoint(
            int routeId,
            int waypointId)
        {
            using (var db = CreateConnection())
            {
                return db.Table<Waypoint>().Where(w => w.RouteId == routeId && w.Id == waypointId).FirstOrDefault();
            }
        }


        /// <summary>
        /// Get number of waypoints in a route
        /// </summary>
        /// <param name="routeId">Route ID</param>
        /// <returns>Number of waypoints</returns>
        public int GetWaypointsCount(int routeId)
        {
            using (var db = CreateConnection())
            {
                return db.Table<Waypoint>().Count(w => w.RouteId == routeId);
            }
        }


        /// <summary>
        /// Get number of ticked waypoints in a route
        /// </summary>
        /// <param name="routeId">Route ID</param>
        /// <returns>Number of ticked waypoints</returns>
        public int GetTickedWaypointsCount(int routeId)
        {
            using (var db = CreateConnection())
            {
                return db.Table<Waypoint>().Count(w => w.RouteId == routeId && w.Ticked);
            }
        }


        /// <summary>
        /// Get the IDs of a route's waypoints, in route order
        /// </summary>
        /// <param name="routeId">Route ID</param>
        /// <returns>
        /// Waypoint IDs.
        /// If the route contains no waypoints then an empty array is returned.
        /// </returns>
        public int[] GetWaypointIds(int routeId)
        {
            using (var db = CreateConnection())
            {
                return db.Table<Waypoint>().Where(w => w.RouteId == routeId).OrderBy(w => w.Id).Select(w => w.Id).ToArray();
            }
        }


        /// <summary>
        /// Update a waypoint with supplied values.
        /// If the waypoint does not exist then it is added to the end of the route identified by its RouteId property.
        /// </summary>
        /// <param name="waypoint">Waypoint values</param>
        /// <returns>Waypoint ID</returns>
        public int UpdateWaypoint(Waypoint waypoint)
        {
            using (var db = CreateConnection())
            {
                var waypointExists = waypoint.Id != IdNull && db.Table<Waypoint>().Count(w => w.Id == waypoint.Id) > 0;
                if (waypointExists)
                {
                    db.Update(waypoint);
                }
                else
                {
                    db.Insert(waypoint);
                }
            }

            return waypoint.Id;
        }


        /// <summary>
        /// Add a waypoint to the end of a route
        /// </summary>
        /// <param name="routeId">Route ID</param>
        /// <param name="waypoint">Waypoint</param>
        /// <returns>Waypoint ID</returns>
        public int AddWaypoint(
            int routeId,
            Waypoint waypoint)
        {
            using (var db = CreateConnection())
            {
                InsertWaypoint(db, routeId, waypoint);
                return waypoint.Id;
            }
        }


        /// <summary>
        /// Get the waypoint at an index in a route
        /// </summary>
        /// <param name="routeId">Route ID</param>
        /// <param name="index">Zero-based index</param>
        /// <returns>Waypoint at the index</returns>
        /// <exception cref="ArgumentOutOfRangeException">Index is out of range</exception>
        public Waypoint GetWaypointAt(
            int routeId,
            int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), "Waypoint index must be >= 0");

            using (var db = CreateConnection())
            {
                return db.Table<Waypoint>().Where(w => w.RouteId == routeId).OrderBy(w => w.Id).Skip(index).FirstOrDefault()
                    ?? throw new ArgumentOutOfRangeException(nameof(index), $"Route {routeId} has no waypoint at index {index}");
            }
        }


        /// <summary>
        /// Delete waypoint in a route
        /// </summary>
        /// <param name="routeId">Route ID</param>
        /// <param name="waypointId">Waypoint ID</param>
        public void DeleteWaypoint(
            int routeId,
            int waypointId)
        {
            using (var db = CreateConnection())
            {
                db.Execute("DELETE FROM Waypoints WHERE RouteId = ? AND _id = ?", routeId, waypointId);
            }
        }


        /// <summary>
        /// Delete the last waypoint in a route.
        /// Does nothing if the route has no waypoints.
        /// </summary>
        /// <param name="routeId">Route ID</param>
        public void DeleteLastWaypoint(int routeId)
        {
            using (var db = CreateConnection())
            {
                db.Execute("DELETE FROM Waypoints WHERE _id = (SELECT MAX(_id) FROM Waypoints WHERE RouteId = ?)", routeId);
            }
        }


        /// <summary>
        /// Delete all the waypoints in a route, but not the route itself.
        /// </summary>
        /// <param name="routeId">Route ID</param>
        public void DeleteAllWaypoints(int routeId)
        {
            using (var db = CreateConnection())
            {
                DeleteAllWaypoints(db, routeId);
            }
        }


        private static void DeleteAllWaypoints(SQLiteConnection db, int routeId)
        {
            db.Execute("DELETE FROM Waypoints WHERE RouteId = ?", routeId);
        }

        #endregion Waypoints


#if DEBUG

        /// <summary>
        /// Describe the database's contents: the routes, their waypoint IDs, and any waypoints without a route.
        /// Debug builds only.
        /// </summary>
        /// <returns>Description, one item per line</returns>
        public string Dump()
        {
            using (var wtr = new StringWriter())
            {
                Dump(wtr);
                return wtr.ToString();
            }
        }


        /// <summary>
        /// Write a description of the database's contents: the routes, their waypoint IDs, and any waypoints without a
        /// route. Debug builds only.
        /// </summary>
        /// <param name="wtr">Writer to write the description to, one item per line</param>
        public void Dump(TextWriter wtr)
        {
            using (var db = CreateConnection())
            {
                var routes = db.Table<Route>().OrderBy(r => r.Id).ToList();
                var waypoints = db.Table<Waypoint>().OrderBy(w => w.Id).ToList();
                wtr.WriteLine($"Found {routes.Count} routes and {waypoints.Count} waypoints");
                foreach (var route in routes)
                {
                    var routeWaypointIds = waypoints.Where(w => w.RouteId == route.Id).Select(w => w.Id).ToArray();
                    wtr.WriteLine($"Route {route.Id} contains {routeWaypointIds.Length} waypoints: [{string.Join(',', routeWaypointIds)}]");
                }
                var routeIds = routes.Select(r => r.Id).ToHashSet();
                var orphanIds = waypoints.Where(w => !routeIds.Contains(w.RouteId)).Select(w => w.Id).ToArray();
                wtr.WriteLine($"Found {orphanIds.Length} orphan waypoints: [{string.Join(',', orphanIds)}]");
            }
        }

#endif
    }
}
