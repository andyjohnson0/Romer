using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Android.Runtime;

using AndroidX.Preference;

using Romer.CommonLib;


namespace Romer.Wearable.App.Preferences
{
    /// <summary>
    /// The first page of settings, from Resources/xml/preferences.xml.
    /// </summary>
    [Register("uk.andyjohnson.romer.wearable.app.preferences.RootPreferencesFragment")]
    public class RootPreferencesFragment : PreferencesFragmentBase
    {
        /// <inheritdoc/>
        public override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }


        /// <inheritdoc/>
        public override string PrefsTitle
        {
            get { return RequireContext()!.GetString(Resource.String.prefs_title); }
        }


        /// <inheritdoc/>
        public override void OnCreatePreferences(Bundle? savedInstanceState, string? rootKey)
        {
            base.OnCreatePreferences(savedInstanceState, rootKey);

            var context = RequireContext()!;
            this.PreferenceManager!.PreferenceDataStore = new PreferencesDataStore(context);
            SetPreferencesFromResource(Resource.Xml.preferences, rootKey);

            // Selectively enable compass style preference. FindPreference() searches nested categories.
            var pref = FindPreference(context.GetString(Resource.String.prefs_navigation_compassBezelMode_key));
            if (pref != null)
                pref.Enabled = CompassManager.HasMagnetometer(context);
        }
    }
}
