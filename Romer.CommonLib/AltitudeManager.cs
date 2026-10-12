using System;
using System.Threading;
using System.Threading.Tasks;

using Android.Content;
using Android.Hardware;
using Android.Runtime;


namespace Romer.CommonLib
{
    /// <summary>
    /// Measures altitude using the device's pressure sensor.
    /// The pressure sensor is not calibrated against local sea-level pressure, so readings can be
    /// wrong by tens to hundreds of metres depending on the weather. Prefer GPS altitude where available.
    /// </summary>
    public static class AltitudeManager
    {
        /// <summary>
        /// Determine if the device has a pressure sensor for measuring altitude.
        /// </summary>
        /// <param name="context">Context</param>
        /// <returns>True if the device has a pressure sensor, otherwise false</returns>
        public static bool HasAltitudeSensor(Context context)
        {
            var sensorManager = (SensorManager?)context.GetSystemService(Context.SensorService);
            return sensorManager?.GetDefaultSensor(SensorType.Pressure) != null;
        }


        /// <summary>
        /// Get the current altitude from the pressure sensor, assuming standard sea-level pressure.
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="timeout">Maximum time to wait for a reading</param>
        /// <param name="cancellationToken">Cancels the request</param>
        /// <returns>Altitude in metres</returns>
        /// <exception cref="InvalidOperationException">The device has no pressure sensor</exception>
        /// <exception cref="TimeoutException">No reading within the timeout</exception>
        /// <exception cref="System.OperationCanceledException">The request was cancelled</exception>
        public static async Task<double> GetCurrentAltitudeAsync(
            Context context,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sensorManager = (SensorManager?)context.GetSystemService(Context.SensorService);
            var sensor = sensorManager?.GetDefaultSensor(SensorType.Pressure)
                ?? throw new InvalidOperationException("No pressure sensor");

            var listener = new PressureListener();
            if (!sensorManager.RegisterListener(listener, sensor, SensorDelay.Normal))
                throw new InvalidOperationException("Could not register pressure sensor listener");
            try
            {
                using var timeoutCts = new CancellationTokenSource(timeout);
                using var timeoutReg = timeoutCts.Token.Register(() =>
                    listener.Tcs.TrySetException(new TimeoutException($"No pressure reading within {timeout.TotalSeconds:0} seconds")));
                using var cancelReg = cancellationToken.Register(() => listener.Tcs.TrySetCanceled(cancellationToken));

                var pressure = await listener.Tcs.Task.ConfigureAwait(true);
                return SensorManager.GetAltitude(SensorManager.PressureStandardAtmosphere, pressure);
            }
            finally
            {
                sensorManager.UnregisterListener(listener);
            }
        }


        private sealed class PressureListener : Java.Lang.Object, ISensorEventListener
        {
            public TaskCompletionSource<float> Tcs { get; } = new TaskCompletionSource<float>(TaskCreationOptions.RunContinuationsAsynchronously);

            public void OnSensorChanged(SensorEvent? e)
            {
                if (e?.Values?.Count > 0)
                    Tcs.TrySetResult(e.Values[0]);
            }

            public void OnAccuracyChanged(Sensor? sensor, [GeneratedEnum] SensorStatus accuracy)
            {
            }
        }
    }
}
