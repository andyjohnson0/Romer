using System;
using System.Collections.Generic;
using System.Text;

namespace Romer.GeoLib
{
    /// <summary>
    /// Grid system provider for the Irish Grid, e.g. "C 39872 58812" at 1 m precision.
    /// Applies on the island of Ireland, including Northern Ireland.
    /// Based on https://github.com/chriscant/PhotoGridRef/blob/master/gt_irish.cpp,
    /// which is Copyright 2018 PHD Computer Consultants Ltd and contributors.
    /// </summary>
    /// <remarks>
    /// The WGS84 to Irish Grid conversion diverges from the original, to fix a bug found in 2026.
    /// The original offers two datum transformations, and Romer used the first:
    /// - Level 1: project WGS84 latitude and longitude directly, then add a fixed shift of +49 m east and -23.4 m north.
    ///   A fixed shift is only right near the middle of Ireland, and was up to 30 m out towards the coasts,
    ///   compared with Tailte Éireann's official converter.
    /// - Level 2: a 7-parameter Helmert transformation, but reversing the published parameters wrongly. It changed the
    ///   signs of the translations and scale but not the rotations, which put it about 34 m out everywhere. It also
    ///   used the altitude for the Z coordinate but zero height for X and Y.
    /// Romer now always uses the Helmert transformation, with every parameter's sign reversed and zero height
    /// throughout, which agrees with the official converter to within about 1 m. The Level 1 shift is removed.
    /// </remarks>
    public class IrishGridSystemProvider : IGridSystemProvider
    {
        /// <inheritdoc/>
        bool IGridSystemProvider.IsInArea(Location location)
        {
            // The island of Ireland. The bounding box from https://epsg.io/29903 also covers parts of Scotland.
            return GeoArea.InIreland(location);
        }


        /// <inheritdoc/>
        string IGridSystemProvider.GetGridReference(
            Location location,
            int precisionMetres)
        {
            var irish = Wgs84ToIrish(location);

            int y = (int)(Math.Floor(irish.northings / 100000));
            int x = (int)(Math.Floor(irish.eastings / 100000));

            var code = _gridCodes[x, y];
            var e = irish.eastings % 100000.0;
            var n = irish.northings % 100000.0;

            var numDigitsPerPart = GridPrecision.DigitsPerPart(precisionMetres);
            const string fmt = "00000";
            var eastingStr = ((int)e).ToString(fmt).Substring(0, numDigitsPerPart);
            var northingStr = ((int)n).ToString(fmt).Substring(0, numDigitsPerPart);
            return string.Join(" ", code, eastingStr, northingStr);
        }


        /// <inheritdoc/>
        string IGridSystemProvider.Tag
        {
            get { return "IRELG"; }
        }



        #region Implementation

        // Based on https://github.com/chriscant/PhotoGridRef

        private static char[,] _gridCodes =
        {
            {'V', 'Q', 'L', 'F', 'A'},
            {'W', 'R', 'M', 'G', 'B'},
            {'X', 'S', 'N', 'H', 'C'},
            {'Y', 'T', 'O', 'J', 'D'},
            {'Z', 'U', 'P', 'K', 'E'}
        };


        private struct Irish
        {
            public double eastings;
            public double northings;
            public double height;
        };


