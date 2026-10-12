using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Icu.Text;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;


using Romer.GeoLib;
using Romer.CommonLib;


namespace Romer.Wearable.App
{
    /// <summary>
    /// Settings that the user can change, stored in the app's shared preferences.
    /// </summary>
    public class UserSettings
    {
        private const string schemaVersionKey = "_schema_version";
        private const int schemaVersionId = 1;

        private static readonly UserSettings _defaults = new UserSettings();


        private static ISharedPreferences GetSharedPreferences(Context context)
        {
            return context.GetSharedPreferences(context.PackageName + "_User", FileCreationMode.Private)
                ?? throw new InvalidOperationException("Shared preferences not available");
        }


        /// <summary>
        /// Load the settings. Settings that haven't been saved have their default values.
        /// </summary>
        /// <param name="context">Context</param>
        /// <returns>Settings</returns>
        public static UserSettings Load(Context context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            using (var prefs = UserSettings.GetSharedPreferences(context))
            {
                var settings = new UserSettings()
                {
                    Navigation_GridReferencePrecision = prefs.GetInt(context.GetString(Resource.String.prefs_navigation_gridReferencePrecision_key),
                                                                     _defaults.Navigation_GridReferencePrecision),
                    Navigation_CompassBezelMode = (CompassView.BezelModes)prefs.GetInt(context.GetString(Resource.String.prefs_navigation_compassBezelMode_key),
                                                                                       (int)_defaults.Navigation_CompassBezelMode),
                    Navigation_NightColoursMode = prefs.GetBoolean(context.GetString(Resource.String.prefs_navigation_nightColoursMode_key),
                                                                   _defaults.Navigation_NightColoursMode),
                    Navigation_MeasurementUnitSystem = (MeasurementUnitSystem)prefs.GetInt(context.GetString(Resource.String.prefs_navigation_measurementUnitSystem_key),
                                                                                          (int)_defaults.Navigation_MeasurementUnitSystem),
                    Advanced_LocationFetchOnStartup = prefs.GetBoolean(context.GetString(Resource.String.prefs_advanced_locationFetchOnStartup_key),
                                                                       _defaults.Advanced_LocationFetchOnStartup),
                    Advanced_LocationSource = (LocationSource)prefs.GetInt(context.GetString(Resource.String.prefs_advanced_locationSource_key),
                                                                           (int)_defaults.Advanced_LocationSource),
                    Advanced_LocationFixTimeout = TimeSpan.FromSeconds(prefs.GetInt(context.GetString(Resource.String.prefs_advanced_locationFixTimeout_key),
                                                                                    (int)_defaults.Advanced_LocationFixTimeout.TotalSeconds)),
                    Advanced_KeepScreenOnDuringLocationFetch = prefs.GetBoolean(context.GetString(Resource.String.prefs_advanced_keepScreenOnDuringLocationFetch_key),
                                                                                _defaults.Advanced_KeepScreenOnDuringLocationFetch)
                };
                return settings;
            }
        }


        /// <summary>
        /// Save the settings, replacing all the stored settings.
        /// Doesn't raise <see cref="SettingChanged"/>. Call <see cref="NotifySettingChanged"/> for that.
        /// </summary>
        /// <param name="context">Context</param>
        public void Save(Context context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            using (var prefs = UserSettings.GetSharedPreferences(context))
            {
                using (var editor = prefs.Edit() ?? throw new InvalidOperationException("Shared preferences not editable"))
                {
                    editor.Clear();
                    editor.PutInt(schemaVersionKey, schemaVersionId);
                    editor.PutInt(context.GetString(Resource.String.prefs_navigation_gridReferencePrecision_key),
                                  this.Navigation_GridReferencePrecision);
                    editor.PutInt(context.GetString(Resource.String.prefs_navigation_compassBezelMode_key),
                                  (int)this.Navigation_CompassBezelMode);
                    editor.PutBoolean(context.GetString(Resource.String.prefs_navigation_nightColoursMode_key),
                                      this.Navigation_NightColoursMode);
                    editor.PutInt(context.GetString(Resource.String.prefs_navigation_measurementUnitSystem_key),
                                  (int)this.Navigation_MeasurementUnitSystem);
                    editor.PutBoolean(context.GetString(Resource.String.prefs_advanced_locationFetchOnStartup_key),
                                      this.Advanced_LocationFetchOnStartup);
                    editor.PutInt(context.GetString(Resource.String.prefs_advanced_locationSource_key),
                                  (int)this.Advanced_LocationSource);
                    editor.PutInt(context.GetString(Resource.String.prefs_advanced_locationFixTimeout_key),
                                  (int)this.Advanced_LocationFixTimeout.TotalSeconds);
                    editor.PutBoolean(context.GetString(Resource.String.prefs_advanced_keepScreenOnDuringLocationFetch_key),
                                      this.Advanced_KeepScreenOnDuringLocationFetch);
                    editor.Apply();
                }
            }
        }


