using System.Text.Json;
using System.Text.Json.Serialization;

using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Hardware;
using Android.OS;

using AndroidX.Core.App;
using AndroidX.Core.Content.PM;

using AndroidLocationManager = Android.Locations.LocationManager;


namespace Romer.CommonLib
{
    /// <summary>
    /// Diagnostic information about a device's hardware and software environment.
    /// Contains no personally identifying information.
    /// </summary>
    public class DiagnosticInfo
    {
        /// <summary>
        /// Collect diagnostic information about the device that the app is running on.
        /// Each section is collected independently, so a failure in one leaves the others intact.
        /// </summary>
        /// <param name="context">Context</param>
        /// <returns>Diagnostic information</returns>
        public static DiagnosticInfo CreateFrom(Context context)
        {
            var di = new DiagnosticInfo();
            di.Timestamp = DateTime.UtcNow;
            var packageManager = context.PackageManager;

            // Device
            try
            {
                di.DeviceType = packageManager?.HasSystemFeature(PackageManager.FeatureWatch) == true ? "Watch" : "Phone";
                di.DeviceMake = Build.Manufacturer;
                di.DeviceModel = Build.Model;
                di.DeviceProduct = Build.Product;
                di.DeviceOsRelease = Build.VERSION.Release;
                di.DeviceOsSdk = (int)Build.VERSION.SdkInt;
                if (context.GetSystemService(Context.ActivityService) is ActivityManager actManager)
                {
                    var memInfo = new ActivityManager.MemoryInfo();
                    actManager.GetMemoryInfo(memInfo);
                    di.DeviceMemory = memInfo.TotalMem;
                }
            }
            catch (Exception ex) { di.Errors.Add($"Device: {ex.Message}"); }

            // Location
            try
            {
                di.HasGps = packageManager?.HasSystemFeature(PackageManager.FeatureLocationGps) ?? false;
                di.HasNetworkLocation = packageManager?.HasSystemFeature(PackageManager.FeatureLocationNetwork) ?? false;
                di.HasLocationPermission = context.CheckSelfPermission(Manifest.Permission.AccessFineLocation) == Permission.Granted;
                if (context.GetSystemService(Context.LocationService) is AndroidLocationManager locationMgr)
                {
                    di.LocationProviders = locationMgr.AllProviders
                        .Select(name => LocationProviderInfo.CreateFrom(locationMgr, name))
                        .ToArray();
                }
            }
            catch (Exception ex) { di.Errors.Add($"Location: {ex.Message}"); }

            // Sensors
            try
            {
                if (context.GetSystemService(Context.SensorService) is SensorManager sensorManager)
                {
                    di.MagnetometerSensor = SensorInfo.CreateFrom(sensorManager.GetDefaultSensor(SensorType.MagneticField));
                    di.AccelerometerSensor = SensorInfo.CreateFrom(sensorManager.GetDefaultSensor(SensorType.Accelerometer));
                    di.RotationVectorSensor = SensorInfo.CreateFrom(sensorManager.GetDefaultSensor(SensorType.RotationVector));
                    di.PressureSensor = SensorInfo.CreateFrom(sensorManager.GetDefaultSensor(SensorType.Pressure));
                }
            }
            catch (Exception ex) { di.Errors.Add($"Sensors: {ex.Message}"); }

            // Display
            try
            {
                var displayMetrics = context.Resources?.DisplayMetrics;
                if (displayMetrics != null)
                {
                    di.DisplayWidth = displayMetrics.WidthPixels;
                    di.DisplayHeight = displayMetrics.HeightPixels;
                    di.DisplayDensityDpi = (int)displayMetrics.DensityDpi;
                }
                di.DisplayIsRound = context.Resources?.Configuration?.IsScreenRound ?? false;
            }
            catch (Exception ex) { di.Errors.Add($"Display: {ex.Message}"); }

            // App
            try
            {
                di.PackageName = context.PackageName;
                di.AppVersionName = context.GetAppVersionName();
                if (packageManager != null && context.PackageName != null)
                {
                    var pkgInfo = OperatingSystem.IsAndroidVersionAtLeast(33)
                        ? packageManager.GetPackageInfo(context.PackageName, PackageManager.PackageInfoFlags.Of(0L))
                        : GetPackageInfoLegacy(packageManager, context.PackageName);
                    if (pkgInfo != null)
                    {
                        di.AppVersionCode = PackageInfoCompat.GetLongVersionCode(pkgInfo);
                        di.AppLastUpdate = DateTimeOffset.FromUnixTimeMilliseconds(pkgInfo.LastUpdateTime).UtcDateTime;
                    }
                }
                di.NotificationsEnabled = NotificationManagerCompat.From(context)?.AreNotificationsEnabled() ?? false;
            }
            catch (Exception ex) { di.Errors.Add($"App: {ex.Message}"); }

            return di;
        }


#pragma warning disable CA1422 // Only called below API 33, where the replacement overload doesn't exist
        private static PackageInfo? GetPackageInfoLegacy(PackageManager packageManager, string packageName)
            => packageManager.GetPackageInfo(packageName, 0);
#pragma warning restore CA1422


        /// <summary>
        /// Serialise to JSON.
        /// </summary>
        /// <param name="humanReadable">Indent the JSON and write enums as names</param>
        /// <returns>JSON string</returns>
        public string ToJson(bool humanReadable = false)
        {
            return JsonSerializer.Serialize(this, humanReadable ? _humanReadableOptions : JsonSerializerOptions.Default);
        }

        private static readonly JsonSerializerOptions _humanReadableOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };


