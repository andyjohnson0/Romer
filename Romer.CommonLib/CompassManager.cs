using System;

using Android.Content;
using Android.Hardware;
using Android.Runtime;

using Romer.GeoLib;


namespace Romer.CommonLib
{
    /// <summary>
    /// Provides magnetic compass bearings from the device's sensors.
    /// Uses the rotation vector sensor where available, otherwise the accelerometer and magnetometer.
    /// </summary>
    public class CompassManager : Java.Lang.Object, ISensorEventListener
    {
        /// <summary>
        /// Constructor. Initialise a CompassManager object. The compass is disabled until
        /// <see cref="SetCompassEnabled"/> is called.
        /// </summary>
        /// <param name="context">Context</param>
        public CompassManager(Context context)
        {
            this._context = context;
        }

        private readonly Context _context;

        // Weight given to each new reading. At SensorDelay.Ui (~16 readings/sec) this smooths out jitter over
        // roughly half a second.
        private readonly BearingSmoother _smoother = new BearingSmoother(alpha: 0.15);
        private int? _lastBearing = null;
        private bool _isEnabled = false;


        /// <summary>
        /// Fires when the (whole degree) bearing changes. Fired on the UI thread.
        /// </summary>
        public event EventHandler<CompassBearingEventArgs>? CompassBearing;

        /// <summary>
        /// Fires when sensor accuracy changes. Fired on the UI thread.
        /// </summary>
        public event EventHandler<CompassAccuracyEventArgs>? CompassAccuracy;


        /// <summary>
        /// Determine if the device has a magnetometer, which the compass needs.
        /// </summary>
        /// <param name="context">Context</param>
        /// <returns>True if the device has a magnetometer, otherwise false</returns>
        public static bool HasMagnetometer(Context context)
        {
            var sensorManager = (SensorManager?)context.GetSystemService(Context.SensorService);
            return sensorManager?.GetDefaultSensor(SensorType.MagneticField) != null;
        }


        /// <summary>
        /// Enable or disable the compass.
        /// Enabling it registers for sensor readings, and resets the smoothed bearing and the last reported accuracy,
        /// so that the next reading raises both events.
        /// </summary>
        /// <param name="enable">True to enable the compass, false to disable it</param>
        /// <returns>True if the compass is now enabled. False if it is disabled, or the device lacks the sensors.</returns>
        public bool SetCompassEnabled(bool enable)
        {
            var sensorManager = (SensorManager?)this._context.GetSystemService(Context.SensorService);
            if (sensorManager == null)
                return false;

            // Always unregister first, so that repeated enables don't register twice.
            sensorManager.UnregisterListener(this);
            _isEnabled = false;
            _smoother.Reset();
            _lastBearing = null;
            _lastAccuracy = null;

            if (enable && HasMagnetometer(this._context))
            {
                var rotationSensor = sensorManager.GetDefaultSensor(SensorType.RotationVector);
                if (rotationSensor != null)
                {
                    _isEnabled = sensorManager.RegisterListener(this, rotationSensor, SensorDelay.Ui);
                }
                else
                {
                    var magSensor = sensorManager.GetDefaultSensor(SensorType.MagneticField);
                    var accSensor = sensorManager.GetDefaultSensor(SensorType.Accelerometer);
                    if (magSensor != null && accSensor != null)
                    {
                        _isEnabled = sensorManager.RegisterListener(this, magSensor, SensorDelay.Ui) &&
                                    sensorManager.RegisterListener(this, accSensor, SensorDelay.Ui);
                    }
                }
            }
            return _isEnabled;
        }


        // Latest readings from the fallback sensors
        private readonly float[] _magnetometerData = new float[3];
        private readonly float[] _accelerometerData = new float[3];
        private bool _hasMagnetometerData = false;
        private bool _hasAccelerometerData = false;