        #region Navigation

        /// <summary>
        /// Grid reference precision in metres: 1, 10, 100, etc.
        /// </summary>
        public int Navigation_GridReferencePrecision { get; set; } = 10;

        /// <summary>
        /// How much detail the compass bezel shows, or <see cref="CompassView.BezelModes.None"/> to hide the compass
        /// </summary>
        public CompassView.BezelModes Navigation_CompassBezelMode { get; set; } = CompassView.BezelModes.Standard;

        /// <summary>
        /// Display in muted colours for night navigation
        /// </summary>
        public bool Navigation_NightColoursMode { get; set; } = false;

        /// <summary>
        /// Measurement unit system
        /// </summary>
        public MeasurementUnitSystem Navigation_MeasurementUnitSystem { get; set; } = MeasurementUnitSystem.Metric;

        /// <summary>
        /// Preferred distance unit: kilometres or miles.
        /// This is a derived, non-persisted setting.
        /// </summary>
        public LengthUnit PreferredDistanceUnit
        {
            get { return this.Navigation_MeasurementUnitSystem == MeasurementUnitSystem.Metric ? LengthUnit.Kilometre : LengthUnit.Mile; }
        }

        /// <summary>
        /// Preferred altitude unit: metres or feet.
        /// This is a derived, non-persisted setting.
        /// </summary>
        public LengthUnit PreferredAltitudeUnit
        {
            get { return this.Navigation_MeasurementUnitSystem == MeasurementUnitSystem.Metric ? LengthUnit.Metre : LengthUnit.Foot; }
        }

        #endregion Navigation


        #region Advanced

        /// <summary>
        /// Get the location when the app starts
        /// </summary>
        public bool Advanced_LocationFetchOnStartup { get; set; } = false;

        /// <summary>
        /// Location provider to get the location from
        /// </summary>
        public LocationSource Advanced_LocationSource { get; set; } = LocationSource.Fused;

        /// <summary>
        /// Maximum time to wait for a location fix. Stored in whole seconds.
        /// </summary>
        public TimeSpan Advanced_LocationFixTimeout { get; set; } = new TimeSpan(0, 0, 30);

        /// <summary>
        /// Keep the screen on when getting a location fix.
        /// </summary>
        public bool Advanced_KeepScreenOnDuringLocationFetch { get; set; } = true;

        #endregion Advanced


        #region Change notification

        /// <summary>
        /// Fires on the UI thread when the preferences screens change a setting whose change must be shown immediately.
        /// At present that's only <see cref="Navigation_NightColoursMode"/>.
        /// Subscribers must unsubscribe when they no longer need notifications, to avoid leaking.
        /// </summary>
        public static event EventHandler<SettingsChangeEventArgs>? SettingChanged;


        /// <summary>
        /// Notify SettingChanged subscribers that a user setting has changed.
        /// </summary>
        /// <param name="prefKey">Preference key of setting that has changed</param>
        public static void NotifySettingChanged(string prefKey)
        {
            SettingChanged?.Invoke(null, new SettingsChangeEventArgs() { PreferenceKey = prefKey });
        }


        /// <summary>
        /// Event arguments for user setting changes.
        /// </summary>
        public class SettingsChangeEventArgs : EventArgs
        {
            /// <summary>
            /// Preference key of changed setting.
            /// </summary>
            public required string PreferenceKey { get; init; }
        }

        #endregion Change notification
    }
}
