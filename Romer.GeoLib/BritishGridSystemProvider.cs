using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;


namespace Romer.GeoLib
{
    /// <summary>
    /// Grid system provider for the Ordnance Survey's British National Grid, e.g. "ND 38984 73690" at 1 m precision.
    /// Applies in Great Britain and the Isle of Man.
    /// Based on https://github.com/chriscant/PhotoGridRef/
    /// which is Copyright 2018 PHD Computer Consultants Ltd and contributors.
    /// </summary>
    public class BritishGridSystemProvider : IGridSystemProvider
    {
        /// <inheritdoc/>
        bool IGridSystemProvider.IsInArea(Location location)
        {
            // Bounds from https://epsg.io/27700. The box also covers Ireland (which uses the Irish Grid) and part
            // of northern France.
            return GeoArea.InBox(location, 49.75D, 61.01D, -9.0D, 2.01D) &&
                   !GeoArea.InIreland(location) &&
                   !GeoArea.InNorthernFrance(location);
        }


        /// <inheritdoc/>
        string IGridSystemProvider.GetGridReference(
            Location location,
            int precisionMetres)
        {
            // Convert from the WGS-84 ellipsoid to 3D Cartesian coordinates
            LatLon latLonWgs84 = new LatLon()
            { 
                lat = DegToRad(location.Latitude), 
                lon = DegToRad(location.Longitude),
                eh = location.Altitude ?? 0.0
            };
            CartesianCoord cartWgs84 = LatLonToCartesian(latLonWgs84, _wgs84Ellipsoid);

            // Perform the Helmert transform to (approximately) correct for differences in datum
            CartesianCoord cartAiry30 = HelmertTransform(cartWgs84, _helmertWgs84ToOsgb36);

            // Convert back to latitude, longitude and ellipsoidal height to give coordinates on the Airy 1830 ellipsoid.
            LatLon latLonAiry30 = CartesianToLatLon(cartAiry30, _airy1830Ellipsoid);

            // Project the coordinates to the National Grid's 2D projection
            EastingsNorthings bngAiry30 = LatLonToEastingsNorthings(latLonAiry30, _osNationalGridProj);

            // Turn these Eastings and Northings into a grid references
            GridRef bngGridRef = EastingsNorthingsToGridRef(bngAiry30, _osNationalGrid);

            //
            var numDigitsPerPart = GridPrecision.DigitsPerPart(precisionMetres);
            const string fmt = "00000";
            var eastingStr = ((int)bngGridRef.e).ToString(fmt).Substring(0, numDigitsPerPart);
            var northingStr = ((int)bngGridRef.n).ToString(fmt).Substring(0, numDigitsPerPart);
            return string.Join(" ", bngGridRef.code, eastingStr, northingStr);
        }


        /// <inheritdoc/>
        string IGridSystemProvider.Tag 
        {
            get { return "OSGB"; }
        }





        #region Implementation

        // Based on https://github.com/chriscant/PhotoGridRef
        //
        // Licenced under the BSD 3-Clause "New" or "Revised" License
        //
        // Copyright 2018 PHD Computer Consultants Ltd and contributors
        // www.phdcc.com

        // Redistribution and use in source and binary forms, with or without modification, 
        // are permitted provided that the following conditions are met:
        //
        // 1. Redistributions of source code must retain the above copyright notice,
        //    this list of conditions and the following disclaimer.
        //
        // 2. Redistributions in binary form must reproduce the above copyright notice,
        //    this list of conditions and the following disclaimer in the documentation
        //    and / or other materials provided with the distribution.
        //
        // 3. Neither the name of the copyright holder nor the names of its contributors
        //    may be used to endorse or promote products derived from this software
        //    without specific prior written permission.
        //
        // THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
        // ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
        // WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
        // IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
        // INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
        // BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, 
        // DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY
        // OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT(INCLUDING
        // NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, 
        // EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.


