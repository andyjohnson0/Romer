using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Android.Content;
using Android.Content.Res;

using Romer.DataLib;


namespace Romer.CommonLib
{
#if DEBUG

    /// <summary>
    /// Synthetic demo data, for debug builds.
    /// </summary>
    public static class DemoData
    {
        /// <summary>
        /// Create a demo route: a circuit in the Cairngorms, with altitudes and times. Each waypoint is labelled with
        /// its grid reference.
        /// </summary>
        /// <returns>The route. It hasn't been stored, so its ID is <see cref="RouteManager.IdNull"/>.</returns>
        public static Route CreateRoute()
        {
            // Synthetic demo route, written for this repository: approximate positions of public places, from general
            // knowledge. Summit elevations are published heights; the others are made up.
            var route = new Route()
            {
                CreationTime = new DateTime(2023, 6, 2, 10, 00, 00),
                Label = "Demo Route",
                Notes = "These are the notes for the demo route"
            };
            route.Add(CreateWaypoint(time: new DateTime(2023, 6, 2, 10, 05, 00), latitude: 57.1340, longitude: -3.6700, altitude: 630D));   // Coire Cas car park
            route.Add(CreateWaypoint(time: new DateTime(2023, 6, 2, 10, 50, 00), latitude: 57.1130, longitude: -3.6650, altitude: 950D));   // Coire an t-Sneachda
            route.Add(CreateWaypoint(time: new DateTime(2023, 6, 2, 11, 45, 00), latitude: 57.1050, longitude: -3.6800, altitude: 1215D));  // Cairn Lochan
            route.Add(CreateWaypoint(time: new DateTime(2023, 6, 2, 12, 20, 00), latitude: 57.0920, longitude: -3.6650, altitude: 1110D));  // Lochan Buidhe
            route.Add(CreateWaypoint(time: new DateTime(2023, 6, 2, 13, 05, 00), latitude: 57.0700, longitude: -3.6690, altitude: 1309D));  // Ben Macdui
            route.Add(CreateWaypoint(time: new DateTime(2023, 6, 2, 14, 00, 00), latitude: 57.0830, longitude: -3.6380, altitude: 927D));   // Loch Etchachan
            route.Add(CreateWaypoint(time: new DateTime(2023, 6, 2, 15, 15, 00), latitude: 57.1170, longitude: -3.6440, altitude: 1245D));  // Cairn Gorm
            route.Add(CreateWaypoint(time: new DateTime(2023, 6, 2, 15, 40, 00), latitude: 57.1240, longitude: -3.6530, altitude: 1097D));  // Ptarmigan
            route.Add(CreateWaypoint(time: new DateTime(2023, 6, 2, 16, 30, 00), latitude: 57.1340, longitude: -3.6700, altitude: 630D));   // Coire Cas car park

            return route;
        }


        private static Waypoint CreateWaypoint(
            DateTime time,
            double latitude,
            double longitude,
            double altitude)
        {
            var loc = new GeoLib.Location()
            {
                Latitude = latitude,
                Longitude = longitude,
                Altitude = altitude
            };
            var gsp = GridSystemProviderManager.BestInArea(loc);
            var gridRef = gsp.GetGridReference(loc, 100);

            var waypoint = new Waypoint
            {
                CreationTime = time,
                Latitude = latitude,
                Longitude = longitude,
                Altitude = altitude,
                Label = gridRef,
                Notes = $"These are the notes for the waypoint at {gridRef} created at {time}"
            };
            return waypoint;
        }
    }

#endif // DEBUG
}
