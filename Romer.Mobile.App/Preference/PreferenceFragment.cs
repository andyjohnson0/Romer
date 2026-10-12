using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

using AndroidX.Preference;

using Kilsten.Android;

using Romer.CommonLib;


namespace Romer.Mobile.App.Preference
{
    /// <summary>
    /// The settings, from Resources/xml/preferences.xml. Shows the dialogs for the Diagnostics, About and Credits
    /// entries.
    /// </summary>
    public class PreferenceFragment : PreferenceFragmentCompat
    {
        /// <inheritdoc/>
        public override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }


        /// <inheritdoc/>
        public override void OnCreatePreferences(Bundle? savedInstanceState, string? rootKey)
        {
            this.PreferenceManager!.PreferenceDataStore = new PreferenceDataStore(RequireContext()!);
            SetPreferencesFromResource(Resource.Xml.preferences, rootKey);
        }


        /// <inheritdoc/>
        public override void OnDisplayPreferenceDialog(AndroidX.Preference.Preference preference)
        {
            if (preference.Key == GetString(Resource.String.prefs_app_diagnostics_key))
            {
                // Diagnostics has its own dialog fragment, which doesn't need a target preference.
                new DiagnosticDialogFragment().Show(this.ParentFragmentManager, null);
                return;
            }

            if (preference.Key == GetString(Resource.String.prefs_app_about_key))
            {
                InfoDialogFragment.NewInstance<InfoDialogFragment>(InfoDialogKind.About, Resource.Layout.preference_dialog_about, preference.Title)
                    .Show(this.ParentFragmentManager, null);
            }
            else if (preference.Key == GetString(Resource.String.prefs_app_credits_key))
            {
                InfoDialogFragment.NewInstance<InfoDialogFragment>(InfoDialogKind.Credits, Resource.Layout.preference_dialog_text, preference.Title)
                    .Show(this.ParentFragmentManager, null);
            }
            else
            {
                base.OnDisplayPreferenceDialog(preference);
            }
        }
    }
}