        /// <summary>
        /// Ordnance Survey grid reference and height.
        /// </summary>
        private struct GridRef
        {
            /// <summary>
            /// One- or two-letter National Grid code for the 100 km square. If the string is empty then the grid
            /// reference is not a valid reference.
            /// </summary>
            public string code;

            /// <summary>
            /// Eastings (m)
            /// </summary>
            public double e;

            /// <summary>
            /// Northings (m)
            /// </summary>
            public double n;

            /// <summary>
            /// Height (m)
            /// </summary>
            public double h;
        };


        /// <summary>
        /// Latitude/Longitude with ellipsoidal height.
        /// </summary>
        private struct LatLon
        {
            /// <summary>
            /// Latitude (radians)
            /// </summary>
            public double lat;

            /// <summary>
            /// Longitude (radians)
            /// </summary>
            public double lon;

            /// <summary>
            /// Ellipsoidal height (m)
            /// </summary>
            public double eh;
        };


        /// <summary>
        /// Ellipsoid shape definition.
        /// </summary>
        private struct Ellipsoid
        {
            /// <summary>
            /// Semi-major axis (m)
            /// </summary>
            public double a;

            /// <summary>
            /// Semi-minor axis (m)
            /// </summary>
            public double b;
        };


        /// <summary>
        /// Cartesian 3D coordinate.
        /// </summary>
        private struct CartesianCoord
        {
            public double x;
            public double y;
            public double z;
        };


        /// <summary>
        /// Eastings and Northings (and height).
        /// </summary>
        private struct EastingsNorthings
        {
            /// <summary>
            /// Eastings (m)
            /// </summary>
            public double e;

            /// <summary>
            /// Northings (m)
            /// </summary>
            public double n;

            /// <summary>
            /// Height (m)
            /// </summary>
            public double h;
        };


        /// <summary>
        /// Transverse Mercator projections
        /// </summary>
        private struct TransverseMercatorProjection
        {
            /// <summary>
            /// Eastings of the true origin (m)
            /// </summary>
            public double e0;

            /// <summary>
            /// Northings of the true origin (m)
            /// </summary>
            public double n0;

            /// <summary>
            /// Central meridian scale factor
            /// </summary>
            public double f0;

            /// <summary>
            /// Latitude of true origin (degrees)
            /// </summary>
            public double lat0;

            /// <summary>
            /// Longitude of true origin and central meridian (degrees)
            /// </summary>
            public double lon0;

            /// <summary>
            /// The ellipsoid on which the lat/lon refer.
            /// </summary>
            public Ellipsoid ellipsoid;
        };


        /// <summary>
        /// Helmert transformation parameters for approximately converting a set of
        /// lat/lon between a pair of datum values.
        /// </summary>
        private struct HelmertTransformationParams
        {
            // Translation (m)
            public double tx;
            public double ty;
            public double tz;

            // Rotation (seconds)
            public double rx;
            public double ry;
            public double rz;

            // Scale Factor (ppm)
            public double s;
        };


        /// <summary>
        /// A definition of a National Grid grid-like system of grid squares. The grid is
        /// assumed to be made up of 100km squares. Each letter refers to a
        /// grid/sub-grid of 5x5 letters arranged like so:
        ///
        /// A B C D E
        /// F G H J K
        /// L M N O P
        /// Q R S T U
        /// V W X Y Z
        ///
        /// (Note the missing 'I')
        /// </summary>
        private struct Grid
        {
            // Number of digits in the grid square letters
            public int num_digits;

            // First letter of the bottom-left grid-square.
            public char bottom_left_first_char;

            // Width and height (in 100km grid squares) of the area that the grid system
            // applies.
            public int width;
            public int height;
        };


        /// <summary>
        /// Conversion from cartesian to lat-lon coordinates is done via an iterative
        /// algorithm. This constant defines the number of meters precision to achieve.
        /// </summary>
        private static readonly double _cartToLatLonPrecision = 4.0;


