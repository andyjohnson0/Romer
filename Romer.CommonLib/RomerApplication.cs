using Android.App;
using Android.Runtime;

using AndroidX.Core.App;

using Android.Gms.Wearable;


namespace Romer.CommonLib
{
    /// <summary>
    /// Application base class for the mobile and wearable apps.
    /// Each app's [Application] class derives from this.
    /// </summary>
    public abstract class RomerApplication : Application
    {
        /// <summary>
        /// ID of the notification channel that both apps use.
        /// </summary>
        public const string NotificationChannelId = "NotificationChannelId";


        /// <inheritdoc/>
        protected RomerApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
            // .NET for Android requires this constructor on Application subclasses.
        }


        /// <summary>
        /// Wear OS Data Layer message client.
        /// </summary>
        public MessageClient MessageClient { get; private set; } = null!;  // Set in OnCreate(), which runs before any other app code


        /// <inheritdoc/>
        /// <remarks>Creates <see cref="MessageClient"/> and the notification channel.</remarks>
        public override void OnCreate()
        {
            base.OnCreate();

            this.MessageClient = WearableClass.GetMessageClient(this);

            // Create the notification channel here rather than in an activity, because WearMessageListenerService can
            // post a notification before any activity has run. Creating an existing channel does nothing.
            var channelName = ApplicationInfo?.LoadLabel(PackageManager!) ?? "Romer";
            var channel = new NotificationChannel(NotificationChannelId, channelName, NotificationImportance.Default);
            NotificationManagerCompat.From(this)!.CreateNotificationChannel(channel);
        }


        /// <summary>
        /// Get the running application.
        /// </summary>
        /// <returns>The application</returns>
        public static RomerApplication Instance()
        {
            return (RomerApplication)Application.Context;
        }
    }
}
