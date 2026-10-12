using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.OS;
using Android.Views;

using AndroidX.AppCompat.App;

using Romer.CommonLib;



namespace Romer.Mobile.App.Preference
{
    /// <summary>
    /// The settings screen, which hosts <see cref="PreferenceFragment"/>.
    /// </summary>
    [Activity(Label="@string/settings")]
    public class PreferenceActivity : AppCompatActivity
    {
        /// <inheritdoc/>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetTheme(Resource.Style.AppTheme_Settings);
            SetContentView(Resource.Layout.settings_activity);
            EdgeToEdge.ApplySystemBarInsets(RequireViewById<View>(Resource.Id.settings_activity_settings_fragment_container));

            // This screen has no app bar, so the status bar is drawn over the window background. Use dark status bar
            // icons on the light background.
            var uiMode = this.RequireResources().Configuration?.UiMode ?? Android.Content.Res.UiMode.NightNo;
            var isNightMode = (uiMode & Android.Content.Res.UiMode.NightMask) == Android.Content.Res.UiMode.NightYes;
            // Only on Android 15+, where edge-to-edge is enforced. Earlier versions draw the theme's dark status bar,
            // which needs the default light icons.
            var insetsController = Window != null ? AndroidX.Core.View.WindowCompat.GetInsetsController(Window, Window.DecorView) : null;
            if (insetsController != null && OperatingSystem.IsAndroidVersionAtLeast(35))
            {
                insetsController.AppearanceLightStatusBars = !isNightMode;
            }

            var transaction = this.SupportFragmentManager!.BeginTransaction()!;  // Both @NonNull in AndroidX
            transaction.Replace(Resource.Id.settings_activity_settings_fragment_container, new PreferenceFragment());
            transaction.Commit();
        }
    }
}