        private static Irish Wgs84ToIrish(
            Location location)  // WGS84
        {
            // Height doesn't noticeably affect the horizontal result, so use zero throughout.
            // It must be the same for X, Y and Z, or the transformation skews the position.
            double height = 0;

            double x1 = LatLongHeightToX(location.Latitude, location.Longitude, height, 6378137.00, 6356752.313);
            double y1 = LatLongHeightToY(location.Latitude, location.Longitude, height, 6378137.00, 6356752.313);
            double z1 = LatHeightToZ(location.Latitude, height, 6378137.00, 6356752.313);

            // 7-parameter Helmert transformation from WGS84 to the 1965 datum, using OSi/OSNI's published parameters
            // (EPSG:1954). Those are a position vector transformation from the 1965 datum to ETRS89/WGS84:
            // translations 482.53, -130.596, 564.557 m, rotations -1.042, -0.214, -0.631 arc seconds, scale 8.15 ppm.
            // Going the other way reverses the sign of every parameter, including the rotations.
            // Checked against Tailte Éireann's official co-ordinate converter to within about 1 m across Ireland.
            double x2 = HelmertX(x1, y1, z1, -482.53, 0.214, 0.631, -8.15);
            double y2 = HelmertY(x1, y1, z1, 130.596, 1.042, 0.631, -8.15);
            double z2 = HelmertZ(x1, y1, z1, -564.557, 1.042, 0.214, -8.15);

            double latitude2 = XyzToLat(x2, y2, z2, 6377340.189, 6356034.447);
            double longitude2 = XyzToLong(x2, y2);

            double e = LatLongToEast(latitude2, longitude2, 6377340.189, 6356034.447, 200000, 1.000035, 53.50000, -8.00000);
            double n = LatLongToNorth(latitude2, longitude2, 6377340.189, 6356034.447, 200000, 250000, 1.000035, 53.50000, -8.00000);

            var irish = new Irish()
            {
                // Not rounded: GetGridReference() truncates to the precision shown, as grid references conventionally are.
                eastings = e,
                northings = n,
                height = height
            };
            return irish;
        }






        /*****************************************************************************
        *
        * GT_Math is a collection of static methods doing all the nasty sums
        *
        *****************************************************************************/

        private static double LatLongToEast(double PHI, double LAM, double a, double b, double e0, double f0, double PHI0, double LAM0)
        {
            //Project Latitude and longitude to Transverse Mercator eastings.
            //Input: - _
            //    Latitude (PHI) and Longitude (LAM) in decimal degrees; _
            //    ellipsoid axis dimensions (a & b) in meters; _
            //    eastings of false origin (e0) in meters; _
            //    central meridian scale factor (f0); _
            // latitude (PHI0) and longitude (LAM0) of false origin in decimal degrees.

            // Convert angle measures to radians
            double RadPHI = PHI * (Math.PI / 180);
            double RadLAM = LAM * (Math.PI / 180);
            double RadPHI0 = PHI0 * (Math.PI / 180);
            double RadLAM0 = LAM0 * (Math.PI / 180);

            double af0 = a * f0;
            double bf0 = b * f0;
            double e2 = (Math.Pow(af0, 2) - Math.Pow(bf0, 2)) / Math.Pow(af0, 2);
            double n = (af0 - bf0) / (af0 + bf0);
            double nu = af0 / (Math.Sqrt(1 - (e2 * Math.Pow(Math.Sin(RadPHI), 2))));
            double rho = (nu * (1 - e2)) / (1 - (e2 * Math.Pow(Math.Sin(RadPHI), 2)));
            double eta2 = (nu / rho) - 1;
            double p = RadLAM - RadLAM0;

            double IV = nu * (Math.Cos(RadPHI));
            double V = (nu / 6) * (Math.Pow(Math.Cos(RadPHI), 3)) * ((nu / rho) - (Math.Pow(Math.Tan(RadPHI), 2)));
            double VI = (nu / 120) * (Math.Pow(Math.Cos(RadPHI), 5)) * (5 - (18 * (Math.Pow(Math.Tan(RadPHI), 2))) + (Math.Pow(Math.Tan(RadPHI), 4)) + (14 * eta2) - (58 * (Math.Pow(Math.Tan(RadPHI), 2)) * eta2));

            return e0 + (p * IV) + (Math.Pow(p, 3) * V) + (Math.Pow(p, 5) * VI);
        }


