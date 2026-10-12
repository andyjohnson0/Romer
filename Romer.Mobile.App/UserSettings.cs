using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Romer.GeoLib;
using Romer.CommonLib;



namespace Romer.Mobile.App
{
    /// <summary>
    /// Settings that the user can change, stored in the app's shared preferences.
    /// </summary>
    public class UserSettings
    {
        private const string schemaVersionKey = "_schema_version";
        private const int schemaVersionId = 1;

        private static readonly UserSettings _defaults = new UserSettings();


        /// <summary>
        /// Load the settings. Settings that haven't been saved have their default values.
        /// </summary>
        /// <param name="context">Context</param>
        /// <returns>Settings</returns>
        public static UserSettings Load(Context context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            using (var prefs = context.GetSharedPreferences(context.PackageName + "_User", FileCreationMode.Private) ?? throw new InvalidOperationException("Shared preferences not available"))
            {
                var settings = new UserSettings()
                {
                    Navigation_MeasurementUnitSystem = (MeasurementUnitSystem)prefs.GetInt(context.GetString(Resource.String.prefs_navigation_measurementUnitSystem_key),
                                                                                          (int)_defaults.Navigation_MeasurementUnitSystem)
                };
                return settings;
            }
        }


        /// <summary>
        /// Save the settings, replacing all the stored settings.
        /// </summary>
        /// <param name="context">Context</param>
        public void Save(Context context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            using (var prefs = context.GetSharedPreferences(context.PackageName + "_User", FileCreationMode.Private) ?? throw new InvalidOperationException("Shared preferences not available"))
            {
                using (var editor = prefs.Edit() ?? throw new InvalidOperationException("Shared preferences not editable"))
                {
                    editor.Clear();
                    editor.PutInt(schemaVersionKey, schemaVersionId);
                    editor.PutInt(context.GetString(Resource.String.prefs_navigation_measurementUnitSystem_key),
                                  (int)this.Navigation_MeasurementUnitSystem);
                    editor.Apply();
                }
            }
        }


        #region Navigation

        /// <summary>
        /// Measurement unit system
        /// </summary>
        public MeasurementUnitSystem Navigation_MeasurementUnitSystem { get; set; } = MeasurementUnitSystem.Metric;


        /// <summary>
        /// Preferred distance unit: kilometres or miles.
        /// This is a derived, non-persisted setting.
        /// </summary>
        public LengthUnit Navigation_PreferredDistanceUnit
        {
            get { return this.Navigation_MeasurementUnitSystem == MeasurementUnitSystem.Metric ? LengthUnit.Kilometre : LengthUnit.Mile; }
        }


        /// <summary>
        /// Preferred altitude unit: metres or feet.
        /// This is a derived, non-persisted setting.
        /// </summary>
        public LengthUnit Navigation_PreferredAltitudeUnit
        {
            get { return this.Navigation_MeasurementUnitSystem == MeasurementUnitSystem.Metric ? LengthUnit.Metre : LengthUnit.Foot; }
        }

        #endregion Navigation


    }
}
