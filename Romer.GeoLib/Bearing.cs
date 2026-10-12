using System;

namespace Romer.GeoLib
{
    /// <summary>
    /// Angle arithmetic for compass bearings, in degrees.
    /// Bearings wrap around at 360, so ordinary arithmetic (means, differences) gives wrong answers near north.
    /// </summary>
    public static class Bearing
    {
        /// <summary>
        /// Normalise an angle to the range [0, 360).
        /// </summary>
        /// <param name="degrees">Angle in degrees</param>
        /// <returns>Equivalent angle in the range [0, 360)</returns>
        public static double Normalise(double degrees)
        {
            var d = degrees % 360D;
            if (d < 0D)
                d += 360D;
            return d >= 360D ? 0D : d;  // -1e-15 % 360 + 360 rounds to 360
        }


        /// <summary>
        /// Get the signed rotation that turns one angle into another by the shortest route.
        /// </summary>
        /// <param name="fromDegrees">Starting angle</param>
        /// <param name="toDegrees">Target angle</param>
        /// <returns>Rotation in the range [-180, 180). Positive is clockwise.</returns>
        public static double ShortestRotation(double fromDegrees, double toDegrees)
        {
            return Normalise(toDegrees - fromDegrees + 180D) - 180D;
        }
    }


    /// <summary>
    /// Exponential smoothing of a stream of bearings.
    /// Smooths the bearing's unit vector rather than its angle, so that readings either side of north
    /// (e.g. 359 and 1) average to north rather than south.
    /// </summary>
    public class BearingSmoother
    {
        /// <summary>
        /// Constructor. Initialise a BearingSmoother object.
        /// </summary>
        /// <param name="alpha">
        /// Weight given to each new reading, in the range (0, 1]. Smaller values smooth more but respond more slowly.
        /// </param>
        public BearingSmoother(double alpha)
        {
            if (alpha <= 0D || alpha > 1D)
                throw new ArgumentOutOfRangeException(nameof(alpha), "alpha must be in the range (0, 1]");
            this._alpha = alpha;
        }

        private readonly double _alpha;
        private double _x;
        private double _y;
        private bool _hasValue = false;


        /// <summary>
        /// Smoothed bearing in the range [0, 360), or null if no readings have been added.
        /// </summary>
        public double? Value
        {
            get
            {
                if (!_hasValue || (_x == 0D && _y == 0D))
                    return null;
                return Bearing.Normalise(Math.Atan2(_y, _x) * 180D / Math.PI);
            }
        }


        /// <summary>
        /// Add a reading.
        /// </summary>
        /// <param name="degrees">Bearing in degrees</param>
        /// <returns>The new smoothed bearing</returns>
        public double Add(double degrees)
        {
            var radians = degrees * Math.PI / 180D;
            var newX = Math.Cos(radians);
            var newY = Math.Sin(radians);
            if (_hasValue)
            {
                _x += _alpha * (newX - _x);
                _y += _alpha * (newY - _y);
            }
            else
            {
                _x = newX;
                _y = newY;
                _hasValue = true;
            }
            return Value ?? Bearing.Normalise(degrees);
        }


        /// <summary>
        /// Discard all readings.
        /// </summary>
        public void Reset()
        {
            _hasValue = false;
            _x = _y = 0D;
        }
    }
}
