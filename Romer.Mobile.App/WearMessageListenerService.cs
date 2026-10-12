using System;
using System.IO;
using System.Text;

using Android.App;
using Android.OS;
using Android.Util;
using Android.Content;
using Android.Widget;

using AndroidX.Core.App;

using Android.Gms.Wearable;

using Romer.DataLib;
using Romer.CommonLib;



namespace Romer.Mobile.App
{
    /// <summary>
    /// Receives Wear OS Data Layer messages from watches, even when the app isn't running.
    /// Stores routes that a watch sends, and notifies the user.
    /// </summary>
    [Service(Name = "uk.andyjohnson.romer.mobile.app.WearMessageListenerService", Exported = true)]
    [IntentFilter(new[] { "com.google.android.gms.wearable.MESSAGE_RECEIVED" }, DataScheme = "wear", DataHost = "*")]
    public class WearMessageListenerService : WearableListenerService
    {
        /// <inheritdoc/>
        public override void OnCreate()
        {
            base.OnCreate();
            Log.Debug("LOC", "Wearable Listener Service created");
        }


        /// <inheritdoc/>
        public override void OnDestroy()
        {
            base.OnDestroy();
            Log.Debug("LOC", "Wearable Listener Service destroyed");
        }


        /// <inheritdoc/>
        public override void OnMessageReceived(IMessageEvent messageEvent)
        {
            base.OnMessageReceived(messageEvent);
            Log.Debug("LOC", "Message received from: " + messageEvent.SourceNodeId);

            try
            {
                if (messageEvent.Path == WearMessage.Put + WearMessage.Data_Route)
                {
                    // Wearable device wants to add a route
                    var route = WearMessage.ReadRoute(messageEvent);
                    foreach (var wp in route.Waypoints)
                    {
                        wp.Ticked = false;
                    }
                    AddRoute(route);
                }
                else if (messageEvent.Path == WearMessage.Config_Diagnostics)
                {
                    // A watch's reply to a diagnostics request. DiagnosticDialogFragment's own listener handles it.
                }
                else
                {
                    Log.Warn("LOC", $"Ignoring message with unknown path {messageEvent.Path}");
                }
            }
            catch (Exception ex)
            {
                Log.Error("LOC", $"Failed to handle message {messageEvent.Path}: {ex}");
            }
        }


        /// <summary>
        /// Fires after a route received from a watch has been stored. May fire on a background thread.
        /// </summary>
        public static event EventHandler? RouteReceived;


        private void AddRoute(Route route)
        {
            var routesMgr = RouteManagerFactory.Create(this);
            var routeId = routesMgr.CreateRoute(route);
            RouteReceived?.Invoke(this, EventArgs.Empty);

            // Display a notification that opens the new route. Android doesn't allow a service to start an activity
            // while the app is in the background, so this replaces navigating to the main activity directly.
            const int notificationNewRouteId = 1;
            var intent = new Intent(this, typeof(RouteActivity));
            intent.PutExtra(RouteActivity.RouteIdKey, routeId);
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);
            var pendingIntent = PendingIntent.GetActivity(this, routeId, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            var nb = new NotificationCompat.Builder(this, RomerApplication.NotificationChannelId);
            nb.SetSmallIcon(Resource.Mipmap.ic_launcher);
            nb.SetContentTitle(this.GetFormattedString(Resource.String.newRouteAddedMsg, route.Label));
            nb.SetAutoCancel(true);
            nb.SetContentIntent(pendingIntent);
            var notificationMgr = NotificationManagerCompat.From(this);
            if (notificationMgr?.AreNotificationsEnabled() == true)
            {
                notificationMgr.Notify(notificationNewRouteId, nb.Build());
            }

            // Show feedback if the app is in the foreground. Messages arrive on a background thread, and a toast must be
            // shown from the main thread.
            var msg = this.GetFormattedString(Resource.String.newRouteAddedMsg, route.Label);
            var appContext = this.ApplicationContext!;
            new Handler(Looper.MainLooper!).Post(() => Toast.MakeText(appContext, msg, ToastLength.Short)?.Show());
        }
    }
}
