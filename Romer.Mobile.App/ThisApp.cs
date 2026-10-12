using System;

using Android.App;
using Android.Runtime;

using Romer.CommonLib;


namespace Romer.Mobile.App
{
    /// <summary>
    /// The phone app's application class.
    /// </summary>
    [Application]
    public class ThisApp : RomerApplication
    {
        /// <inheritdoc/>
        public ThisApp(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }
    }
}
