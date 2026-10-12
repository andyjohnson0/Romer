using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;



namespace Romer.GeoLib
{
    /// <summary>
    /// Grid system provider for Universal Transverse Mercator (UTM), e.g. "30N 377486 6296562" at 1 m precision.
    /// The easting and northing are in units of the precision. Applies between 80S and 84N.
    /// </summary>
    public class UtmGridSystemProvider : IGridSystemProvider
    {
        /// <inheritdoc/>
        bool IGridSystemProvider.IsInArea(Location location)
        {
            return location.Latitude <= 84D && location.Latitude >= -80D &&
                   location.Longitude <= 180D && location.Longitude >= -180;
        }


        /// <inheritdoc/>
        string IGridSystemProvider.GetGridReference(Location location, int precisionMetres)
        {
            if (precisionMetres < 1)
                throw new ArgumentOutOfRangeException(nameof(precisionMetres), precisionMetres, "Precision must be at least 1 metre");

            // Get the grid reference
            var gridRef = FromLatLon(latitude: location.Latitude, longitude: location.Longitude);

            // Adjust precision
            gridRef.easting = gridRef.easting / precisionMetres;
            gridRef.northing = gridRef.northing / precisionMetres;

            // Convert to string form.
            var hemi = location.Latitude >= 0 ? 'N' : 'S';
            var gridRefStr = String.Join(' ',
                                         gridRef.zoneNum.ToString() + hemi, 
                                         ((int)gridRef.easting).ToString(), 
                                         ((int)gridRef.northing).ToString());
            return gridRefStr;
        }


        /// <inheritdoc/>
        string IGridSystemProvider.Tag
        {
            get { return "UTM"; }
        }


        #region Implementation

        // Based on https://github.com/TimothyGu/utm/blob/master/index.es.js

        private const double K0 = 0.9996;
        private const double E = 0.00669438;
        private const double R = 6378137;
        private const string ZONE_LETTERS = "CDEFGHJKLMNPQRSTUVWXX";  // 'I' and 'O' omitted, 'X' intentionally appears twice


        private const double E2 = E * E;
        private const double E3 = E2 * E;
        private const double E_P2 = E / (1 - E);

        private const double M1 = 1 - E / 4 - 3 * E2 / 64 - 5 * E3 / 256;
        private const double M2 = 3 * E / 8 + 3 * E2 / 32 + 45 * E3 / 1024;
        private const double M3 = 15 * E2 / 256 + 45 * E3 / 1024;
        private const double M4 = 35 * E3 / 3072;


        private static (double easting, double northing, int zoneNum, char zoneLetter) FromLatLon(
            double latitude,
            double longitude)
        {
            if (latitude < -80 || latitude > 84)
            {
                throw new ArgumentException("latitude out of range (must be between 80 deg S and 84 deg N)", nameof(latitude));
            }
            if (longitude < -180 || longitude > 180)
            {
                throw new ArgumentException("longitude out of range (must be between 180 deg W and 180 deg E)", nameof(longitude));
            }

            var latRad = ToRadians(latitude);
            var latSin = Math.Sin(latRad);
            var latCos = Math.Cos(latRad);

            var latTan = Math.Tan(latRad);
            var latTan2 = Math.Pow(latTan, 2);
            var latTan4 = Math.Pow(latTan, 4);

            var zoneNum = LatLonToZoneNumber(latitude, longitude);
            var zoneLetter = LatitudeToZoneLetter(latitude);

            var lonRad = ToRadians(longitude);
            var centralLon = ZoneNumberToCentralLongitude(zoneNum);
            var centralLonRad = ToRadians(centralLon);

            var n = R / Math.Sqrt(1 - E * latSin * latSin);
            var c = E_P2 * latCos * latCos;

            var a = latCos * (lonRad - centralLonRad);
            var a2 = Math.Pow(a, 2);
            var a3 = Math.Pow(a, 3);
            var a4 = Math.Pow(a, 4);
            var a5 = Math.Pow(a, 5);
            var a6 = Math.Pow(a, 6);

            var m = R * (M1 * latRad -
                         M2 * Math.Sin(2 * latRad) +
                         M3 * Math.Sin(4 * latRad) -
                         M4 * Math.Sin(6 * latRad));
            var easting = K0 * n * (a +
                                    a3 / 6 * (1 - latTan2 + c) +
                                    a5 / 120 * (5 - 18 * latTan2 + latTan4 + 72 * c - 58 * E_P2)) + 500000;
            var northing = K0 * (m + n * latTan * (a2 / 2 +
                                                   a4 / 24 * (5 - latTan2 + 9 * c + 4 * c * c) +
                                                   a6 / 720 * (61 - 58 * latTan2 + latTan4 + 600 * c - 330 * E_P2)));
            if (latitude < 0) northing += 1e7;

            return (easting: easting, northing: northing, zoneNum: zoneNum, zoneLetter: zoneLetter);
        }


        private static char LatitudeToZoneLetter(double latitude)
        {
            if (latitude < -80.0 || latitude > 84.0)
                throw new ArgumentException("latitude out of range");

            return ZONE_LETTERS[(int)(Math.Floor((latitude + 80) / 8))];
        }


        private static int LatLonToZoneNumber(double latitude, double longitude)
        {
            if (56 <= latitude && latitude < 64 && 3 <= longitude && longitude < 12) 
                return 32;

            if (72 <= latitude && latitude <= 84 && longitude >= 0)
            {
                if (longitude < 9) 
                    return 31;
                if (longitude < 21) 
                    return 33;
                if (longitude < 33) 
                    return 35;
                if (longitude < 42) 
                    return 37;
            }

            return (int) Math.Floor((longitude + 180) / 6) + 1;
        }


        private static int ZoneNumberToCentralLongitude(int zoneNum)
        {
            return (zoneNum - 1) * 6 - 180 + 3;
        }


        private static double ToRadians(double deg)
        {
            return deg * Math.PI / 180;
        }

        #endregion Implementation
    }
}