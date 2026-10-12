using Android.Content;

using AndroidX.Wear.Activity;
using AndroidX.Wear.Remote.Interactions;

using Romer.CommonLib;


namespace Romer.Wearable.App.Preferences
{
    /// <summary>
    /// About and Credits dialog for the watch.
    /// Links open on the paired phone, because a watch has no practical browser.
    /// </summary>
    public class WearInfoDialogFragment : InfoDialogFragment
    {
        /// <inheritdoc/>
        protected override void OpenExternalUrl(string url)
        {
            try
            {
                // Open in a browser on the paired phone.
                var context = RequireContext()!;
                var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(url));
                intent.AddCategory(Intent.CategoryBrowsable);
                var remoteActivityHelper = new RemoteActivityHelper(context, Java.Util.Concurrent.Executors.NewSingleThreadExecutor()!);
                remoteActivityHelper.StartRemoteActivity(intent);

                // Show the "continue on phone" animation.
                intent = new Intent(context, Java.Lang.Class.FromType(typeof(ConfirmationActivity)));
                intent.PutExtra(ConfirmationActivity.ExtraAnimationType, ConfirmationActivity.OpenOnPhoneAnimation);
                intent.PutExtra(ConfirmationActivity.ExtraMessage, context.GetString(Resource.String.continueOnPhoneMsg));
                intent.PutExtra(ConfirmationActivity.ExtraAnimationDurationMillis, 2000);
                StartActivity(intent);
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("LOC", $"Couldn't open {url} on the phone: {ex}");
            }
        }
    }
}
