using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Threading.Tasks;
using System.Globalization;
using Task = System.Threading.Tasks.Task;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Util;
using Android.Content.PM;

using AndroidX.AppCompat.App;
using AndroidX.Core.Content;
using AndroidX.DocumentFile.Provider;
using AlertDialog = AndroidX.AppCompat.App.AlertDialog;

using Android.Gms.Tasks;
using Android.Gms.Wearable;

using Romer.DataLib;
using Romer.CommonLib;



namespace Romer.Mobile.App
{
    /// <summary>
    /// Shares a route, or some of its waypoints: to a watch, to another app, or as a GPX file saved on the phone.
    /// A single waypoint can only be shared to another app, as a location.
    /// </summary>
    [Activity(Label = "@string/share", Theme = "@style/AppTheme.Dialog", ScreenOrientation = ScreenOrientation.Portrait)]
    public class ShareActivity : AppCompatActivity
    {
        /// <summary>
        /// Intent extra: ID of the route to share (int). Required.
        /// </summary>
        public const string RouteIdKey = "RouteIdKey";

        /// <summary>
        /// Intent extra: IDs of the waypoints to share (int[]). Optional. The default is all the route's waypoints.
        /// </summary>
        public const string WaypointIdsKey = "WaypointIdsKey";

        /// <summary>
        /// Intent extra: true to share the waypoints as a route, false to share a single waypoint as a location (bool).
        /// Optional. The default is true if there's more than one waypoint.
        /// </summary>
        public const string ShareAsRouteKey = "ShareAsRouteKey";

        private const string SaveFileNameKey = "SaveFileNameKey";

        // Activity result launchers, registered in OnCreate()
        private AndroidX.Activity.Result.ActivityResultLauncher _selectDeviceLauncher = null!;
        private AndroidX.Activity.Result.ActivityResultLauncher _pickFolderLauncher = null!;


        private int _routeId;
        private int[] _waypointIds = Array.Empty<int>();
        private bool _shareAsRoute;


        /// <inheritdoc/>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            RegisterActivityResultLaunchers();
            SetContentView(Resource.Layout.share_activity);
            SetTitle(Resource.String.share);

            var mgr = RouteManagerFactory.Create(this);

            var savedState = savedInstanceState ?? this.Intent?.Extras;
            if (savedState == null)
                throw new InvalidOperationException("No route specified");
            _routeId = savedState.GetInt(RouteIdKey);
            var route = mgr.GetRoute(_routeId);
            if (route == null)
            {
                // Route has been deleted
                Finish();
                return;
            }
            _waypointIds = savedState.GetIntArray(WaypointIdsKey) ?? mgr.GetWaypointIds(_routeId);
            _shareAsRoute = savedState.GetBoolean(ShareAsRouteKey, _waypointIds.Length > 1);
            _saveFileName = savedState.GetString(SaveFileNameKey);


            this.RequireViewById<View>(Resource.Id.shareToWatch).Click += async (o, e) => { await ShareToWatchBtn_Click(o, e); };
            this.RequireViewById<View>(Resource.Id.shareToWatch).Enabled = _shareAsRoute;  // Can only share a route to the watch
            this.RequireViewById<View>(Resource.Id.shareToExternal).Click += ShareToExternalBtn_Click;
            this.RequireViewById<View>(Resource.Id.shareToPhone).Click += ShareToPhoneBtn_Click;
            this.RequireViewById<View>(Resource.Id.shareToPhone).Enabled = _shareAsRoute;  // Can only share a route as gpx

            // Build description of what is being shared
            string descStr;
            if (_shareAsRoute)
            {
                if (_waypointIds.Length == route.Waypoints.Count())
                {
                    descStr = this.GetFormattedString(Resource.String.shareDescRoute, route.Label);
                }
                else
                {
                    descStr = this.GetFormattedString(Resource.String.shareDescRouteWaypoints,
                                                      this.RequireResources().GetQuantityString(Resource.Plurals.waypointCount, _waypointIds.Length, _waypointIds.Length),
                                                      route.Label);
                }
            }
            else
            {
                descStr = this.GetFormattedString(Resource.String.shareDescWaypoints,
                                                  this.RequireResources().GetQuantityString(Resource.Plurals.waypointCount, _waypointIds.Length, _waypointIds.Length));
            }
            RequireViewById<TextView>(Resource.Id.description).Text = descStr;
        }


        /// <inheritdoc/>
        protected override void OnSaveInstanceState(Bundle saveState)
        {
            saveState.PutInt(RouteIdKey, _routeId);
            saveState.PutIntArray(WaypointIdsKey, _waypointIds);
            saveState.PutBoolean(ShareAsRouteKey, _shareAsRoute);
            saveState.PutString(SaveFileNameKey, _saveFileName);

            base.OnSaveInstanceState(saveState);
        }


        /// <summary>
        /// Get the route containing only the waypoints being shared, in route order.
        /// </summary>
        /// <returns>Route to share</returns>
        private Route GetRouteToShare()
        {
            var route = RouteManagerFactory.Create(this).GetRoute(this._routeId)
                ?? throw new InvalidOperationException($"Route {this._routeId} no longer exists");
            route.Waypoints = route.Waypoints.Where(wp => this._waypointIds.Contains(wp.Id)).ToList();
            return route;
        }


