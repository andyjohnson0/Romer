using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Romer.GeoLib
{
    /// <summary>
    /// Grid system provider for WGS84 latitude and longitude in decimal degrees, e.g. "54.429,-2.963" at 100 m
    /// precision. Applies everywhere.
    /// </summary>
    public class LatLonGridSystemProvider : IGridSystemProvider
    {
        /// <inheritdoc/>
        bool IGridSystemProvider.IsInArea(Location location)
        {
            return true;
        }
        

        /// <inheritdoc/>
        string IGridSystemProvider.GetGridReference(Location location, int precisionMetres)
        {
            if (location.Latitude < -90D || location.Latitude > +90D)
                throw new ArgumentException($"Latitude out of range: {location.Latitude}", nameof(location.Latitude));
            if (location.Longitude < -180D || location.Longitude > +180D)
                throw new ArgumentException($"Longitude out of range: {location.Longitude}", nameof(location.Longitude));

            // Precision.
            // See https://en.m.wikipedia.org/wiki/Decimal_degrees#Precision
            // This is approximate, and we use values for the equator.
            // 1m ≅ 5dp, 10m ≅ 4dp, 100m = 3dp, etc.
            var numDp = GridPrecision.DigitsPerPart(precisionMetres);

            // Invariant culture so that the decimal separator is always '.', which keeps the comma separator
            // unambiguous. "0." rather than "#." keeps the leading zero (0.5 rather than .5).
            var fmt = "0." + new string('#', numDp);
            return string.Join(",", location.Latitude.ToString(fmt, CultureInfo.InvariantCulture),
                                    location.Longitude.ToString(fmt, CultureInfo.InvariantCulture));
        }


        /// <inheritdoc/>
        string IGridSystemProvider.Tag
        {
            get { return "WGS84"; }
        }
    }
}