        /******************************************************************************
         * Ellipsoid definitions. Values taken from "A guide to coordinate systems in
         * Great Britain"
         ******************************************************************************/

        /// <summary>
        /// WGS84 ellipsoid
        /// </summary>
        private readonly Ellipsoid _wgs84Ellipsoid = new Ellipsoid()
        { 
            a = 6378137.000,
            b = 6356752.3141
        };

        /// <summary>
        /// Airy 1830 ellipsoid
        /// </summary>
        private readonly Ellipsoid _airy1830Ellipsoid = new Ellipsoid()
        { 
            a = 6377563.396, 
            b = 6356256.910
        };


        /// <summary>
        /// Helmert transformation WGS84 to OSGB36
        /// </summary>
        private readonly HelmertTransformationParams _helmertWgs84ToOsgb36 = new HelmertTransformationParams()
        {
            tx = -446.448,
            ty = 125.157,
            tz = -542.060,
            rx = -0.1502,
            ry = -0.2470,
            rz = -0.8421,
            s = 20.4894
        };



        /******************************************************************************
         * Transverse Mercator Projections. Values taken from "A guide to coordinate
         * systems in Great Britain".
         ******************************************************************************/

        /// <summary>
        /// Ordnance Survey National Grid projection
        /// </summary>
        private readonly TransverseMercatorProjection _osNationalGridProj = new TransverseMercatorProjection()
        {
            e0 = 400000.0,
            n0 = -100000.0,
            f0 = 0.9996012717,
            lat0 = 49.0,
            lon0 = -2.0,
            ellipsoid = new Ellipsoid()
            {
                // Airy 1830
                a = 6377563.396,
                b = 6356256.910
            } 
        };


        /// <summary>
        /// Ordnance Survey National Grid over England, Scotland and Wales.
        /// </summary>
        private readonly Grid _osNationalGrid = new Grid()
        {
            num_digits = 2,
            bottom_left_first_char = 'S',
            width = 7,
            height = 13
        };



        /// <summary>
        /// Convert degrees to radians
        /// </summary>
        /// <param name="deg">Degrees</param>
        /// <returns>Radians</returns>
        private static double DegToRad(double deg)
        {
            return (deg / 180) * Math.PI;
        }


        /// <summary>
        /// Convert an upper-case grid-ref character to the index within a 5x5 grid (skipping 'I').
        /// </summary>
        /// <param name="c">Character</param>
        /// <returns>Index</returns>
        private static int CtoI(char c)
        {
            return (c) - 'A' - ((c > 'I') ? 1 : 0);
        }


        /// <summary>
        /// Convert an index within a 5x5 grid into an upper-case letter (skipping 'I').
        /// </summary>
        /// <param name="i">Index</param>
        /// <returns>Character</returns>
        private static char ItoC(int i)
        {
            return (char)('A' + ((i >= 8) ? i + 1 : i));
        }


        /// <summary>
        /// Convert an index within a 5x5 grid into the x-offset into the grid.
        /// </summary>
        /// <param name="i">Index</param>
        /// <returns>Index</returns>
        private static int ItoX(int i)
        {
            return i % 5;
        }


        /// <summary>
        /// Convert an index within a 5x5 grid into the y-offset into the grid.
        /// </summary>
        /// <param name="i">Index</param>
        /// <returns>Index</returns>
        private static int ItoY(int i)
        {
            return 4 - ((i) / 5);
        }


        /// <summary>
        /// Convert an x, y pair into an index into a 5x5 grid.
        /// </summary>
        /// <param name="x">x index</param>
        /// <param name="y">y index</param>
        /// <returns>Index</returns>
        private static int XYtoI(int x, int y)
        {
            return x + ((4 - y) * 5);
        }