        // Properties rather than fields, because the JSON serialiser ignores fields by default.

        /// <summary>When the information was collected (UTC).</summary>
        public DateTime Timestamp { get; private set; }

        // Device

        /// <summary>"Watch" or "Phone".</summary>
        public string? DeviceType { get; private set; }

        /// <summary>Manufacturer, from <see cref="Build.Manufacturer"/>.</summary>
        public string? DeviceMake { get; private set; }

        /// <summary>Model name, from <see cref="Build.Model"/>.</summary>
        public string? DeviceModel { get; private set; }

        /// <summary>Product name, from <see cref="Build.Product"/>.</summary>
        public string? DeviceProduct { get; private set; }

        /// <summary>Android version, e.g. "13".</summary>
        public string? DeviceOsRelease { get; private set; }

        /// <summary>Android API level, e.g. 33.</summary>
        public int DeviceOsSdk { get; private set; }

        /// <summary>Total memory in bytes.</summary>
        public long DeviceMemory { get; private set; }

        // Location

        /// <summary>True if the device has a GPS receiver.</summary>
        public bool HasGps { get; private set; }

        /// <summary>True if the device can get its location from the network.</summary>
        public bool HasNetworkLocation { get; private set; }

        /// <summary>True if the app has the fine location permission.</summary>
        public bool HasLocationPermission { get; private set; }

        /// <summary>The device's location providers.</summary>
        public LocationProviderInfo[] LocationProviders { get; private set; } = Array.Empty<LocationProviderInfo>();

        // Sensors

        /// <summary>Magnetometer, or null if the device doesn't have one.</summary>
        public SensorInfo? MagnetometerSensor { get; private set; }

        /// <summary>Accelerometer, or null if the device doesn't have one.</summary>
        public SensorInfo? AccelerometerSensor { get; private set; }

        /// <summary>Rotation vector sensor, or null if the device doesn't have one.</summary>
        public SensorInfo? RotationVectorSensor { get; private set; }

        /// <summary>Pressure sensor, or null if the device doesn't have one.</summary>
        public SensorInfo? PressureSensor { get; private set; }

        // Display

        /// <summary>Display width in pixels.</summary>
        public int DisplayWidth { get; private set; }

        /// <summary>Display height in pixels.</summary>
        public int DisplayHeight { get; private set; }

        /// <summary>Display density in dots per inch.</summary>
        public int DisplayDensityDpi { get; private set; }

        /// <summary>True if the display is round, as on many watches.</summary>
        public bool DisplayIsRound { get; private set; }

        // App

        /// <summary>The app's package name.</summary>
        public string? PackageName { get; private set; }

        /// <summary>The app's version name, e.g. "1.0".</summary>
        public string? AppVersionName { get; private set; }

        /// <summary>The app's version code.</summary>
        public long AppVersionCode { get; private set; }

        /// <summary>When the app was last installed or updated (UTC).</summary>
        public DateTime AppLastUpdate { get; private set; }

        /// <summary>True if the app may post notifications.</summary>
        public bool NotificationsEnabled { get; private set; }

        /// <summary>
        /// Errors encountered while collecting the information.
        /// </summary>
        public List<string> Errors { get; private set; } = new List<string>();


        /// <summary>
        /// Information about a sensor.
        /// </summary>
        public class SensorInfo
        {
            /// <summary>
            /// Collect information about a sensor.
            /// </summary>
            /// <param name="sensor">Sensor, or null</param>
            /// <returns>Sensor information, or null if the sensor is null</returns>
            public static SensorInfo? CreateFrom(Sensor? sensor)
            {
                if (sensor == null)
                    return null;
                return new SensorInfo()
                {
                    Name = sensor.Name,
                    Vendor = sensor.Vendor,
                    Version = sensor.Version,
                    Resolution = sensor.Resolution
                };
            }

            /// <summary>Sensor name.</summary>
            public string? Name { get; private set; }

            /// <summary>Sensor vendor.</summary>
            public string? Vendor { get; private set; }

            /// <summary>Version of the sensor's module.</summary>
            public int Version { get; private set; }

            /// <summary>Resolution in the sensor's units.</summary>
            public float Resolution { get; private set; }
        }


        /// <summary>
        /// Information about a location provider.
        /// </summary>
        public class LocationProviderInfo
        {
            /// <summary>
            /// Collect information about a location provider.
            /// </summary>
            /// <param name="locationMgr">Location manager</param>
            /// <param name="providerName">Provider name, e.g. "gps"</param>
            /// <returns>Location provider information</returns>
            public static LocationProviderInfo CreateFrom(AndroidLocationManager locationMgr, string providerName)
            {
                var info = new LocationProviderInfo()
                {
                    Name = providerName,
                    IsEnabled = locationMgr.IsProviderEnabled(providerName)
                };
                if (OperatingSystem.IsAndroidVersionAtLeast(31))
                {
                    var properties = locationMgr.GetProviderProperties(providerName);
                    info.Accuracy = properties?.Accuracy.ToString();
                    info.PowerUsage = properties?.PowerUsage.ToString();
                }
                return info;
            }

            /// <summary>Provider name, e.g. "gps".</summary>
            public string? Name { get; private set; }

            /// <summary>True if the provider is enabled.</summary>
            public bool IsEnabled { get; private set; }

            /// <summary>The provider's accuracy class, or null if not available (always below Android 12, API 31).</summary>
            public string? Accuracy { get; private set; }

            /// <summary>The provider's power usage class, or null if not available (always below Android 12, API 31).</summary>
            public string? PowerUsage { get; private set; }
        }
    }
}