        private static double LatLongToNorth(double PHI, double LAM, double a, double b, double e0, double n0, double f0, double PHI0, double LAM0)
        {
            // Project Latitude and longitude to Transverse Mercator northings
            // Input: - _
            // Latitude (PHI) and Longitude (LAM) in decimal degrees; _
            // ellipsoid axis dimensions (a & b) in meters; _
            // eastings (e0) and northings (n0) of false origin in meters; _
            // central meridian scale factor (f0); _
            // latitude (PHI0) and longitude (LAM0) of false origin in decimal degrees.

            // REQUIRES THE "Marc" FUNCTION

            // Convert angle measures to radians
            double RadPHI = PHI * (Math.PI / 180);
            double RadLAM = LAM * (Math.PI / 180);
            double RadPHI0 = PHI0 * (Math.PI / 180);
            double RadLAM0 = LAM0 * (Math.PI / 180);

            double af0 = a * f0;
            double bf0 = b * f0;
            double e2 = (Math.Pow(af0, 2) - Math.Pow(bf0, 2)) / Math.Pow(af0, 2);
            double n = (af0 - bf0) / (af0 + bf0);
            double nu = af0 / (Math.Sqrt(1 - (e2 * Math.Pow(Math.Sin(RadPHI), 2))));
            double rho = (nu * (1 - e2)) / (1 - (e2 * Math.Pow(Math.Sin(RadPHI), 2)));
            double eta2 = (nu / rho) - 1;
            double p = RadLAM - RadLAM0;
            double M = Marc(bf0, n, RadPHI0, RadPHI);

            double I = M + n0;
            double II = (nu / 2) * (Math.Sin(RadPHI)) * (Math.Cos(RadPHI));
            double III = ((nu / 24) * (Math.Sin(RadPHI)) * (Math.Pow(Math.Cos(RadPHI), 3))) * (5 - (Math.Pow(Math.Tan(RadPHI), 2)) + (9 * eta2));
            double IIIA = ((nu / 720) * (Math.Sin(RadPHI)) * (Math.Pow(Math.Cos(RadPHI), 5))) * (61 - (58 * (Math.Pow(Math.Tan(RadPHI), 2))) + (Math.Pow(Math.Tan(RadPHI), 4)));

            return I + (Math.Pow(p, 2) * II) + (Math.Pow(p, 4) * III) + (Math.Pow(p, 6) * IIIA);
        }

        private static double LatLongHeightToX(double PHI, double LAM, double H, double a, double b)
        {
            // Convert geodetic coords lat (PHI), long (LAM) and height (H) to cartesian X coordinate.
            // Input: - _
            //    Latitude (PHI)& Longitude (LAM) both in decimal degrees; _
            //  Ellipsoidal height (H) and ellipsoid axis dimensions (a & b) all in meters.

            // Convert angle measures to radians
            double RadPHI = PHI * (Math.PI / 180);
            double RadLAM = LAM * (Math.PI / 180);

            // Compute eccentricity squared and nu
            double e2 = (Math.Pow(a, 2) - Math.Pow(b, 2)) / Math.Pow(a, 2);
            double V = a / (Math.Sqrt(1 - (e2 * (Math.Pow(Math.Sin(RadPHI), 2)))));

            // Compute X
            return (V + H) * (Math.Cos(RadPHI)) * (Math.Cos(RadLAM));
        }


        private static double LatLongHeightToY(double PHI, double LAM, double H, double a, double b)
        {
            // Convert geodetic coords lat (PHI), long (LAM) and height (H) to cartesian Y coordinate.
            // Input: - _
            // Latitude (PHI)& Longitude (LAM) both in decimal degrees; _
            // Ellipsoidal height (H) and ellipsoid axis dimensions (a & b) all in meters.

            // Convert angle measures to radians
            double RadPHI = PHI * (Math.PI / 180);
            double RadLAM = LAM * (Math.PI / 180);

            // Compute eccentricity squared and nu
            double e2 = (Math.Pow(a, 2) - Math.Pow(b, 2)) / Math.Pow(a, 2);
            double V = a / (Math.Sqrt(1 - (e2 * (Math.Pow(Math.Sin(RadPHI), 2)))));

            // Compute Y
            return (V + H) * (Math.Cos(RadPHI)) * (Math.Sin(RadLAM));
        }


