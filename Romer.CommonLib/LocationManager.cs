using System;
using System.Threading;
using System.Threading.Tasks;

using Android.Content;
using Android.Content.PM;
using Android.OS;

using AndroidX.Core.Location;
using AndroidX.Core.Util;

using AndroidLocation = Android.Locations.Location;
using AndroidLocationManager = Android.Locations.LocationManager;


namespace Romer.CommonLib
{
    /// <summary>
    /// Location provider to get the location from.
    /// Note: These values are persisted and should not change.
    /// </summary>
    public enum LocationSource
    {
        /// <summary>The device's own GPS receiver</summary>
        BuiltInGps,
        /// <summary>The fused location provider, which combines GPS with other sources. Falls back to GPS on devices that lack it.</summary>
        Fused
    }


    /// <summary>
    /// Thrown when the device could not determine its location.
    /// </summary>
    public class LocationUnavailableException : Exception
    {
        /// <summary>
        /// Constructor. Initialise a LocationUnavailableException object.
        /// </summary>
        /// <param name="message">Message that describes the error</param>
        /// <param name="innerException">The exception that caused this one, or null</param>
        public LocationUnavailableException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }


    /// <summary>
    /// Gets the device's current location.
    /// </summary>
    public static class LocationManager
    {
        /// <summary>
        /// Determine if the device has a GPS receiver.
        /// </summary>
        /// <param name="context">Context</param>
        /// <returns>True if the device has a GPS receiver, otherwise false</returns>
        public static bool HasGps(Context context)
        {
            return context.PackageManager?.HasSystemFeature(PackageManager.FeatureLocationGps) ?? false;
        }


        /// <summary>
        /// Determine if the GPS location provider is enabled.
        /// </summary>
        /// <param name="context">Context</param>
        /// <returns>True if the GPS location provider is enabled, otherwise false</returns>
        public static bool IsGpsEnabled(Context context)
        {
            var locationMgr = (AndroidLocationManager?)context.GetSystemService(Context.LocationService);
            return locationMgr?.IsProviderEnabled(AndroidLocationManager.GpsProvider) ?? false;
        }


        /// <summary>
        /// Get the current location.
        /// Must be called on the UI thread. The caller must hold the ACCESS_FINE_LOCATION permission.
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="locationSource">Location provider to use. Fused falls back to GPS on devices that lack it.</param>
        /// <param name="timeout">Maximum time to wait for a location fix</param>
        /// <param name="cancellationToken">Cancels the request</param>
        /// <returns>Current location</returns>
        /// <exception cref="TimeoutException">No fix within the timeout</exception>
        /// <exception cref="LocationUnavailableException">The location provider could not get a fix</exception>
        /// <exception cref="System.OperationCanceledException">The request was cancelled</exception>
        public static async Task<AndroidLocation> GetCurrentLocationAsync(
            Context context,
            LocationSource locationSource,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var locationMgr = (AndroidLocationManager?)context.GetSystemService(Context.LocationService)
                ?? throw new LocationUnavailableException("Location service not available");
            var provider = ChooseProvider(locationMgr, locationSource);

            var consumer = new LocationConsumer();
            var signal = new CancellationSignal();
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var timeoutReg = timeoutCts.Token.Register(() =>
            {
                if (consumer.Tcs.TrySetException(new TimeoutException($"No location fix within {timeout.TotalSeconds:0} seconds")))
                    signal.Cancel();
            });
            using var cancelReg = cancellationToken.Register(() =>
            {
                if (consumer.Tcs.TrySetCanceled(cancellationToken))
                    signal.Cancel();
            });

            try
            {
                // Delivers the result, or null if the provider gave up, to consumer on the main thread.
                LocationManagerCompat.GetCurrentLocation(locationMgr, provider, signal, context.MainExecutor!, consumer);
            }
            catch (Exception ex)
            {
                consumer.Tcs.TrySetException(new LocationUnavailableException($"Could not request location from {provider} provider", ex));
            }

            return await consumer.Tcs.Task.ConfigureAwait(true);
        }


        private static string ChooseProvider(AndroidLocationManager locationMgr, LocationSource locationSource)
        {
            if (locationSource == LocationSource.Fused &&
                OperatingSystem.IsAndroidVersionAtLeast(31) &&
                locationMgr.HasProvider(AndroidLocationManager.FusedProvider))
            {
                return AndroidLocationManager.FusedProvider;
            }
            return AndroidLocationManager.GpsProvider;
        }


        private sealed class LocationConsumer : Java.Lang.Object, IConsumer
        {
            public TaskCompletionSource<AndroidLocation> Tcs { get; } = new TaskCompletionSource<AndroidLocation>(TaskCreationOptions.RunContinuationsAsynchronously);

            public void Accept(Java.Lang.Object? obj)
            {
                if (obj is AndroidLocation location)
                    Tcs.TrySetResult(location);
                else
                    Tcs.TrySetException(new LocationUnavailableException("The location provider could not get a fix"));
            }
        }
    }
}
