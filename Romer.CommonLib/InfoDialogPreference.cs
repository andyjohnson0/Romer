using Android.Content;
using Android.Runtime;
using Android.Util;

using AndroidX.Preference;

namespace Romer.CommonLib
{
    /// <summary>
    /// A preference that shows an informational dialog, such as "About" or "Credits".
    /// androidx.preference.DialogPreference is abstract, so preference XML can't use it directly; this concrete
    /// subclass adds no behaviour. The app's preference fragment provides the dialog.
    /// </summary>
    [Register("uk.andyjohnson.romer.preference.InfoDialogPreference")]
    public class InfoDialogPreference : DialogPreference
    {
        /// <inheritdoc/>
        public InfoDialogPreference(Context context, IAttributeSet? attrs)
            : base(context, attrs)
        {
        }

        /// <inheritdoc/>
        public InfoDialogPreference(Context context, IAttributeSet? attrs, int defStyleAttr)
            : base(context, attrs, defStyleAttr)
        {
        }

        /// <inheritdoc/>
        protected InfoDialogPreference(nint javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }
    }
}