        private static CartesianCoord LatLonToCartesian(
            LatLon point,
            Ellipsoid ellipsoid)
        {
            double sinPhi = Math.Sin(point.lat);
            double cosPhi = Math.Cos(point.lat);
            double sinLambda = Math.Sin(point.lon);
            double cosLambda = Math.Cos(point.lon);

            double eSq = ((ellipsoid.a * ellipsoid.a) - (ellipsoid.b * ellipsoid.b))
                         / (ellipsoid.a * ellipsoid.a);
            double nu = ellipsoid.a / Math.Sqrt(1.0 - (eSq * (sinPhi * sinPhi)));

            CartesianCoord cart_point;
            cart_point.x = (nu + point.eh) * cosPhi * cosLambda;
            cart_point.y = (nu + point.eh) * cosPhi * sinLambda;
            cart_point.z = ((1.0 - eSq) * nu + point.eh) * sinPhi;

            return cart_point;
        }


        private static LatLon CartesianToLatLon(
            CartesianCoord point,
            Ellipsoid ellipsoid)
        {
            // results accurate to around the given number of metres
            double precision = _cartToLatLonPrecision / ellipsoid.a;

            double eSq = ((ellipsoid.a * ellipsoid.a) - (ellipsoid.b * ellipsoid.b))
                         / (ellipsoid.a * ellipsoid.a);
            double p = Math.Sqrt((point.x * point.x) + (point.y * point.y));
            double phi = Math.Atan2(point.z, p * (1.0 - eSq));
            double phiP = 2.0 * Math.PI;
            double nu = 0.0;  // Was uninitialised in original
            while (Math.Abs(phi - phiP) > precision)
            {
                nu = ellipsoid.a / Math.Sqrt(1.0 - eSq * (Math.Sin(phi) * Math.Sin(phi)));
                phiP = phi;
                phi = Math.Atan2(point.z + eSq * nu * Math.Sin(phi), p);
            }

            LatLon lat_lon;
            lat_lon.lat = phi;
            lat_lon.lon = Math.Atan2(point.y, point.x);
            lat_lon.eh = p / Math.Cos(phi) - nu;

            return lat_lon;
        }


        private static CartesianCoord HelmertTransform(
            CartesianCoord point,
            HelmertTransformationParams helmert)
        {
            // Normalise seconds to radians
            double rx = DegToRad(helmert.rx / 3600.0);
            double ry = DegToRad(helmert.ry / 3600.0);
            double rz = DegToRad(helmert.rz / 3600.0);
            // Normalise ppm to (1+s)
            double s1 = 1 + (helmert.s / 1000000.0);

            // Apply transform
            CartesianCoord new_point;
            new_point.x = helmert.tx + point.x * s1 - point.y * rz + point.z * ry;
            new_point.y = helmert.ty + point.x * rz + point.y * s1 - point.z * rx;
            new_point.z = helmert.tz - point.x * ry + point.y * rx + point.z * s1;

            return new_point;
        }


