using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.Content;
using Android.OS;
using Android.Views;

using Romer.CommonLib;
using AndroidX.Preference;

namespace Romer.Wearable.App.Preferences
{
    /// <summary>
    /// Custom dialog fragment for list preferences.
    /// Has a centred title and no OK button, unlike the default AndroidX dialog. Choosing an item closes it.
    /// </summary>
    public class ListPreferenceDialogFragment : PreferenceDialogFragmentCompat
    {
        /// <summary>
        /// Create a dialog fragment for a list preference.
        /// Call SetTargetFragment() with the preference's fragment before showing it, so that it can find the preference.
        /// </summary>
        /// <param name="key">Preference key</param>
        /// <returns>Dialog fragment</returns>
        public static ListPreferenceDialogFragment NewInstance(string key)
        {
            var fragment = new ListPreferenceDialogFragment();
            var b = new Bundle(1);
            b.PutString(ArgKey, key);
            fragment.Arguments = b;
            return fragment;
        }


        private string[] _dialogEntries = Array.Empty<string>();
        private string[] _dialogEntryValues = Array.Empty<string>();
        private int _dialogClickedEntryIdx;


        /// <inheritdoc/>
        protected override void OnPrepareDialogBuilder(AndroidX.AppCompat.App.AlertDialog.Builder builder)
        {
            base.OnPrepareDialogBuilder(builder);

            var context = RequireContext()!;
            var userSettings = UserSettings.Load(context);

            // Customise the dialog with a centred title
            var titleTv = new TextView(context);
            titleTv.Text = this.Preference?.Title;
            titleTv.SetPadding(0, (int)context.RequireResources().GetDimension(Resource.Dimension.dialogHeaderTopPadding),
                               0, (int)context.RequireResources().GetDimension(Resource.Dimension.dialogHeaderBottomPadding));
            titleTv.Gravity = GravityFlags.CenterHorizontal;
            titleTv.SetTextColor(userSettings.Navigation_NightColoursMode
                ? new Android.Graphics.Color(AndroidX.Core.Content.ContextCompat.GetColor(context, Resource.Color.nightColorAccent))
                : new Android.Graphics.Color(AndroidX.Core.Content.ContextCompat.GetColor(context, Resource.Color.colorAccent)));
            titleTv.SetTextSize(Android.Util.ComplexUnitType.Px, context.RequireResources().GetDimensionPixelSize(Resource.Dimension.dialogHeaderTextSize));
            titleTv.SetTypeface(null, Android.Graphics.TypefaceStyle.Bold);
            builder.SetCustomTitle(titleTv);

            // Hide the OK button
            builder.SetPositiveButton("", (IDialogInterfaceOnClickListener?)null);

            // Get the entries and entry values.
            var lp = (ListPreference)this.Preference!;
            _dialogClickedEntryIdx = lp.FindIndexOfValue(lp.Value);
            _dialogEntries = lp.GetEntries() ?? Array.Empty<string>();
            _dialogEntryValues = lp.GetEntryValues() ?? Array.Empty<string>();

            // Set the choice items and wire-upo a handler that closes the dialog like AndroidX would have done.
            builder.SetSingleChoiceItems(_dialogEntries, _dialogClickedEntryIdx, handler: (o, ea) =>
            {
                var dlg = (Dialog)o!;
                _dialogClickedEntryIdx = ea.Which;

                // Clicking on an item simulates the positive button click, and dismisses the dialog.
                OnClick(dlg, (int)DialogButtonType.Positive);
                dlg.Dismiss();
            });
        }



        /// <inheritdoc/>
        public override void OnDialogClosed(bool positiveResult)
        {
            // Handle the dialog closing event
            var preference = this.Preference as ListPreference;
            if (positiveResult && _dialogClickedEntryIdx >= 0 && preference != null)
            {
                var value = _dialogEntryValues[_dialogClickedEntryIdx].ToString();
                if (preference.CallChangeListener(value))
                {
                    preference.Value = value;
                }
            }
        }
    }
}
