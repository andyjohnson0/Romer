
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Android.App;
using Android.Util;
using Android.Content;
using Android.OS;
using Android.Widget;

using Android.Gms.Wearable;

using Romer.DataLib;
using Romer.CommonLib;
using AndroidX.Core.App;



namespace Romer.Wearable.App
{
    /// <summary>
    /// Receives Wear OS Data Layer messages from the phone, even when the app isn't running.
    /// Replaces the watch's route with one that the phone sends, and replies to requests for diagnostic information.
    /// </summary>
    [Service(Name = "uk.andyjohnson.romer.wearable.app.WearMessageListenerService", Exported = true)]
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

            // Don't let exceptions escape: they would crash the app.
            try
            {
                if (messageEvent.Path == WearMessage.Put + WearMessage.Data_Route)
                {
                    HandleAddRoute(WearMessage.ReadRoute(messageEvent));
                }
                else if (messageEvent.Path == WearMessage.Get + WearMessage.Config_Diagnostics)
                {
                    _ = ReplyWithDiagnosticsAsync(messageEvent.SourceNodeId!);
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



        private void HandleAddRoute(Route route)
        {
            // Mobile device wants to add a route

            // Replace the watch's single route with the inbound route
            var routesMgr = RouteManagerFactory.Create(Application.Context);
            routesMgr.GetOrCreateRoute(AppSettings.RouteId, overwrite: true, label: route.Label);
            route.Id = AppSettings.RouteId;
            routesMgr.UpdateRoute(route);

            // Display a notification that links through to the main activity.
            const int notificationNewRouteId = 1;
            var intent = new Intent(Application.Context, typeof(MainActivity));
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);
            var pendingIntent = PendingIntent.GetActivity(Application.Context, 0, intent, PendingIntentFlags.CancelCurrent | PendingIntentFlags.Immutable);
            var nb = new NotificationCompat.Builder(Application.Context, RomerApplication.NotificationChannelId);
            nb.SetSmallIcon(Resource.Mipmap.ic_launcher);
            nb.SetContentTitle(Application.Context.GetFormattedString(Resource.String.routeReceivedContentMsg, route.Label));
            nb.SetAutoCancel(true);
            nb.SetContentIntent(pendingIntent);
            var notificationMgr = NotificationManagerCompat.From(Application.Context);
            if (notificationMgr?.AreNotificationsEnabled() == true)
            {
                notificationMgr.Notify(notificationNewRouteId, nb.Build());
            }
        }


        /// <summary>
        /// Send this device's diagnostic information to the phone that asked for it.
        /// </summary>
        /// <param name="sourceNodeId">ID of the requesting node</param>
        private async Task ReplyWithDiagnosticsAsync(string sourceNodeId)
        {
            try
            {
                var json = DiagnosticInfo.CreateFrom(this).ToJson(humanReadable: true);
                await ThisApp.Instance().MessageClient.SendMessageAsync(sourceNodeId, WearMessage.Config_Diagnostics, Encoding.UTF8.GetBytes(json));
            }
            catch (Exception ex)
            {
                Log.Error("LOC", $"Failed to send diagnostics to {sourceNodeId}: {ex}");
            }
        }
    }
}
