using System;
using System.Linq;

namespace Romer.GeoLib
{
    /// <summary>
    /// Simple geographic areas used to decide where grid systems apply.
    /// These are deliberately coarse: they only need to choose the right grid on land.
    /// </summary>
    internal static class GeoArea
    {
        /// <summary>
        /// Determine if a location lies within a latitude/longitude box. The bounds are inclusive.
        /// </summary>
        /// <param name="location">Location</param>
        /// <param name="minLat">Southern bound, in decimal degrees</param>
        /// <param name="maxLat">Northern bound, in decimal degrees</param>
        /// <param name="minLon">Western bound, in decimal degrees</param>
        /// <param name="maxLon">Eastern bound, in decimal degrees</param>
        /// <returns>True if the location is in the box, otherwise false</returns>
        public static bool InBox(Location location, double minLat, double maxLat, double minLon, double maxLon)
        {
            return location.Latitude >= minLat && location.Latitude <= maxLat &&
                   location.Longitude >= minLon && location.Longitude <= maxLon;
        }


        /// <summary>
        /// Determine if a location lies within a polygon, using ray casting.
        /// Suitable for small polygons that don't cross the antimeridian or a pole.
        /// </summary>
        /// <param name="location">Location</param>
        /// <param name="polygon">Vertices as (longitude, latitude) pairs, in order. The polygon is closed automatically.</param>
        /// <returns>True if the location is in the polygon, otherwise false</returns>
        public static bool InPolygon(Location location, (double Lon, double Lat)[] polygon)
        {
            var inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var (xi, yi) = polygon[i];
                var (xj, yj) = polygon[j];
                if ((yi > location.Latitude) != (yj > location.Latitude) &&
                    location.Longitude < (xj - xi) * (location.Latitude - yi) / (yj - yi) + xi)
                {
                    inside = !inside;
                }
            }
            return inside;
        }


        /// <summary>
        /// Coarse outline of the island of Ireland, including Northern Ireland, which uses the Irish Grid.
        /// It follows the sea between Ireland and Great Britain, passing between Antrim and Kintyre/Islay
        /// and between County Down and Galloway.
        /// </summary>
        private static readonly (double Lon, double Lat)[] _irelandOutline =
        {
            (-10.8, 51.1),   // South-west of Kerry
            (-6.0, 51.9),    // South-east of Wexford
            (-5.8, 53.0),    // East of Wicklow/Dublin
            (-5.3, 54.3),    // East of the Ards peninsula, west of the Isle of Man
            (-5.55, 54.85),  // Between Belfast Lough and the Rhins of Galloway
            (-5.93, 55.15),  // Between the Antrim coast and the Mull of Kintyre
            (-6.3, 55.45),   // North of Rathlin, south of Islay
            (-7.4, 55.6),    // North of Malin Head
            (-8.7, 55.4),    // North-west of Donegal
            (-10.9, 54.4),   // West of Mayo
        };

        /// <summary>
        /// Coarse test for the island of Ireland, including Northern Ireland.
        /// </summary>
        public static bool InIreland(Location location)
        {
            return InPolygon(location, _irelandOutline);
        }


        /// <summary>
        /// Coarse test for the part of northern France that falls inside Great Britain's bounding box.
        /// </summary>
        public static bool InNorthernFrance(Location location)
        {
            // East of Dover (Calais, Boulogne), and the Normandy/Picardy coast. England's south coast is north of
            // 50.7N everywhere east of the Greenwich meridian.
            return (location.Longitude > 1.4 && location.Latitude < 51.1) ||
                   (location.Longitude > 0.0 && location.Latitude < 50.6);
        }


        /// <summary>
        /// Coarse test for the United States and its main Caribbean territories.
        /// The contiguous-states box also contains parts of Canada and Mexico.
        /// </summary>
        public static bool InUnitedStates(Location location)
        {
            return InBox(location, 24.0, 49.5, -125.0, -66.5) ||   // Contiguous states
                   InBox(location, 51.0, 71.5, -180.0, -129.5) ||  // Alaska
                   InBox(location, 51.0, 53.0, 172.0, 180.0) ||    // Western Aleutians
                   InBox(location, 18.5, 22.5, -161.0, -154.5) ||  // Hawaii
                   InBox(location, 17.5, 18.6, -68.0, -64.5);      // Puerto Rico and US Virgin Islands
        }
    }


    /// <summary>
    /// Grid reference precision.
    /// </summary>
    internal static class GridPrecision
    {
        /// <summary>
        /// Get the number of digits to show for each of the easting and northing parts of a five-digit grid reference.
        /// For example 1m precision gives 5 digits, 10m gives 4, and 100m gives 3.
        /// </summary>
        /// <param name="precisionMetres">Precision in metres, in the range 1 to 100000</param>
        /// <returns>Number of digits, in the range 1 to 5</returns>
        public static int DigitsPerPart(int precisionMetres)
        {
            if (precisionMetres < 1 || precisionMetres > 100000)
                throw new ArgumentOutOfRangeException(nameof(precisionMetres), precisionMetres, "Precision must be between 1 and 100000 metres");
            return Math.Max(1, (10000 / precisionMetres).ToString().Length);
        }
    }
}
