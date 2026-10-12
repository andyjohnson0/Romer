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


namespace Romer.Wearable.App
{
    /// <summary>
    /// Fixed app settings, which the user can't change. See <see cref="UserSettings"/> for those they can.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// ID of the single route that the watch app stores.
        /// </summary>
        public const int RouteId = 0;
    }
}
