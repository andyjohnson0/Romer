using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Android.App;
using Android.OS;
using Android.Views;

using AndroidX.AppCompat.App;
using AndroidX.Preference;
using AndroidX.Wear.Widget;




namespace Romer.Wearable.App.Preferences
{
    /// <summary>
    /// The settings screens. Starts with <see cref="RootPreferencesFragment"/>, and swiping right goes back a page or
    /// closes the settings.
    /// </summary>
    [Activity]
    public class PreferencesActivity : AppCompatActivity, PreferenceFragmentCompat.IOnPreferenceStartFragmentCallback
    {
        /// <summary>
        /// Intent extra: key of a preference to scroll to (string). Optional.
        /// </summary>
        public const string ScrollToPrefKeyKey = "scrollToPrefKeyKey";  // In

        /// <inheritdoc/>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            // Load the night or day colours theme
            var userSettings = UserSettings.Load(this);
            SetTheme(userSettings.Navigation_NightColoursMode ? Resource.Style.Theme_App_Settings_NightColours : Resource.Style.Theme_App_Settings);

            //
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.preferences_activity);

            var headingTxt = RequireViewById<TextView>(Resource.Id.prefHeaderTxt);
            headingTxt.SetTextColor(userSettings.Navigation_NightColoursMode
                ? new Android.Graphics.Color(AndroidX.Core.Content.ContextCompat.GetColor(this, Resource.Color.nightColorAccent))
                : new Android.Graphics.Color(AndroidX.Core.Content.ContextCompat.GetColor(this, Resource.Color.colorAccent)));

            // Create the fragment
            var fragment = new RootPreferencesFragment();
            var scrollToKey = this.Intent?.GetStringExtra(PreferencesActivity.ScrollToPrefKeyKey);
            if (scrollToKey != null)
            {
                // Tell fragment to scroll to a preference.
                // This can be because PreferencesFragment has recreated the activity and wants to scroll to a preference that it stashed in the intent.
                var args = new Bundle();
                args.PutString(PreferencesFragmentBase.ScrollToPrefKeyKey, scrollToKey);
                fragment.Arguments = args;
            }
            var transaction = this.SupportFragmentManager!.BeginTransaction()!;  // Both @NonNull in AndroidX
            transaction.Replace(Resource.Id.preferences_activity_fragment_container, fragment);
            transaction.Commit();

            // Set-up stack-aware swipe to dismiss.
            this.RequireViewById<SwipeDismissFrameLayout>(Resource.Id.prefSwipeFrame).AddCallback(new SwipeDismissCallback(this));
            this.SupportFragmentManager.BackStackChangeCommitted += (o, e) =>
            {
                // Un-hide the prefs container after a back navigation is completed.
                this.RequireViewById<View>(Resource.Id.prefsContainer).Visibility = ViewStates.Visible;
            };
        }


        private class SwipeDismissCallback : SwipeDismissFrameLayout.Callback
        {
            public SwipeDismissCallback(PreferencesActivity activity)
            {
                this._activity = activity;
            }

            private readonly PreferencesActivity _activity;

            /// <inheritdoc/>
            public override void OnDismissed(SwipeDismissFrameLayout? layout)
            {
                // Hide the prefs container to give a nicer animation. Otherwise the current contains appear momentarily.
                _activity.RequireViewById<View>(Resource.Id.prefsContainer).Visibility = ViewStates.Invisible;

                // Start back navigation. The dispatcher pops the fragment back stack, or finishes the activity if it's empty.
                _activity.OnBackPressedDispatcher.OnBackPressed();
            }
        }


        /// <summary>
        /// Set the title in the header.
        /// </summary>
        /// <param name="title">Title</param>
        public void SetPrefsTitle(string title)
        {
            var titleTxt = this.RequireViewById<TextView>(Resource.Id.prefHeaderTxt);
            titleTxt.Text = title;
        }


        bool PreferenceFragmentCompat.IOnPreferenceStartFragmentCallback.OnPreferenceStartFragment(
            PreferenceFragmentCompat caller,
            Preference pref)
        {
            // Instantiate the new Fragment.
            // The following should work, but doesn't:
            //     var fragment = SupportFragmentManager.FragmentFactory.Instantiate(this.ClassLoader, pref.Fragment);
            // The reason for this isn't clear - it might be because the Preference's app:fragment attribute value is incorrect.
            // As a workaround we switch on the android:Fragment attribute value to determine the fragment type.
            PreferenceFragmentCompat fragment;
            if (pref.Fragment?.EndsWith("AdvancedPreferencesFragment") == true)
            {
                fragment = new AdvancedPreferencesFragment();
            }
            else
            {
                throw new InvalidOperationException($"Unexpected fragment class name in OnPreferenceStartFragment(): {pref.Fragment}");
            }
            fragment.Arguments = pref.Extras;
            // androidx.preference finds a custom dialog's preference through its target fragment, so this deprecated call is still required.
#pragma warning disable CS0618
            fragment.SetTargetFragment(caller, 0);
#pragma warning restore CS0618

            // Replace the existing Fragment with the new Fragment.
            var transaction = SupportFragmentManager!.BeginTransaction()!;  // Both @NonNull in AndroidX
            transaction.Replace(Resource.Id.preferences_activity_fragment_container, fragment);
            transaction.AddToBackStack(null);
            transaction.Commit();

            return true;
        }
    }
}
