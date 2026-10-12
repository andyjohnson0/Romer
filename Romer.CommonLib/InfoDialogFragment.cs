using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;


namespace Romer.CommonLib
{
    /// <summary>
    /// Kinds of informational dialog shown by <see cref="InfoDialogFragment"/>.
    /// </summary>
    public enum InfoDialogKind
    {
        /// <summary>App name, version and copyright, with a link to the vendor's website</summary>
        About = 0,
        /// <summary>Third-party credits, from the app's credits.md asset</summary>
        Credits = 1
    }


    /// <summary>
    /// Dialog for the About and Credits entries in the apps' settings (<see cref="InfoDialogPreference"/>).
    /// Each app supplies its own layout. About layouts must contain views with the IDs icon, subTitle, info and link;
    /// Credits layouts must contain title and info.
    /// </summary>
    public class InfoDialogFragment : AndroidX.Fragment.App.DialogFragment
    {
        private const string KindKey = "InfoDialogKind";
        private const string LayoutKey = "InfoDialogLayout";
        private const string TitleKey = "InfoDialogTitle";


        /// <summary>
        /// Create a dialog fragment.
        /// </summary>
        /// <typeparam name="T">InfoDialogFragment or a subclass</typeparam>
        /// <param name="kind">Kind of dialog</param>
        /// <param name="layoutResId">The app's layout for the dialog</param>
        /// <param name="title">Dialog title, used by Credits dialogs</param>
        /// <returns>Dialog fragment, ready to show</returns>
        public static T NewInstance<T>(InfoDialogKind kind, int layoutResId, string? title)
            where T : InfoDialogFragment, new()
        {
            var args = new Bundle();
            args.PutInt(KindKey, (int)kind);
            args.PutInt(LayoutKey, layoutResId);
            args.PutString(TitleKey, title);
            return new T() { Arguments = args };
        }


        /// <inheritdoc/>
        public override View OnCreateView(LayoutInflater? inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            return inflater!.Inflate(RequireArguments()!.GetInt(LayoutKey), container, false)!;
        }


        /// <inheritdoc/>
        public override void OnViewCreated(View? view, Bundle? savedInstanceState)
        {
            base.OnViewCreated(view!, savedInstanceState);
            if (view == null)
                return;

            var context = RequireContext()!;
            var args = RequireArguments()!;
            switch ((InfoDialogKind)args.GetInt(KindKey))
            {
                case InfoDialogKind.About:
                    var vendorUrl = context.GetString(Resource.String.vendorUrl);
                    view.RequireViewById<View>(Resource.Id.icon).Click += (o, e) => OpenExternalUrl(vendorUrl);
                    view.RequireViewById<View>(Resource.Id.link).Click += (o, e) => OpenExternalUrl(vendorUrl);
                    view.RequireViewById<TextView>(Resource.Id.subTitle).Text =
                        context.GetFormattedString(Resource.String.appVersion, context.GetAppVersionName());
                    view.RequireViewById<TextView>(Resource.Id.info).Text =
                        context.GetFormattedString(Resource.String.appCopyright, context.GetString(Resource.String.vendorName));
                    break;

                case InfoDialogKind.Credits:
                    view.RequireViewById<TextView>(Resource.Id.title).Text = args.GetString(TitleKey);
                    using (var stm = context.Assets!.Open("credits.md"))
                    {
                        view.RequireViewById<TextView>(Resource.Id.info).SetText(Markdown.ToSpannableString(stm), TextView.BufferType.Spannable);
                    }
                    break;
            }
        }


        /// <summary>
        /// Open a web page. The default opens it in a browser on this device.
        /// </summary>
        /// <param name="url">URL to open</param>
        protected virtual void OpenExternalUrl(string url)
        {
            try
            {
                var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(url));
                intent.AddCategory(Intent.CategoryBrowsable);
                StartActivity(intent);
            }
            catch (ActivityNotFoundException)
            {
                // No browser. Nothing useful to do.
            }
        }
    }
}
