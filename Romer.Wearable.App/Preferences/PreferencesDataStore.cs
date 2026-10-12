using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using AndroidX.Preference;

using Romer.CommonLib;


namespace Romer.Wearable.App.Preferences
{
    /// <summary>
    /// Datastore class used by the preferences UI framework.
    /// Acts as a front-end to the <see cref="UserSettings"/> class, which does the actual persistence.
    /// Note that the Android preferences framework stores list preference values as strings by calling GetString() and PutString().
    /// </summary>
    public class PreferencesDataStore : PreferenceDataStore
    {
        /// <summary>
        /// Constructor. Initialise a PreferencesDataStore object.
        /// </summary>
        /// <param name="context">Context</param>
        public PreferencesDataStore(Context context)
        {
            this._context = context;
        }

        private readonly Context _context;


        /// <inheritdoc/>
        public override string? GetString(string? key, string? defValue)
        {
            var settings = UserSettings.Load(this._context);
            if (key == this._context.GetString(Resource.String.prefs_navigation_gridReferencePrecision_key))
                return settings.Navigation_GridReferencePrecision.ToString();
            else if (key == this._context.GetString(Resource.String.prefs_navigation_compassBezelMode_key))
                return ((int)settings.Navigation_CompassBezelMode).ToString();
            else if (key == this._context.GetString(Resource.String.prefs_navigation_measurementUnitSystem_key))
                return ((int)settings.Navigation_MeasurementUnitSystem).ToString();
            else if (key == this._context.GetString(Resource.String.prefs_advanced_locationSource_key))
                return ((int)settings.Advanced_LocationSource).ToString();
            else if (key == this._context.GetString(Resource.String.prefs_advanced_locationFixTimeout_key))
                return settings.Advanced_LocationFixTimeout.TotalSeconds.ToString();
            else
                return defValue;
        }


        /// <inheritdoc/>
        public override void PutString(string? key, string? value)
        {
            if (key == null || value == null)
                return;
            var settings = UserSettings.Load(this._context);
            if (key == this._context.GetString(Resource.String.prefs_navigation_gridReferencePrecision_key))
                settings.Navigation_GridReferencePrecision = int.Parse(value);
            else if (key == this._context.GetString(Resource.String.prefs_navigation_compassBezelMode_key))
                settings.Navigation_CompassBezelMode = (CompassView.BezelModes)int.Parse(value);
            else if (key == this._context.GetString(Resource.String.prefs_navigation_measurementUnitSystem_key))
                settings.Navigation_MeasurementUnitSystem = (MeasurementUnitSystem)int.Parse(value);
            else if (key == this._context.GetString(Resource.String.prefs_advanced_locationSource_key))
                settings.Advanced_LocationSource = (LocationSource)int.Parse(value);
            else if (key == this._context.GetString(Resource.String.prefs_advanced_locationFixTimeout_key))
                settings.Advanced_LocationFixTimeout = TimeSpan.FromSeconds(int.Parse(value));
            settings.Save(this._context);
        }


        /// <inheritdoc/>
        public override bool GetBoolean(string? key, bool defValue)
        {
            var settings = UserSettings.Load(this._context);
            if (key == this._context.GetString(Resource.String.prefs_navigation_nightColoursMode_key))
                return settings.Navigation_NightColoursMode;
            else if (key == this._context.GetString(Resource.String.prefs_advanced_locationFetchOnStartup_key))
                return settings.Advanced_LocationFetchOnStartup;
            else if (key == this._context.GetString(Resource.String.prefs_advanced_keepScreenOnDuringLocationFetch_key))
                return settings.Advanced_KeepScreenOnDuringLocationFetch;
            else
                return defValue;
        }


        /// <inheritdoc/>
        public override void PutBoolean(string? key, bool value)
        {
            var settings = UserSettings.Load(this._context);
            if (key == this._context.GetString(Resource.String.prefs_navigation_nightColoursMode_key))
                settings.Navigation_NightColoursMode = value;
            else if (key == this._context.GetString(Resource.String.prefs_advanced_locationFetchOnStartup_key))
                settings.Advanced_LocationFetchOnStartup = value;
            else if (key == this._context.GetString(Resource.String.prefs_advanced_keepScreenOnDuringLocationFetch_key))
                settings.Advanced_KeepScreenOnDuringLocationFetch = value;
            settings.Save(this._context);

            if (key == this._context.GetString(Resource.String.prefs_navigation_nightColoursMode_key))
            {
                UserSettings.NotifySettingChanged(key);
            }
        }
    }
}
