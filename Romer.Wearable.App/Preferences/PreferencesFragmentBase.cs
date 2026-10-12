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

using Romer.CommonLib;
using Kilsten.Android;
using AndroidX.Fragment.App;


namespace Romer.Wearable.App.Preferences
{
    /// <summary>
    /// Base class for the pages of settings. Sets the page's title in <see cref="PreferencesActivity"/>, shows the
    /// custom dialogs, and recreates the activity when night colours mode changes, so that the new theme applies.
    /// </summary>
    public abstract class PreferencesFragmentBase : PreferenceFragmentCompat
    {
        /// <summary>
        /// Argument: key of a preference to scroll to (string). Optional.
        /// </summary>
        public const string ScrollToPrefKeyKey = "scrollToPrefKeyKey";  // In


        /// <inheritdoc/>
        public override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // If activity wants us to scroll to a key then do so.
            // This is initiated in SettingsChanged() below.
            var scrollToKey = this.Arguments?.GetString(PreferencesFragmentBase.ScrollToPrefKeyKey);
            if (scrollToKey != null)
            {
                this.ScrollToPreference(scrollToKey);
            }
        }


        /// <inheritdoc/>
        public override void OnResume()
        {
            base.OnResume();

            // Set title in activity
            if (this.Activity is PreferencesActivity act)
            {
                act.SetPrefsTitle(this.PrefsTitle);
            }

            // Register to detect preference changes
            UserSettings.SettingChanged += SettingsChanged;
        }


        /// <inheritdoc/>
        public override void OnPause()
        {
            base.OnPause();

            // Unregister when the fragment is paused
            UserSettings.SettingChanged -= SettingsChanged;
        }



        private void SettingsChanged(object? sender, UserSettings.SettingsChangeEventArgs args)
        {
            if (args.PreferenceKey == RequireContext()!.GetString(Resource.String.prefs_navigation_nightColoursMode_key))
            {
                // Recreate the parent activity, asking it to ask us to scroll to the changed preference (see OnCreate() above)
                // The activity will read the changed user settings and apply the new theme.
                var activity = RequireActivity()!;
                activity.Intent?.PutExtra(PreferencesActivity.ScrollToPrefKeyKey, args.PreferenceKey);
                activity.Recreate();
            }
        }


        /// <summary>
        /// Title of this page of preferences, shown in <see cref="PreferencesActivity"/>'s header.
        /// </summary>
        public abstract string PrefsTitle { get; }


        /// <inheritdoc/>
        public override void OnCreatePreferences(Bundle? savedInstanceState, string? rootKey)
        {
            // Intentionally empty and doesn't call base.
        }


        /// <inheritdoc/>
        public override void OnDisplayPreferenceDialog(AndroidX.Preference.Preference preference)
        {
            if (preference is ListPreference lp)
            {
                // Display our custom ListPreferenceDialogFragment.
                var dialogFragment = ListPreferenceDialogFragment.NewInstance(lp.Key!);
                // androidx.preference finds a custom dialog's preference through its target fragment, so this deprecated call is still required.
#pragma warning disable CS0618
                dialogFragment.SetTargetFragment(this, 0);
#pragma warning restore CS0618
                dialogFragment.Show(this.ParentFragmentManager, "androidx.preference.PreferenceFragment.DIALOG");
            }
            else if (preference.Key == GetString(Resource.String.prefs_app_about_key))
            {
                InfoDialogFragment.NewInstance<WearInfoDialogFragment>(InfoDialogKind.About, Resource.Layout.preference_dialog_about, preference.Title)
                    .Show(this.ParentFragmentManager, null);
            }
            else if (preference.Key == GetString(Resource.String.prefs_app_credits_key))
            {
                InfoDialogFragment.NewInstance<WearInfoDialogFragment>(InfoDialogKind.Credits, Resource.Layout.preference_dialog_text, preference.Title)
                    .Show(this.ParentFragmentManager, null);
            }
            else
            {
                base.OnDisplayPreferenceDialog(preference);
            }
        }
    }
}
