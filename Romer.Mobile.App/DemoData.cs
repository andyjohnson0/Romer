using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Android.Content;

using Romer.DataLib;
using Romer.CommonLib;


namespace Romer.Mobile.App
{
#if DEBUG

    /// <summary>
    /// Synthetic demo routes for the phone app, for debug builds.
    /// </summary>
    internal static class DemoData
    {
        /// <summary>
        /// Create the demo routes: the shared demo route from <see cref="CommonLib.DemoData"/>, and two routes in the Lake
        /// District whose waypoints are labelled with their grid references.
        /// </summary>
        /// <param name="context">Context</param>
        /// <returns>The routes. They haven't been stored.</returns>
        public static Route[] CreateRoutes(Context context)
        {
            var routes = new List<Route>();

            routes.Add(CommonLib.DemoData.CreateRoute());
            routes.Add(Augment(RouteConverter.FromGpx(context, _testRoute1WtoE)));
            routes.Add(Augment(RouteConverter.FromGpx(context, _testRoute2NtoS)));

            return routes.ToArray();
        }


        private static Route Augment(Route route)
        {
            var locProvider = GridSystemProviderManager.BestInArea(route.Waypoints.ElementAt(0).GetLocation());

            foreach(var waypoint in route.Waypoints)
            {
                waypoint.Label = locProvider.GetGridReference(waypoint.GetLocation(), 10);
            }

            return route;
        }



        // Synthetic demo routes, written for this repository: approximate positions of public places, from general
        // knowledge. Not exported from a mapping app, so they can be published under the repository's licence.

        private static string _testRoute1WtoE =
        @"<?xml version=""1.0"" encoding=""UTF-8""?>
<gpx version=""1.1"" creator=""Romer demo data"" xmlns=""http://www.topografix.com/GPX/1/1"">
    <metadata>
        <name>Test route 1 W to E</name>
        <desc>A west to east route</desc>
    </metadata>
    <wpt lat=""54.3550"" lon=""-3.4080""><name>Ravenglass</name></wpt>
    <wpt lat=""54.3930"" lon=""-3.3350""><name>Eskdale Green</name></wpt>
    <wpt lat=""54.4000"" lon=""-3.2050""><name>Hardknott Pass</name></wpt>
    <wpt lat=""54.4130"" lon=""-3.1280""><name>Wrynose Pass</name></wpt>
    <wpt lat=""54.4170"" lon=""-3.0500""><name>Little Langdale</name></wpt>
    <wpt lat=""54.4290"" lon=""-2.9630""><name>Ambleside</name></wpt>
    <wpt lat=""54.4170"" lon=""-2.9100""><name>Troutbeck</name></wpt>
    <wpt lat=""54.4300"" lon=""-2.8400""><name>Kentmere</name></wpt>
    <wpt lat=""54.4620"" lon=""-2.7540""><name>Longsleddale</name></wpt>
    <wpt lat=""54.5310"" lon=""-2.6800""><name>Shap</name></wpt>
</gpx>";

        private static string _testRoute2NtoS =
        @"<?xml version=""1.0"" encoding=""UTF-8""?>
<gpx version=""1.1"" creator=""Romer demo data"" xmlns=""http://www.topografix.com/GPX/1/1"">
    <metadata>
        <name>Test route 2 N to S</name>
        <desc>A north to south route</desc>
    </metadata>
    <wpt lat=""54.6500"" lon=""-3.0900""><name>Threlkeld</name></wpt>
    <wpt lat=""54.6010"" lon=""-3.1340""><name>Keswick</name></wpt>
    <wpt lat=""54.5540"" lon=""-3.1520""><name>Grange in Borrowdale</name></wpt>
    <wpt lat=""54.5120"" lon=""-3.1680""><name>Seatoller</name></wpt>
    <wpt lat=""54.4840"" lon=""-3.1480""><name>Stake Pass</name></wpt>
    <wpt lat=""54.4480"" lon=""-3.0960""><name>Dungeon Ghyll</name></wpt>
    <wpt lat=""54.4170"" lon=""-3.0500""><name>Little Langdale</name></wpt>
    <wpt lat=""54.3690"" lon=""-3.0750""><name>Coniston</name></wpt>
    <wpt lat=""54.3150"" lon=""-3.1520""><name>Torver</name></wpt>
    <wpt lat=""54.2790"" lon=""-3.2110""><name>Broughton-in-Furness</name></wpt>
</gpx>";
    }
  
#endif
}
