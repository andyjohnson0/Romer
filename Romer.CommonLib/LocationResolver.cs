using System;
using System.Threading;
using System.Threading.Tasks;

using Android.Content;

using AndroidLocation = Android.Locations.Location;

namespace Romer.CommonLib
{
    /// <summary>
    /// Source of altitude information
    /// </summary>
    public enum AltitudeSource
    {
        /// <summary>No altitude information</summary>
        None = 0,
        /// <summary>Altitude came from GPS</summary>
        Gps = 1,
        /// <summary>Altitude came from pressure sensor</summary>
        PressureSensor = 2
    }


    /// <summary>
    /// A location and the source of its altitude.
    /// </summary>
    /// <param name="Location">Location</param>
    /// <param name="AltitudeSource">Where the altitude came from</param>
    public sealed record ResolvedLocation(AndroidLocation Location, AltitudeSource AltitudeSource);


    /// <summary>
    /// Location resolver.
    /// Gets the location from GPS (local or fused). Altitude comes from the location fix or, if the fix has no
    /// altitude, from the pressure sensor.
    /// </summary>
    public static class LocationResolver
    {
        /// <summary>
        /// Maximum time to wait for a pressure sensor reading.
        /// </summary>
        private static readonly TimeSpan _pressureSensorTimeout = TimeSpan.FromSeconds(5);


        /// <summary>
        /// Resolve the current location and altitude.
        /// Must be called on the UI thread.
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="timeout">Maximum time to wait for a location fix</param>
        /// <param name="locationSource">Location source</param>
        /// <param name="cancellationToken">Cancels the request</param>
        /// <returns>Resolved location</returns>
        /// <exception cref="TimeoutException">No location fix within the timeout</exception>
        /// <exception cref="LocationUnavailableException">The location provider could not get a fix</exception>
        /// <exception cref="System.OperationCanceledException">The request was cancelled</exception>
        public static async Task<ResolvedLocation> ResolveCurrentLocationAsync(
            Context context,
            TimeSpan timeout,
            LocationSource locationSource,
            CancellationToken cancellationToken = default)
        {
            var location = await LocationManager.GetCurrentLocationAsync(context, locationSource, timeout, cancellationToken);
            if (location.HasAltitude)
            {
                return new ResolvedLocation(location, AltitudeSource.Gps);
            }

            if (AltitudeManager.HasAltitudeSensor(context))
            {
                try
                {
                    var altitude = await AltitudeManager.GetCurrentAltitudeAsync(context, _pressureSensorTimeout, cancellationToken);
                    var withAltitude = new AndroidLocation(location)
                    {
                        Altitude = altitude
                    };
                    return new ResolvedLocation(withAltitude, AltitudeSource.PressureSensor);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Altitude is optional. Use the location without it.
                }
            }

            return new ResolvedLocation(location, AltitudeSource.None);
        }
    }
}
