using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace Romer.GeoLib
{
    /// <summary>
    /// Grid system provider for the United States National Grid (USNG), e.g. "18S UJ 23371 06519" at 1 m precision.
    /// Applies in the United States and its main Caribbean territories.
    /// See https://en.wikipedia.org/wiki/United_States_National_Grid for info.
    /// </summary>
    public class UsngGridSystemProvider : IGridSystemProvider
    {
        /// <inheritdoc/>
        bool IGridSystemProvider.IsInArea(Location location)
        {
            // The conversion below only supports the northern hemisphere, which is where the US is.
            return GeoArea.InUnitedStates(location);
        }


        /// <inheritdoc/>
        string IGridSystemProvider.GetGridReference(
            Location location,
            int precisionMetres)
        {
            // Calculation inaccurate for latitudes below -80 and above 84  --  zones A, B, Y, and Z.
            if (location.Latitude < -80D || location.Latitude > +84D)
                throw new ArgumentException($"Latitude out of range: {location.Latitude}. Latitudes below -80deg and above 84deg (zones A, B, Y, and Z) not supported.", nameof(location.Latitude));
            if (location.Longitude < -180D || location.Longitude > +180D)
                throw new ArgumentException($"Longitude out of range: {location.Longitude}", nameof(location.Longitude));

            // Precision: the number of digits in each of the easting and northing. 1m = 5 digits, 10m = 4, 100m = 3, etc.
            var numDigitsPerPart = GridPrecision.DigitsPerPart(precisionMetres);

            double lon = location.Longitude;
            double lat = location.Latitude;

            // Calculate USNG grid zone designation from latitude
            var gridZone = _gridZones[(int)Math.Floor((lat - (-80.0)) / 8)];

            // Calculate UTM zone number from longitude
            int utmZone = (int)((lon - (-180.0)) / 6.0) + 1;

            var utmPoint = LatLonToUtm(utmZone, lon, lat);
            return UtmPointToUsng(utmZone, gridZone, utmPoint.utmEasting, utmPoint.utmNorthing, numDigitsPerPart);
        }


        /// <inheritdoc/>
        string IGridSystemProvider.Tag
        {
            get { return "USNG"; }
        }




        #region Implementation

        // Based on https://github.com/klassenjs/usng_tools/blob/master/usng2.js and converted from javascript to C#.
        //
        // Copyright(c) 2008-2013 James Klassen
        //
        // Permission is hereby granted, free of charge, to any person obtaining a copy of
        // this software and associated documentation files(the “Software”), to deal in
        // the Software without restriction, including without limitation the rights to
        // use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
        // of the Software, and to permit persons to whom the Software is furnished to do
        // so, subject to the following conditions:
        //
        // The above copyright notice and this permission notice shall be included in all
        // copies of this Software or works derived from this Software.
        //
        // THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        // IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        // FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
        // AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        // LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        // OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        // SOFTWARE.



        // Calculation inaccurate for latitudes below -80 and above 84  --  zones A, B, Y, and Z.
        // US National grid is derived from the Universal Transverse Mercator (UTM) and the Universal Polar Stereographic (UPS)
        // grid systems, and these acronyms are used in this code.



        // Note: grid locations are the SW corner of the grid square (because easting and northing are always positive)
        //                                          0   1   2   3   4   5   6   7   8   9  10  11  12  13  14  15  16  17  18  19   x 100,000m northing
        private static readonly char[] _nsLetters135 = new char[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M', 'N', 'P', 'Q', 'R', 'S', 'T', 'U', 'V' };
        private static readonly char[] _nsLetters246 = new char[] { 'F', 'G', 'H', 'J', 'K', 'L', 'M', 'N', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'A', 'B', 'C', 'D', 'E' };

        //                                         1   2   3   4   5   6   7   8   x 100,000m easting
        private static readonly char[] _ewLetters14 = new char[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H' };
        private static readonly char[] _ewLetters25 = new char[] { 'J', 'K', 'L', 'M', 'N', 'P', 'Q', 'R' };
        private static readonly char[] _ewLetters36 = new char[] { 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' };

        // Grid zone letters.
        //                                      -80  -72  -64  -56  -48  -40  -32  -24  -16  -8    0    8   16   24   32   40   48   56   64   72   (*Latitude) 
        //                                                                                                               Handle oddball zone 80-84
        private static readonly char[] _gridZones = new char[] { 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M', 'N', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'X' };

        // Various geodetic constants
        private const double majorAxis = 6378137.0;
        private const double minorAxis = 6356752.3;
        private const double ecc = (majorAxis * majorAxis - minorAxis * minorAxis) / (majorAxis * majorAxis);
        private const double ecc2 = ecc / (1.0 - ecc);
        private const double k0 = 0.9996;
        private const double e4 = ecc * ecc;
        private const double e6 = ecc * e4;
        private const double degrees2radians = Math.PI / 180.0;



        // -------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Convert lat/lon (given in decimal degrees) to UTM, given a particular UTM zone.
        /// </summary>
        /// <param name="zone">UTM zone number</param>
        /// <param name="lon">Longitude in decimal degrees</param>
        /// <param name="lat">Latitude in decimal degrees</param>
        /// <returns>The zone, and the UTM easting and northing in metres</returns>
        private static (int utmZone, double utmEasting, double utmNorthing) LatLonToUtm(
            int zone,
            double lon,
            double lat)
        {
            var centeralMeridian = -((30 - zone) * 6 + 3) * degrees2radians;

            // Convert lat and lon to radians.
            lat = lat * degrees2radians;
            lon = lon * degrees2radians;

            var latSin = Math.Sin(lat);
            var latCos = Math.Cos(lat);
            var latTan = latSin / latCos;
            var latTan2 = latTan * latTan;
            var latTan4 = latTan2 * latTan2;

            var N = majorAxis / Math.Sqrt(1 - ecc * (latSin * latSin));
            var c = ecc2 * latCos * latCos;
            var a = latCos * (lon - centeralMeridian);
            var m = GetMeridianDistance(lat);

            var temp5 = 1.0 - latTan2 + c;
            var temp6 = 5.0 - 18.0 * latTan2 + latTan4 + 72.0 * c - 58.0 * ecc2;
            var temp11 = Math.Pow(a, 5);

            var x = k0 * N * (a + (temp5 * Math.Pow(a, 3)) / 6.0 + temp6 * temp11 / 120.0) + 500000;

            var temp7 = (5.0 - latTan2 + 9.0 * c + 4.0 * (c * c)) * Math.Pow(a, 4) / 24.0;
            var temp8 = 61.0 - 58.0 * latTan2 + latTan4 + 600.0 * c - 330.0 * ecc2;
            var temp9 = temp11 * a / 720.0;

            var y = k0 * (m + N * latTan * ((a * a) / 2.0 + temp7 + temp8 * temp9));

            return (utmZone: zone, utmEasting: x, utmNorthing: y);
        }


        /// <summary>
        /// Get the meridional arc: the distance along the central meridian from the equator to a latitude.
        /// </summary>
        /// <param name="lat">Latitude in radians</param>
        /// <returns>Distance in metres</returns>
        private static double GetMeridianDistance(double lat)
        {
            var c1 = majorAxis * (1 - ecc / 4 - 3 * e4 / 64 - 5 * e6 / 256);
            var c2 = -majorAxis * (3 * ecc / 8 + 3 * e4 / 32 + 45 * e6 / 1024);
            var c3 = majorAxis * (15 * e4 / 256 + 45 * e6 / 1024);
            var c4 = -majorAxis * 35 * e6 / 3072;
            return (c1 * lat + c2 * Math.Sin(lat * 2) + c3 * Math.Sin(lat * 4) + c4 * Math.Sin(lat * 6));
        }



        /// <summary>
        /// Get the USNG grid reference of a UTM point.
        /// </summary>
        /// <param name="utmZone">UTM zone number</param>
        /// <param name="gridZone">Latitude band letter of the grid zone designation</param>
        /// <param name="utmEasting">UTM easting in metres</param>
        /// <param name="utmNorthing">UTM northing in metres</param>
        /// <param name="precision">Number of digits in each of the easting and northing. 5 is 1 m precision.</param>
        /// <returns>USNG grid reference</returns>
        /// <exception cref="InvalidOperationException">The point is in the southern hemisphere, which isn't supported</exception>
	    private static string UtmPointToUsng(
            int utmZone,
            char gridZone,
            double utmEasting,
            double utmNorthing,
            int precision)
        {

            var ewIdx = (int)Math.Floor(utmEasting / 100000) - 1; // should be [100000, 900000]
            var nsIdx = (int)Math.Floor((utmNorthing % 2000000) / 100000); // should [0, 10000000) => [0, 2000000)
            if (nsIdx < 0)
            {
                // Southern hemisphere unsupported
                throw new InvalidOperationException($"Invalid nsIdx value: {nsIdx}");
            }

            // Derive grid square (two letters)
            var gridSquareSet = utmZone % 6;
            string gridSquare = gridSquareSet switch
            {
                1 => string.Concat(_ewLetters14[ewIdx], _nsLetters135[nsIdx]),
                2 => string.Concat(_ewLetters25[ewIdx], _nsLetters246[nsIdx]),
                3 => string.Concat(_ewLetters36[ewIdx], _nsLetters135[nsIdx]),
                4 => string.Concat(_ewLetters14[ewIdx], _nsLetters246[nsIdx]),
                5 => string.Concat(_ewLetters25[ewIdx], _nsLetters135[nsIdx]),
                0 => string.Concat(_ewLetters36[ewIdx], _nsLetters246[nsIdx]),  // Calculates as zero, but is technically 6
                _ => throw new InvalidOperationException($"Invalid grid square set {gridSquareSet}")
            };

            // Calc Easting and Northing integer to 100,000s place
            var easting = Math.Floor(utmEasting % 100000);
            var northing = Math.Floor(utmNorthing % 100000);
            if (northing < 0)
            {
                // Southern hemisphere unsupported
                throw new InvalidOperationException($"Invalid northing value: {northing}");
            }
            var northingStr = northing.ToString();
            var eastingStr = easting.ToString();

            // Pad up to meter precision (5 digits)
            while (northingStr.Length < 5)
                northingStr = '0' + northingStr;
            while (eastingStr.Length < 5)
                eastingStr = '0' + eastingStr;

            string gridEastingStr;
            string gridNorthingStr;
            if (precision > 5)
            {
                // Calculate the fractional meter parts
                var digits = precision - 5;
                gridEastingStr = easting + (utmEasting % 1).ToString("F" + digits).Substring(2, digits);
                gridNorthingStr = northing + (utmNorthing % 1).ToString("F" + digits).Substring(2, digits);
            }
            else
            {
                // Remove unnecessary digits
                gridEastingStr = eastingStr.Substring(0, precision);
                gridNorthingStr = northingStr.Substring(0, precision);
            }

            return string.Join(" ", utmZone.ToString() + gridZone, gridSquare, gridEastingStr, gridNorthingStr);
        }

        #endregion Implementation
    }
}