        private static double LatHeightToZ(double PHI, double H, double a, double b)
        {
            // Convert geodetic coord components latitude (PHI) and height (H) to cartesian Z coordinate.
            // Input: - _
            //    Latitude (PHI) decimal degrees; _
            // Ellipsoidal height (H) and ellipsoid axis dimensions (a & b) all in meters.

            // Convert angle measures to radians
            double RadPHI = PHI * (Math.PI / 180);

            // Compute eccentricity squared and nu
            double e2 = (Math.Pow(a, 2) - Math.Pow(b, 2)) / Math.Pow(a, 2);
            double V = a / (Math.Sqrt(1 - (e2 * (Math.Pow(Math.Sin(RadPHI), 2)))));

            // Compute X
            return ((V * (1 - e2)) + H) * (Math.Sin(RadPHI));
        }


        private static double HelmertX(double X, double Y, double Z, double DX, double Y_Rot, double Z_Rot, double s)
        {
            // (X, Y, Z, DX, Y_Rot, Z_Rot, s)
            // Computed Helmert transformed X coordinate.
            // Input: - _
            //    cartesian XYZ coords (X,Y,Z), X translation (DX) all in meters ; _
            // Y and Z rotations in seconds of arc (Y_Rot, Z_Rot) and scale in ppm (s).

            // Convert rotations to radians and ppm scale to a factor
            double sfactor = s * 0.000001;

            double RadY_Rot = (Y_Rot / 3600) * (Math.PI / 180);

            double RadZ_Rot = (Z_Rot / 3600) * (Math.PI / 180);

            //Compute transformed X coord
            return (X + (X * sfactor) - (Y * RadZ_Rot) + (Z * RadY_Rot) + DX);
        }


        private static double HelmertY(double X, double Y, double Z, double DY, double X_Rot, double Z_Rot, double s)
        {
            // (X, Y, Z, DY, X_Rot, Z_Rot, s)
            // Computed Helmert transformed Y coordinate.
            // Input: - _
            //    cartesian XYZ coords (X,Y,Z), Y translation (DY) all in meters ; _
            //  X and Z rotations in seconds of arc (X_Rot, Z_Rot) and scale in ppm (s).

            // Convert rotations to radians and ppm scale to a factor
            double sfactor = s * 0.000001;
            double RadX_Rot = (X_Rot / 3600) * (Math.PI / 180);
            double RadZ_Rot = (Z_Rot / 3600) * (Math.PI / 180);

            // Compute transformed Y coord
            return (X * RadZ_Rot) + Y + (Y * sfactor) - (Z * RadX_Rot) + DY;
        }


        private static double HelmertZ(double X, double Y, double Z, double DZ, double X_Rot, double Y_Rot, double s)
        {
            // (X, Y, Z, DZ, X_Rot, Y_Rot, s)
            // Computed Helmert transformed Z coordinate.
            // Input: - _
            //    cartesian XYZ coords (X,Y,Z), Z translation (DZ) all in meters ; _
            // X and Y rotations in seconds of arc (X_Rot, Y_Rot) and scale in ppm (s).
            // 
            // Convert rotations to radians and ppm scale to a factor
            double sfactor = s * 0.000001;
            double RadX_Rot = (X_Rot / 3600) * (Math.PI / 180);
            double RadY_Rot = (Y_Rot / 3600) * (Math.PI / 180);

            // Compute transformed Z coord
            return (-1 * X * RadY_Rot) + (Y * RadX_Rot) + Z + (Z * sfactor) + DZ;
        }


