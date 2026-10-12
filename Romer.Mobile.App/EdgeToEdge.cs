using Android.Views;

using AndroidX.Core.View;

namespace Romer.Mobile.App
{
    /// <summary>
    /// Window inset handling for edge-to-edge layouts, which Android 15 (API 35) and later enforce for apps
    /// targeting those versions.
    /// </summary>
    public static class EdgeToEdge
    {
        /// <summary>
        /// Keep content clear of the system bars.
        /// If there is an app bar then it is padded at the top so that its background extends behind the status bar;
        /// otherwise the root view is padded at the top. The root view is padded at the sides and bottom.
        /// </summary>
        /// <param name="root">Activity root view</param>
        /// <param name="appBar">App bar at the top of the root view, or null if there isn't one</param>
        public static void ApplySystemBarInsets(View root, View? appBar = null)
        {
            ViewCompat.SetOnApplyWindowInsetsListener(root, new InsetsListener(appBar));
        }


        private sealed class InsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
        {
            public InsetsListener(View? appBar)
            {
                this._appBar = appBar;
            }

            private readonly View? _appBar;

            public WindowInsetsCompat OnApplyWindowInsets(View? v, WindowInsetsCompat? insets)
            {
                if (v == null || insets == null)
                    return insets ?? WindowInsetsCompat.Consumed!;

                var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout());
                if (bars == null)
                    return insets;
                var bar = this._appBar;
                bar?.SetPadding(bar.PaddingLeft, bars.Top, bar.PaddingRight, bar.PaddingBottom);
                v.SetPadding(bars.Left, bar == null ? bars.Top : 0, bars.Right, bars.Bottom);
                return WindowInsetsCompat.Consumed!;  // Documented as non-null
            }
        }
    }
}
