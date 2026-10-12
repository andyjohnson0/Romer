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


namespace Romer.Mobile.App
{
    /// <summary>
    /// Fixed app settings, which the user can't change. See <see cref="UserSettings"/> for those they can.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Mime type for GPX data.
        /// </summary>
        public const string GpxMimeType = "application/gpx+xml";

        /// <summary>
        /// Mime type that apps use for data of unknown type, including GPX files when they don't recognise them.
        /// </summary>
        public const string UnknownMimeType = "application/octet-stream";
    }
}