        /// <summary>
        /// Make a string safe to use as a file name by replacing invalid characters.
        /// </summary>
        /// <param name="name">Proposed file name</param>
        /// <returns>Safe file name</returns>
        private static string ToSafeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }).ToHashSet();
            var safe = new string(name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray()).Trim();
            return string.IsNullOrEmpty(safe) ? "route" : safe;
        }


        /// <summary>
        /// Register the callbacks for the activities that this activity launches.
        /// Called from OnCreate(), so that results delivered after the activity is recreated reach the callbacks.
        /// </summary>
        private void RegisterActivityResultLaunchers()
        {
            // Choosing a watch to send the route to
            _selectDeviceLauncher = this.RegisterForActivityResult(result =>
            {
                var device = DeviceListActivity.ReadResult(result);
                if (device != null)
                    _ = SendToDeviceAndFinishAsync(device.Value.NodeId, device.Value.DisplayName);
            });

            // Choosing a folder to save the GPX file to
            _pickFolderLauncher = this.RegisterForResult<Android.Net.Uri>(new AndroidX.Activity.Result.Contract.ActivityResultContracts.OpenDocumentTree(), folderUri =>
            {
                if (folderUri != null && this._saveFileName != null &&
                    !SaveToPhone(GetRouteToShare(), folderUri, this._saveFileName))
                {
                    return;  // Stay open, so that the error message stays on screen
                }
                SetResult(Result.Ok);
                Finish();
            });
        }



        #region Share to watch

        private async Task ShareToWatchBtn_Click(object? sender, EventArgs e)
        {
            try
            {
                using (var capabilityClient = WearableClass.GetCapabilityClient(Android.App.Application.Context))
                {
                    var capabilityInfo = await capabilityClient.GetCapabilityAsync(WearMessage.RouteManagerCapability, CapabilityClient.FilterReachable);
                    var nodes = capabilityInfo?.Nodes;

                    if (nodes?.Count == 1 && nodes.ElementAt(0) != null)
                    {
                        // Single node.
                        await SendToDeviceAndFinishAsync(nodes.ElementAt(0).Id, nodes.ElementAt(0).DisplayName);
                        return;
                    }
                    else if (nodes?.Count > 1)
                    {
                        // Multiple nodes. Let the user choose one. The launcher's callback sends the route.
                        _selectDeviceLauncher.Launch(DeviceListActivity.CreateIntent(this, nodes));
                        return;
                    }
                    else
                    {
                        // No paired device
                        var adb = new AlertDialog.Builder(this, Resource.Style.AppTheme_Dialog_Alert);
                        adb.SetTitle(Resource.String.error);
                        adb.SetMessage(Resource.String.deviceOrAppNotFoundErrorMsg);
                        adb.SetPositiveButton(Android.Resource.String.Ok, (IDialogInterfaceOnClickListener?)null);
                        adb.Show();
                        return;
                    }
                }
            }
            catch (Exception)
            {
                var adb = new AlertDialog.Builder(this, Resource.Style.AppTheme_Dialog_Alert);
                adb.SetTitle(Resource.String.error);
                adb.SetMessage(Resource.String.deviceOrAppCommsErrorMsg);
                adb.SetPositiveButton(Android.Resource.String.Ok, (IDialogInterfaceOnClickListener?)null);
                adb.Show();
                return;
            }

        }


        /// <summary>
        /// Send the route to a wearable device and, if successful, finish the activity.
        /// Must be called on the UI thread.
        /// </summary>
        private async Task SendToDeviceAndFinishAsync(
            string nodeId,
            string nodeDisplayName)
        {
            var message = Encoding.UTF8.GetBytes(GetRouteToShare().ToJson());

            try
            {
                await ThisApp.Instance().MessageClient.SendMessageAsync(nodeId, WearMessage.Put + WearMessage.Data_Route, message);
                var msg = this.GetFormattedString(Resource.String.routeSentToDeviceMsg, nodeDisplayName);
                Toast.MakeText(this, msg, ToastLength.Short)?.Show();
                SetResult(Result.Ok);
                Finish();
            }
            catch (Exception)
            {
                var adb = new AlertDialog.Builder(this, Resource.Style.AppTheme_Dialog_Alert);
                adb.SetTitle(Resource.String.error);
                adb.SetMessage(Resource.String.deviceOrAppCommsErrorMsg);
                adb.SetPositiveButton(Android.Resource.String.Ok, (IDialogInterfaceOnClickListener?)null);
                adb.Show();
            }
        }

        #endregion Share to watch



        #region Share to external app

        private void ShareToExternalBtn_Click(object? sender, EventArgs e)
        {
            if (this._shareAsRoute)
            {
                // Share whole route or selected waypoints from route.
                ShareRouteToExternal(this, GetRouteToShare());
            }
            else
            {
                // Share single waypoint
                ShareWaypointToExternal(this, this._routeId, this._waypointIds[0]);
            }

            this.SetResult(Result.Ok);
            this.Finish();
        }


        private static void ShareRouteToExternal(
            Context context,
            Route route)
        {
            // Share a route
            var fileName = ToSafeFileName(route.Label) + ".gpx";
            var tmpFilePath = Path.Join(Path.GetTempPath(), fileName);  // created in cache
            using (var tmpFileStm = File.Open(tmpFilePath, FileMode.Create))
            {
                RouteConverter.ToGpx(context, route, tmpFileStm);
            }
            // The display name is the file name that the receiving app sees. Include the extension: many apps rely on
            // it to recognise GPX files.
            var uri = FileProvider.GetUriForFile(context,
                                                 "Romer.Mobile.App.fileprovider", // See <provider> section in AndroidManifest.xml
                                                 new Java.IO.File(tmpFilePath),
                                                 fileName);

            // Create a share intent
            var shareIntent = new Intent(Intent.ActionSend);
            shareIntent.SetType(AppSettings.GpxMimeType);
            shareIntent.PutExtra(Intent.ExtraStream, uri);

            // Start the activity to show the share dialog
            context.StartActivity(Intent.CreateChooser(shareIntent, context.GetString(Resource.String.shareChooserDlgTitleGpx)));
        }


        private static void ShareWaypointToExternal(
            Context context,
            int routeId,
            int waypointId)
        {
            var mgr = RouteManagerFactory.Create(context);
            var waypoint = mgr.GetWaypoint(routeId, waypointId);
            if (waypoint == null)
                return;  // Deleted

            // Build a "geo" URI.
            // Format with the invariant culture: the URI requires '.' decimal separators whatever the device's locale.
            var latLon = string.Create(CultureInfo.InvariantCulture, $"{waypoint.Latitude},{waypoint.Longitude}");
            var uri = Android.Net.Uri.Parse("geo:" + latLon);

            // Share.
            var shareIntent = new Intent(Intent.ActionView, uri);
            context.StartActivity(Intent.CreateChooser(shareIntent, context.GetString(Resource.String.shareChooserDlgTitleLatLon)));
        }

        #endregion Share to external app



        #region Save to phone

        private string? _saveFileName;  // Used to hold the filename across the transition to the directory picker activity

        private void ShareToPhoneBtn_Click(object? sender, EventArgs e)
        {
            var mgr = RouteManagerFactory.Create(this);
            var route = mgr.GetRoute(this._routeId);
            if (route == null)
                return;  // Deleted
            _saveFileName = ToSafeFileName(route.Label) + ".gpx";

            var adb = new AlertDialog.Builder(this, Resource.Style.AppTheme_Dialog_Alert);
            adb.SetTitle(Resource.String.saveAs);
            var dlgView = this.LayoutInflater.Inflate(Resource.Layout.dlg_filename, null)!;
            dlgView.RequireViewById<EditText>(Resource.Id.filenameTxt).Text = _saveFileName;
            adb.SetView(dlgView);
            adb.SetPositiveButton(Android.Resource.String.Ok, (o, e) =>
            {
                _saveFileName = ToSafeFileName(dlgView.RequireViewById<EditText>(Resource.Id.filenameTxt).Text ?? string.Empty);

                // Invoke the directory picker. The launcher's callback calls SaveToPhone().
                _pickFolderLauncher.Launch(null);  // No initial folder
            });
            adb.SetNegativeButton(Android.Resource.String.Cancel, (IDialogInterfaceOnClickListener?)null);
            adb.Show();
        }


        /// <summary>
        /// Save the route as a GPX file, and show a message saying whether it worked.
        /// </summary>
        /// <returns>true if the file was saved</returns>
        private bool SaveToPhone(
            Route route,
            Android.Net.Uri folderUri,
            string fileName)
        {
            try
            {
                // Save the file. The directory picker has already granted access to the folder.
                var dirDocFile = DocumentFile.FromTreeUri(this, folderUri)
                    ?? throw new IOException($"Can't access folder {folderUri}");
                var gpxDocFile = dirDocFile.CreateFile(AppSettings.GpxMimeType, fileName)
                    ?? throw new IOException($"Can't create {fileName} in {folderUri}");
                using (var stm = this.ContentResolver?.OpenOutputStream(gpxDocFile.Uri ?? throw new IOException($"No URI for {fileName}"))
                                 ?? throw new IOException($"Can't write to {gpxDocFile.Uri}"))
                {
                    RouteConverter.ToGpx(this, route, stm);
                }

                Toast.MakeText(this, Resource.String.routeSavedToFileMsg, ToastLength.Short)?.Show();
                return true;
            }
            catch (Exception ex)
            {
                Log.Warn("LOC", $"Saving {fileName} failed: {ex}");
                var adb = new AlertDialog.Builder(this, Resource.Style.AppTheme_Dialog_Alert);
                adb.SetTitle(Resource.String.error);
                adb.SetMessage(Resource.String.routeSaveErrorMsg);
                adb.SetPositiveButton(Android.Resource.String.Ok, (IDialogInterfaceOnClickListener?)null);
                adb.Show();
                return false;
            }
        }

        #endregion Save to phone
    }
}
