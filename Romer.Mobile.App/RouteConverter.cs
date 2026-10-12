using System;
using System.IO;

using Android.Content;

using Romer.DataLib;
using Romer.CommonLib;


namespace Romer.Mobile.App
{
    /// <summary>
    /// Convert a Route to and from GPX, using the app's resources for default names and the creator.
    /// See <see cref="GpxConverter"/> for the conversion rules.
    /// </summary>
    public static class RouteConverter
    {
        /// <summary>
        /// Create a route from GPX data.
        /// </summary>
        /// <param name="context">Current context</param>
        /// <param name="stm">Stream containing GPX data</param>
        /// <returns>Route object</returns>
        /// <exception cref="Exception">The data isn't valid GPX</exception>
        public static Route FromGpx(
            Context context,
            Stream stm)
        {
            return GpxConverter.FromGpx(stm, DefaultRouteName(context), DefaultWaypointName(context));
        }


        /// <summary>
        /// Create a route from GPX data.
        /// </summary>
        /// <param name="context">Current context</param>
        /// <param name="gpxStr">GPX/XML data</param>
        /// <returns>Route object</returns>
        /// <exception cref="Exception">The data isn't valid GPX</exception>
        public static Route FromGpx(
            Context context,
            string gpxStr)
        {
            return GpxConverter.FromGpx(gpxStr, DefaultRouteName(context), DefaultWaypointName(context));
        }


        /// <summary>
        /// Write the route to the supplied stream in GPX format.
        /// The creator attribute is the app's name and version.
        /// </summary>
        /// <param name="context">Current context</param>
        /// <param name="route">Route to convert to GPX</param>
        /// <param name="stm">Stream to serialise the route to</param>
        /// <param name="waypointIds">IDs of the waypoints to write, or null to write all of them</param>
        public static void ToGpx(
            Context context,
            Route route,
            Stream stm,
            int[]? waypointIds = null)
        {
            var creator = context.GetString(Resource.String.appName) + " v" + context.GetAppVersionName();
            GpxConverter.ToGpx(route, stm, creator, waypointIds);
        }


        private static string DefaultRouteName(Context context)
        {
            return context.GetString(Resource.String.newRouteName);
        }


        private static string DefaultWaypointName(Context context)
        {
            return context.RequireResources().GetQuantityString(Resource.Plurals.waypoint, 1);
        }
    }
}