        private static EastingsNorthings LatLonToEastingsNorthings(
            LatLon point, 
            TransverseMercatorProjection projection)
        {
            // Convert to radians
            double lat0 = DegToRad(projection.lat0);
            double lon0 = DegToRad(projection.lon0);

            // Shorter-named alias
            double lat = point.lat;
            double lon = point.lon;
            double a = projection.ellipsoid.a;
            double b = projection.ellipsoid.b;

            double e2 = 1.0 - (b * b) / (a * a);

            double n = (a - b) / (a + b);
            double n2 = n * n;
            double n3 = n * n * n;

            double cosLat = Math.Cos(lat);
            double sinLat = Math.Sin(lat);

            // Transverse radius of curvature
            double nu = a * projection.f0 / Math.Sqrt(1.0 - e2 * sinLat * sinLat);
            // Meridional radius of curvature
            double rho = a * projection.f0 * (1.0 - e2) / Math.Pow(1.0 - e2 * sinLat * sinLat, 1.5);
            double eta2 = nu / rho - 1.0;

            double Ma = (1.0 + n + (5.0 / 4.0) * n2 + (5.0 / 4.0) * n3) * (lat - lat0);
            double Mb = (3.0 * n + 3.0 * n * n + (21.0 / 8.0) * n3) * Math.Sin(lat - lat0) * Math.Cos(lat + lat0);
            double Mc = ((15.0 / 8.0) * n2 + (15.0 / 8.0) * n3) * Math.Sin(2.0 * (lat - lat0)) * Math.Cos(2.0 * (lat + lat0));
            double Md = (35.0 / 24.0) * n3 * Math.Sin(3.0 * (lat - lat0)) * Math.Cos(3.0 * (lat + lat0));
            // Meridional arc
            double M = b * projection.f0 * (Ma - Mb + Mc - Md);

            double cos3lat = cosLat * cosLat * cosLat;
            double cos5lat = cos3lat * cosLat * cosLat;
            double tan2lat = Math.Tan(lat) * Math.Tan(lat);
            double tan4lat = tan2lat * tan2lat;

            double I = M + projection.n0;
            double II = (nu / 2.0) * sinLat * cosLat;
            double III = (nu / 24.0) * sinLat * cos3lat * (5.0 - tan2lat + 9.0 * eta2);
            double IIIA = (nu / 720.0) * sinLat * cos5lat * (61.0 - 58.0 * tan2lat + tan4lat);
            double IV = nu * cosLat;
            double V = (nu / 6.0) * cos3lat * (nu / rho - tan2lat);
            double VI = (nu / 120.0) * cos5lat * (5.0 - 18.0 * tan2lat + tan4lat + 14.0 * eta2 - 58.0 * tan2lat * eta2);

            double dLon = lon - lon0;
            double dLon2 = dLon * dLon;
            double dLon3 = dLon2 * dLon;
            double dLon4 = dLon3 * dLon;
            double dLon5 = dLon4 * dLon;
            double dLon6 = dLon5 * dLon;

            EastingsNorthings eas_nor;
            eas_nor.n = I + II * dLon2 + III * dLon4 + IIIA * dLon6;
            eas_nor.e = projection.e0 + IV * dLon + V * dLon3 + VI * dLon5;
            eas_nor.h = point.eh;

            return eas_nor;
        }


        private static GridRef EastingsNorthingsToGridRef(
            EastingsNorthings point,
            Grid grid)
        {
            // An invalid grid reference (to start with)
            GridRef grid_ref = new GridRef()
            {
                code = ""
            };

            // Figure out the coordinate of the square in the grid
            int sq_x = ((int)point.e) / 100000;
            int sq_y = ((int)point.n) / 100000;

            // Check that the point is within the range covered by the grid-reference, if
            // not return an invalid grid reference.
            if (sq_x < 0 || sq_y < 0 || sq_x >= grid.width || sq_y >= grid.height)
            {
                return grid_ref;
            }

            // Work out each digit of the grid code
            var codeLetters = new char[grid.num_digits];
            for (int i = grid.num_digits - 1; i >= 0; i--)
            {
                // First digit may be offset in its grid
                int off_x = (i == 0) ? ItoX(CtoI(grid.bottom_left_first_char)) : 0;
                int off_y = (i == 0) ? ItoY(CtoI(grid.bottom_left_first_char)) : 0;

                // Figure out the letter for this digit
                codeLetters[i] = ItoC(XYtoI(off_x + (sq_x % 5), off_y + (sq_y % 5)));

                // "Shift" off the digit
                sq_x /= 5;
                sq_y /= 5;
            }
            grid_ref.code = new string(codeLetters);

            // Work out the other fields
            grid_ref.e = point.e % 100000.0;
            grid_ref.n = point.n % 100000.0;
            grid_ref.h = point.h;

            return grid_ref;
        }

        #endregion Implementation
    }
}
