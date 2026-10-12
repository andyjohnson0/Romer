using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using SharpGPX;
using SharpGPX.GPX1_1;


namespace Romer.DataLib
{
    /// <summary>
    /// Convert routes to and from GPX.
    /// </summary>
    public static class GpxConverter
    {
        /// <summary>
        /// Maximum number of points imported from a GPX track. Tracks are usually recorded every few seconds and can
        /// have thousands of points, which is too many to use as waypoints.
        /// </summary>
        public const int MaxTrackPoints = 250;


        /// <summary>
        /// Create a route from GPX data.
        /// Uses the file's waypoints (wpt) if it has any, otherwise the points of its first route (rte) that has any,
        /// otherwise the points of its first track (trk) that has any, thinned to at most <see cref="MaxTrackPoints"/>.
        /// The route is named from the file's metadata, otherwise from the route or track that supplied the points.
        /// </summary>
        /// <param name="stm">Stream containing GPX data</param>
        /// <param name="defaultRouteName">Route name to use if the file doesn't name it</param>
        /// <param name="defaultWaypointName">Waypoint name to use for points without one</param>
        /// <returns>Route object. It has no waypoints if the file has no points.</returns>
        /// <exception cref="Exception">The data isn't valid GPX. The exception type depends on the GPX library.</exception>
        public static Route FromGpx(
            Stream stm,
            string defaultRouteName,
            string defaultWaypointName)
        {
            var route = new Route();
            var gpx = GpxClass.FromStream(stm);

            // Choose the points and, if the file has no metadata name, a name for the route
            IList<wptType> points;
            string? sourceName = null;
            if (gpx.Waypoints?.Count > 0)
            {
                points = gpx.Waypoints;
            }
            else if (gpx.Routes?.FirstOrDefault(r => r.rtept?.Count > 0) is rteType gpxRoute)
            {
                points = gpxRoute.rtept;
                sourceName = gpxRoute.name;
            }
            else if (gpx.Tracks?.FirstOrDefault(t => t.trkseg?.Any(s => s.trkpt?.Count > 0) == true) is trkType gpxTrack)
            {
                points = Thin(gpxTrack.trkseg.Where(s => s.trkpt != null).SelectMany(s => s.trkpt).ToList(), MaxTrackPoints);
                sourceName = gpxTrack.name;
            }
            else
            {
                points = Array.Empty<wptType>();
            }

            route.CreationTime = gpx.Metadata?.timeSpecified == true ? gpx.Metadata.time : DateTime.UtcNow;
            route.Label = FirstNonEmpty(gpx.Metadata?.name, sourceName, defaultRouteName);
            route.Notes = gpx.Metadata?.desc ?? string.Empty;

            foreach (var gpxWaypoint in points)
            {
                var waypoint = new Waypoint()
                {
                    CreationTime = gpxWaypoint.timeSpecified ? gpxWaypoint.time : DateTime.UtcNow,
                    Latitude = (double)gpxWaypoint.lat,
                    Longitude = (double)gpxWaypoint.lon,
                    Altitude = gpxWaypoint.eleSpecified ? (double?)gpxWaypoint.ele : (double?)null,
                    Label = FirstNonEmpty(gpxWaypoint.name, defaultWaypointName),
                    Notes = gpxWaypoint.desc ?? string.Empty
                };
                route.Add(waypoint);
            }

            return route;
        }


        /// <summary>
        /// Create a route from GPX data.
        /// </summary>
        /// <param name="gpxStr">GPX/XML data</param>
        /// <param name="defaultRouteName">Route name to use if the data doesn't name it</param>
        /// <param name="defaultWaypointName">Waypoint name to use for points without one</param>
        /// <returns>Route object</returns>
        /// <exception cref="Exception">The data isn't valid GPX</exception>
        public static Route FromGpx(
            string gpxStr,
            string defaultRouteName,
            string defaultWaypointName)
        {
            using (var stm = new MemoryStream(Encoding.UTF8.GetBytes(gpxStr)))
            {
                return FromGpx(stm, defaultRouteName, defaultWaypointName);
            }
        }


        /// <summary>
        /// Write a route to a stream as GPX 1.1. Its waypoints are written as waypoints (wpt).
        /// </summary>
        /// <param name="route">Route to write</param>
        /// <param name="stm">Stream to write the GPX data to</param>
        /// <param name="creator">Value of the gpx element's creator attribute, e.g. app name and version</param>
        /// <param name="waypointIds">IDs of the waypoints to write, or null to write all of them</param>
        public static void ToGpx(
            Route route,
            Stream stm,
            string creator,
            int[]? waypointIds = null)
        {
            var gpx = new GpxClass(GpxVersion.GPX_1_1);
            gpx.Creator = creator;
            gpx.Metadata = new metadataType()
            {
                time = route.CreationTime,
                timeSpecified = true,
                name = route.Label,
                desc = route.Notes
            };
            foreach (var waypoint in route.Waypoints)
            {
                if (waypointIds != null && !waypointIds.Contains(waypoint.Id))
                    continue;  // Omit this waypoint
                var gpxWaypoint = new wptType()
                {
                    time = waypoint.CreationTime,
                    timeSpecified = true,
                    lat = (decimal)waypoint.Latitude,
                    lon = (decimal)waypoint.Longitude,
                    name = waypoint.Label,
                    desc = waypoint.Notes
                };
                // Only set ele when there's an altitude. SharpGPX writes an ele element whenever the property has been
                // set, even to 0 with eleSpecified false, so a missing altitude would be exported as 0m.
                if (waypoint.Altitude.HasValue)
                {
                    gpxWaypoint.ele = (decimal)waypoint.Altitude.Value;
                    gpxWaypoint.eleSpecified = true;
                }
                gpx.AddWaypoint(gpxWaypoint);
            }

            GpxClass.XmlWriterSettings.Encoding = Encoding.UTF8;
            gpx.ToStream(stm);
        }


        /// <summary>
        /// Reduce a list of points to at most maxCount by keeping evenly spaced points, always including the first and last.
        /// </summary>
        private static IList<wptType> Thin(IList<wptType> points, int maxCount)
        {
            if (points.Count <= maxCount)
                return points;
            return Enumerable.Range(0, maxCount)
                             .Select(i => points[(int)Math.Round(i * (points.Count - 1) / (double)(maxCount - 1))])
                             .ToList();
        }


        private static string FirstNonEmpty(params string?[] values)
        {
            return values.First(v => !string.IsNullOrWhiteSpace(v))!;
        }
    }
}