        private static double XyzToLat(double X, double Y, double Z, double a, double b)
        {
            // Convert XYZ to Latitude (PHI) in Dec Degrees.
            // Input: - _
            // XYZ cartesian coords (X,Y,Z) and ellipsoid axis dimensions (a & b), all in meters.

            // THIS FUNCTION REQUIRES THE "Iterate_XYZ_to_Lat" FUNCTION
            // THIS FUNCTION IS CALLED BY THE "XYZ_to_H" FUNCTION

            double RootXYSqr = Math.Sqrt(Math.Pow(X, 2) + Math.Pow(Y, 2));
            double e2 = (Math.Pow(a, 2) - Math.Pow(b, 2)) / Math.Pow(a, 2);
            double PHI1 = Math.Atan2(Z, (RootXYSqr * (1 - e2)));

            double PHI = IterateXyzToLat(a, e2, PHI1, Z, RootXYSqr);

            return PHI * (180 / Math.PI);
        }


        private static double IterateXyzToLat(double a, double e2, double PHI1, double Z, double RootXYSqr)
        {
            // Iteratively computes Latitude (PHI).
            // Input: - _
            //    ellipsoid semi major axis (a) in meters; _
            //    eta squared (e2); _
            //    estimated value for latitude (PHI1) in radians; _
            //    cartesian Z coordinate (Z) in meters; _
            // RootXYSqr computed from X & Y in meters.

            // THIS FUNCTION IS CALLED BY THE "XYZ_to_PHI" FUNCTION
            // THIS FUNCTION IS ALSO USED ON IT'S OWN IN THE _
            // "Projection and Transformation Calculations.xls" SPREADSHEET


            double V = a / (Math.Sqrt(1 - (e2 * Math.Pow(Math.Sin(PHI1), 2))));
            double PHI2 = Math.Atan2((Z + (e2 * V * (Math.Sin(PHI1)))), RootXYSqr);

            while (Math.Abs(PHI1 - PHI2) > 0.000000001)
            {
                PHI1 = PHI2;
                V = a / (Math.Sqrt(1 - (e2 * Math.Pow(Math.Sin(PHI1), 2))));
                PHI2 = Math.Atan2((Z + (e2 * V * (Math.Sin(PHI1)))), RootXYSqr);
            }

            return PHI2;
        }


        private static double XyzToLong(double X, double Y)
        {
            // Convert XYZ to Longitude (LAM) in Dec Degrees.
            // Input: - _
            // X and Y cartesian coords in meters.

            return Math.Atan2(Y, X) * (180 / Math.PI);
        }


        private static double Marc(double bf0, double n, double PHI0, double PHI)
        {
            //Compute meridional arc.
            //Input: - _
            // ellipsoid semi major axis multiplied by central meridian scale factor (bf0) in meters; _
            // n (computed from a, b and f0); _
            // lat of false origin (PHI0) and initial or final latitude of point (PHI) IN RADIANS.

            //THIS FUNCTION IS CALLED BY THE - _
            // "Lat_Long_to_North" and "InitialLat" FUNCTIONS
            // THIS FUNCTION IS ALSO USED ON IT'S OWN IN THE "Projection and Transformation Calculations.xls" SPREADSHEET

            // The coefficients are fractions such as 5.0 / 4.0. The original's 5 / 4 is integer division in C#, which gave 1
            // and put northings up to about 0.8 m out towards the north and south coasts.
            return bf0 * (((1 + n + ((5.0 / 4.0) * Math.Pow(n, 2)) + ((5.0 / 4.0) * Math.Pow(n, 3))) * (PHI - PHI0)) - (((3 * n) + (3 * Math.Pow(n, 2)) + ((21.0 / 8.0) * Math.Pow(n, 3))) * (Math.Sin(PHI - PHI0)) * (Math.Cos(PHI + PHI0))) + ((((15.0 / 8.0) * Math.Pow(n, 2)) + ((15.0 / 8.0) * Math.Pow(n, 3))) * (Math.Sin(2 * (PHI - PHI0))) * (Math.Cos(2 * (PHI + PHI0)))) - (((35.0 / 24.0) * Math.Pow(n, 3)) * (Math.Sin(3 * (PHI - PHI0))) * (Math.Cos(3 * (PHI + PHI0)))));
        }

        #endregion Implementation
    }
}
