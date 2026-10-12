using System;
using System.Collections.Generic;
using System.Text;

namespace Romer.GeoLib
{
    /// <summary>
    /// Geographic calculations.
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Calculate the great-circle distance in kilometres between two locations, using the haversine formula.
        /// Altitudes are ignored.
        /// </summary>
        /// <param name="location1">Location 1</param>
        /// <param name="location2">Location 2</param>
        /// <returns>Distance between the locations, in kilometres</returns>
        public static double HaversineKm(Location location1, Location location2)
        {
            return HaversineKm(lon1: location1.Longitude, lat1: location1.Latitude, lon2: location2.Longitude, lat2: location2.Latitude);
        }


        /// <summary>
        /// Calculate the great-circle distance in kilometres between two points, using the haversine formula.
        /// </summary>
        /// <param name="lon1">Point 1 longitude in decimal degrees</param>
        /// <param name="lat1">Point 1 latitude in decimal degrees</param>
        /// <param name="lon2">Point 2 longitude in decimal degrees</param>
        /// <param name="lat2">Point 2 latitude in decimal degrees</param>
        /// <returns>Distance between the points, in kilometres</returns>
        public static double HaversineKm(double lon1, double lat1, double lon2, double lat2)
        {
            // Convert decimal degrees to radians
            lon1 = DegreesToRadians(lon1);
            lat1 = DegreesToRadians(lat1);
            lon2 = DegreesToRadians(lon2);
            lat2 = DegreesToRadians(lat2);

            // Haversine formula
            double dlon = lon2 - lon1;
            double dlat = lat2 - lat1;
            double a = Math.Pow(Math.Sin(dlat / 2), 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dlon / 2), 2);
            double c = 2 * Math.Asin(Math.Sqrt(a));

            // Radius of earth in kilometers is 6371
            double km = 6371 * c;
            return km;
        }


        /// <summary>
        /// Convert degrees to radians
        /// </summary>
        /// <param name="degrees">Angle in degrees</param>
        /// <returns>Angle in radians</returns>
        public static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }
}
