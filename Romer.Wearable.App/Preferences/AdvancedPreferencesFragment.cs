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
    /// The advanced settings page, from Resources/xml/preferences_advanced.xml.
    /// </summary>
    [Register("uk.andyjohnson.romer.wearable.app.preferences.AdvancedPreferencesFragment")]
    public class AdvancedPreferencesFragment : PreferencesFragmentBase
    {
        /// <inheritdoc/>
        public override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }


        /// <inheritdoc/>
        public override string PrefsTitle
        {
            get { return RequireContext()!.GetString(Resource.String.prefs_advanced_title); }
        }
 

        /// <inheritdoc/>
        public override void OnCreatePreferences(Bundle? savedInstanceState, string? rootKey)
        {
            base.OnCreatePreferences(savedInstanceState, rootKey);

            var context = RequireContext()!;
            this.PreferenceManager!.PreferenceDataStore = new PreferencesDataStore(context);
            SetPreferencesFromResource(Resource.Xml.preferences_advanced, rootKey);

            // Selectively enable gps source preference. FindPreference() searches nested categories.
            var pref = FindPreference(context.GetString(Resource.String.prefs_advanced_locationSource_key));
            if (pref != null)
                pref.Enabled = LocationManager.HasGps(context);
        }
    }
}