        // Used only by OnSensorChanged(). Member variables to avoid allocations on each reading.
        private readonly float[] _rotationMatrix = new float[9];
        private readonly float[] _orientationAngles = new float[3];
        private readonly float[] _rotationVector = new float[5];


        void ISensorEventListener.OnSensorChanged(SensorEvent? e)
        {
            if (e?.Sensor == null || e.Values == null || !_isEnabled)
                return;

            ReportAccuracy(e.Sensor, e.Accuracy);

            switch (e.Sensor.Type)
            {
                case SensorType.RotationVector:
                    var count = Math.Min(e.Values.Count, _rotationVector.Length);
                    for (var i = 0; i < count; i++)
                        _rotationVector[i] = e.Values[i];
                    SensorManager.GetRotationMatrixFromVector(_rotationMatrix, count == _rotationVector.Length ? _rotationVector : _rotationVector[..count]);
                    break;

                case SensorType.MagneticField:
                case SensorType.Accelerometer:
                    var target = e.Sensor.Type == SensorType.MagneticField ? _magnetometerData : _accelerometerData;
                    for (var i = 0; i < 3; i++)
                        target[i] = e.Values[i];
                    _hasMagnetometerData |= e.Sensor.Type == SensorType.MagneticField;
                    _hasAccelerometerData |= e.Sensor.Type == SensorType.Accelerometer;
                    if (!_hasMagnetometerData || !_hasAccelerometerData)
                        return;
                    if (!SensorManager.GetRotationMatrix(_rotationMatrix, null, _accelerometerData, _magnetometerData))
                        return;  // Device in free-fall
                    break;

                default:
                    return;
            }

            SensorManager.GetOrientation(_rotationMatrix, _orientationAngles);
            var azimuthDeg = _orientationAngles[0] * 180D / Math.PI;
            var bearing = (int)Math.Round(_smoother.Add(azimuthDeg)) % 360;

            if (bearing != _lastBearing)
            {
                _lastBearing = bearing;
                CompassBearing?.Invoke(this, new CompassBearingEventArgs() { Bearing = bearing });
            }
        }


        void ISensorEventListener.OnAccuracyChanged(Sensor? sensor, [GeneratedEnum] SensorStatus accuracy)
        {
            ReportAccuracy(sensor, accuracy);
        }


        // The last accuracy reported, or null if none has been reported since the compass was enabled
        private SensorStatus? _lastAccuracy = null;


        /// <summary>
        /// Raise CompassAccuracy if the compass sensor's accuracy has changed.
        /// Android calls OnAccuracyChanged() only when the accuracy changes, so after the compass is re-enabled the
        /// current accuracy might never be reported that way. Every reading carries it too, so check both.
        /// </summary>
        private void ReportAccuracy(Sensor? sensor, SensorStatus accuracy)
        {
            // Only the sensors that measure direction. The accelerometer's accuracy isn't the compass's.
            if (sensor?.Type != SensorType.RotationVector && sensor?.Type != SensorType.MagneticField)
                return;
            if (accuracy == _lastAccuracy)
                return;

            _lastAccuracy = accuracy;
            CompassAccuracy?.Invoke(this, new CompassAccuracyEventArgs() { Accuracy = accuracy });
        }
    }


    /// <summary>
    /// Arguments for the <see cref="CompassManager.CompassBearing"/> event.
    /// </summary>
    public class CompassBearingEventArgs : EventArgs
    {
        /// <summary>
        /// Magnetic bearing in whole degrees, in the range [0, 360).
        /// </summary>
        public int Bearing { get; init; }
    }


    /// <summary>
    /// Arguments for the <see cref="CompassManager.CompassAccuracy"/> event.
    /// </summary>
    public class CompassAccuracyEventArgs : EventArgs
    {
        /// <summary>
        /// Accuracy of the compass sensor. Low or unreliable accuracy usually means that the compass needs calibrating.
        /// </summary>
        public SensorStatus Accuracy { get; init; }
    }
}
