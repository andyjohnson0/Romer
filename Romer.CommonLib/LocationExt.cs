using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using Romer.GeoLib;
using Romer.DataLib;

namespace Romer.CommonLib
{
    /// <summary>
    /// Conversions between <see cref="Android.Locations.Location"/>, <see cref="Romer.GeoLib.Location"/>, and
    /// <see cref="Waypoint"/>.
    /// Each is an extension method on the target type, whose value is ignored, e.g.
    /// <c>new GeoLib.Location().From(waypoint)</c>.
    /// </summary>
    public static class LocationExt
    {
        /// <summary>
        /// Convert from Android Location to GeoLib Location.
        /// The time is the fix time, in UTC.
        /// </summary>
        /// <param name="self">Ignored</param>
        /// <param name="from">Android Location object</param>
        /// <returns>GeoLib Location struct</returns>
        /// <exception cref="ArgumentNullException">Null argument</exception>
        public static Romer.GeoLib.Location From(
            this Romer.GeoLib.Location self,
            Android.Locations.Location from)
        {
            if (from == null)
                throw new ArgumentNullException(nameof(from));

            var to = new Romer.GeoLib.Location
            {
                Time = (DateTimeOffset.FromUnixTimeMilliseconds(from.Time)).UtcDateTime,
                Latitude = from.Latitude,
                Longitude = from.Longitude,
                Altitude = from.HasAltitude ? from.Altitude : (double?)null
            };
            return to;
        }


        /// <summary>
        /// Convert from DataLib Waypoint to GeoLib Location.
        /// The time is the waypoint's creation time.
        /// </summary>
        /// <param name="self">Ignored</param>
        /// <param name="from">DataLib Waypoint</param>
        /// <returns>GeoLib Location struct</returns>
        /// <exception cref="ArgumentNullException">Null argument</exception>
        public static Romer.GeoLib.Location From(
            this Romer.GeoLib.Location self,
            Romer.DataLib.Waypoint from)
        {
            if (from == null)
                throw new ArgumentNullException(nameof(from));

            var to = new Romer.GeoLib.Location
            {
                Time = from.CreationTime,
                Latitude = from.Latitude,
                Longitude = from.Longitude,
                Altitude = from.Altitude
            };
            return to;
        }



        /// <summary>
        /// Convert from GeoLib Location to Android Location.
        /// The time is the location's time, or the current time if it has none. The provider name is empty.
        /// </summary>
        /// <param name="self">Ignored</param>
        /// <param name="from">GeoLib Location struct</param>
        /// <returns>Android Location object</returns>
        public static Android.Locations.Location From(
            this Android.Locations.Location self,
            Romer.GeoLib.Location from)
        {
            var to = new Android.Locations.Location("")
            {
                Time = new DateTimeOffset(from.Time ?? DateTime.UtcNow).ToUnixTimeMilliseconds(),
                Latitude = from.Latitude,
                Longitude = from.Longitude
            };
            if (from.Altitude.HasValue)
                to.Altitude = from.Altitude.Value;
            return to;
        }



        /// <summary>
        /// Convert from DataLib Waypoint to Android Location.
        /// The time is the waypoint's creation time. The provider name is empty.
        /// </summary>
        /// <param name="self">Ignored</param>
        /// <param name="from">DataLib Waypoint</param>
        /// <returns>Android Location object</returns>
        public static Android.Locations.Location From(
            this Android.Locations.Location self,
            Romer.DataLib.Waypoint from)
        {
            var to = new Android.Locations.Location("")
            {
                Time = ((DateTimeOffset)from.CreationTime).ToUnixTimeMilliseconds(),
                Latitude = from.Latitude,
                Longitude = from.Longitude
            };
            if (from.Altitude.HasValue)
                to.Altitude = from.Altitude.Value;
            return to;
        }



        /// <summary>
        /// Convert from GeoLib Location to DataLib Waypoint.
        /// The creation time is the location's time, or the current time if it has none. The label and notes are empty.
        /// </summary>
        /// <param name="self">Ignored</param>
        /// <param name="from">GeoLib Location struct</param>
        /// <returns>DataLib Waypoint object. It hasn't been stored.</returns>
        public static Romer.DataLib.Waypoint From(
            this Romer.DataLib.Waypoint self,
            Romer.GeoLib.Location from)
        {
            var to = new Waypoint()
            {
                CreationTime = from.Time.HasValue ? from.Time.Value : DateTime.Now,
                Latitude = from.Latitude,
                Longitude = from.Longitude,
                Altitude = from.Altitude
            };
            return to;
        }


        /// <summary>
        /// Convert from Android Location to DataLib Waypoint.
        /// The creation time is the fix time. The label and notes are empty.
        /// </summary>
        /// <param name="self">Ignored</param>
        /// <param name="from">Android Location object</param>
        /// <returns>DataLib Waypoint object. It hasn't been stored.</returns>
        /// <exception cref="ArgumentNullException">Null argument</exception>
        public static Romer.DataLib.Waypoint From(
            this Romer.DataLib.Waypoint self,
            Android.Locations.Location from)
        {
            if (from == null)
                throw new ArgumentNullException(nameof(from));

            var to = new Waypoint()
            {
                CreationTime = DateTimeOffset.FromUnixTimeMilliseconds(from.Time).DateTime,
                Latitude = from.Latitude,
                Longitude = from.Longitude,
                Altitude = from.HasAltitude ? from.Altitude : (double?)null
            };
            return to;
        }

    }
}
