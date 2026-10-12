using System;
using System.Collections.Generic;
using System.Text;

namespace Romer.GeoLib
{
    /// <summary>
    /// A geographic location: a WGS84 position, with an optional time and altitude.
    /// </summary>
    public struct Location
    {
        /// <summary>Time of the location fix, or null if not known.</summary>
        public DateTime? Time;

        /// <summary>WGS84 latitude in decimal degrees.</summary>
        public double Latitude;

        /// <summary>WGS84 longitude in decimal degrees.</summary>
        public double Longitude;

        /// <summary>Altitude in metres, or null if not known.</summary>
        public double? Altitude;
    }
}
