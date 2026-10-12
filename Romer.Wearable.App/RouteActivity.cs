using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Runtime;
using Location = Android.Locations.Location;
using Android.Content.Res;
using Android.Gms.Tasks;
using Android.Gms.Wearable;

using AndroidX.AppCompat.App;
using AndroidX.RecyclerView.Widget;
using AndroidX.AppCompat.Widget;

using Kilsten.WearOS.Controls;

using Romer.GeoLib;
using Romer.DataLib;
using Kilsten.WearOS;
using Romer.CommonLib;



namespace Romer.Wearable.App
{
    /// <summary>
    /// The route menu, headed with the route's name: add a waypoint at the current location, list the waypoints,
    /// delete the last waypoint, send the route to the phone, and start a new route.
    /// </summary>
    [Activity]  // Note: title is provided by recycler view
    public class RouteActivity : AndroidX.Activity.ComponentActivity
    {
        /// <summary>
        /// Intent extra: the current location (<see cref="Location"/>), used to add a waypoint. Optional. Without it,
        /// waypoints can't be added.
        /// </summary>
        public const string CurrentLocationKey = "CurrentLocation";

        private Location? _currentLocation;

        // Activity result launchers, registered in OnCreate()
        private AndroidX.Activity.Result.ActivityResultLauncher _selectDeviceLauncher = null!;


        /// <inheritdoc/>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            // Load the night or day colours theme
            var userSettings = UserSettings.Load(this);
            SetTheme(userSettings.Navigation_NightColoursMode ? Resource.Style.Theme_App_NightColours : Resource.Style.Theme_App);

            //
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.route_activity);

            // Choosing a device to send the route to
            _selectDeviceLauncher = this.RegisterForActivityResult(result =>
            {
                var device = DeviceListActivity.ReadResult(result);
                if (device != null)
                    _ = sendToDeviceAndFinish(device.Value.NodeId, device.Value.DisplayName);
            });

            //
            var headingTxt = RequireViewById<TextView>(Resource.Id.headerTxt);
            headingTxt.SetTextColor(userSettings.Navigation_NightColoursMode
                ? new Android.Graphics.Color(AndroidX.Core.Content.ContextCompat.GetColor(this, Resource.Color.nightColorAccent))
                : new Android.Graphics.Color(AndroidX.Core.Content.ContextCompat.GetColor(this, Resource.Color.colorAccent)));

            // Restore state
            var savedState = savedInstanceState ?? this.Intent?.Extras;
            if (savedState != null)
            {
                _currentLocation = AndroidX.Core.OS.BundleCompat.GetParcelable(savedState, CurrentLocationKey, Java.Lang.Class.FromType(typeof(Android.Locations.Location))) as Android.Locations.Location;
            }

            //
            var rv = RequireViewById<AndroidX.Wear.Widget.WearableRecyclerView>(Resource.Id.waypoint_options_list);
            rv.HasFixedSize = true;
            rv.SetLayoutManager(new LinearLayoutManager(this));
            rv.SetAdapter(new RouteMenuItemsAdaptor(this));
        }



        /// <inheritdoc/>
        protected override void OnSaveInstanceState(Bundle saveState)
        {
            saveState.PutParcelable(CurrentLocationKey, _currentLocation);

            base.OnSaveInstanceState(saveState);
        }


        /// <inheritdoc/>
        protected override void OnRestoreInstanceState(Bundle savedState)
        {
            base.OnRestoreInstanceState(savedState);

            _currentLocation = AndroidX.Core.OS.BundleCompat.GetParcelable(savedState, CurrentLocationKey, Java.Lang.Class.FromType(typeof(Android.Locations.Location))) as Android.Locations.Location;
        }


        /// <inheritdoc/>
        protected override void OnResume()
        {
            base.OnResume();

            // Waypoint counts may have changed, e.g. by ticking waypoints in WaypointsListActivity
            RefreshMenu();
        }



        private class RouteMenuItemsAdaptor : RecyclerView.Adapter
        {
            public RouteMenuItemsAdaptor(RouteActivity activity)
            {
                this._activity = activity;
            }

            private readonly RouteActivity _activity;


            /// <inheritdoc/>
            public override int ItemCount
            {
                get { return 5; }
            }


            /// <inheritdoc/>
            public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
            {
                // Clear the view holder's click event as it may have been set before.
                var vh = (RouteMenuItemViewHolder)holder;
                vh.ResetClickHandler();

                var img = vh.ItemView.RequireViewById<ImageView>(Resource.Id.item_img);
                var txt = vh.ItemView.RequireViewById<TextView>(Resource.Id.item_text);

                var wpMgr = RouteManagerFactory.Create(this._activity);
                var totalWaypointCount = wpMgr.GetWaypointsCount(AppSettings.RouteId);

                switch (position)
                {
                    case 0:
                        var tickedWaypointCount = wpMgr.GetTickedWaypointsCount(AppSettings.RouteId);
                        txt.Text = _activity.GetString(Resource.String.listWaypoints) + $" ({tickedWaypointCount}/{totalWaypointCount})";
                        txt.Enabled = (totalWaypointCount > 0);
                        img.SetImageResource(Resource.Drawable.ic_list);
                        img.Enabled = txt.Enabled;
                        if (totalWaypointCount > 0)
                        {
                            vh.Click += _activity.listWaypoints_Click;
                        }
                        break;
                    case 1:
                        txt.SetText(Resource.String.sendToPhone);
                        txt.Enabled = (totalWaypointCount > 0);
                        img.SetImageResource(Resource.Drawable.ic_send_to_mobile);
                        img.Enabled = txt.Enabled;
                        if (totalWaypointCount > 0)
                        {
                            vh.Click += async (o, e) => { await _activity.sendToPhone_Click(o, e); };
                        }
                        break;
                    case 2:
                        // Always enabled: without a location, it says why it can't add a waypoint, as the long press
                        // on the main screen's route button does.
                        txt.SetText(Resource.String.addWaypoint);
                        txt.Enabled = true;
                        img.SetImageResource(Resource.Drawable.ic_add);
                        img.Enabled = txt.Enabled;
                        vh.Click += _activity.addWaypoint_Click;
                        break;
                    case 3:
                        txt.SetText(Resource.String.deleteLastWaypoint);
                        txt.Enabled = (totalWaypointCount > 0);
                        img.SetImageResource(Resource.Drawable.ic_clear);
                        img.Enabled = txt.Enabled;
                        if (totalWaypointCount > 0)
                        {
                            vh.Click += _activity.deleteLastWaypoint_Click;
                        }
                        break;
                    case 4:
                        txt.SetText(Resource.String.newRoute);
                        txt.Enabled = true;
                        img.SetImageResource(Resource.Drawable.ic_playlist_add);
                        img.Enabled = txt.Enabled;
                        vh.Click += _activity.newRoute_Click;
                        break;
                }
            }

            /// <inheritdoc/>
            public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
            {
                var v = LayoutInflater.From(parent.Context)!.Inflate(Resource.Layout.list_menu_item, parent, false)!;
                return new RouteMenuItemViewHolder(v);
            }
        }


        private class RouteMenuItemViewHolder : RecyclerView.ViewHolder
        {
            public RouteMenuItemViewHolder(View view)
                : base(view)
            {
                this.ItemView = view;
                AttachClickHandler(view);
            }


            public event EventHandler? Click;


            public void ResetClickHandler()
            {
                this.Click = null;
            }


            private void AttachClickHandler(View view)
            {
                if (view is ViewGroup vg)
                {
                    for (var i = 0; i < vg.ChildCount; i++)
                    {
                        AttachClickHandler(vg.GetChildAt(i)!);
                    }
                }
                else
                {
                    view.Click += (o, e) => { this.Click?.Invoke(this, e); };
                }
            }
        }


        private void RefreshMenu()
        {
            // The route's name, which changes when a new route is started or one arrives from the phone
            var route = RouteManagerFactory.Create(this).GetRoute(AppSettings.RouteId);
            RequireViewById<TextView>(Resource.Id.routeNameTxt).Text = route?.Label ?? string.Empty;

            RequireViewById<AndroidX.Wear.Widget.WearableRecyclerView>(Resource.Id.waypoint_options_list).GetAdapter()?.NotifyDataSetChanged();
        }


        private void addWaypoint_Click(object? sender, EventArgs e)
        {
            var wpMgr = RouteManagerFactory.Create(this);
            if (_currentLocation == null)
            {
                // E.g. the app has restarted since the last fix
                Toast.MakeText(this, Resource.String.noLocationForWaypointMsg, ToastLength.Long)?.Show();
                return;
            }
            // Name it with its latitude and longitude, as MainActivity does for waypoints added with a long press
            var waypoint = new Waypoint().From(_currentLocation);
            waypoint.Label = (new LatLonGridSystemProvider() as IGridSystemProvider).GetGridReference(new GeoLib.Location().From(_currentLocation), 100);
            wpMgr.AddWaypoint(AppSettings.RouteId, waypoint);
            Toast.MakeText(this, Resource.String.waypointAddedMsg, ToastLength.Short)?.Show();
            RefreshMenu();
        }


        private void listWaypoints_Click(object? sender, EventArgs e)
        {
            StartActivity(new Intent(this, typeof(WaypointsListActivity)));  // OnResume() refreshes the menu on return
        }


        private void deleteLastWaypoint_Click(object? sender, EventArgs e)
        {
            var adb = new WearableAlertDialog.Builder(this);
            adb.SetCancelable(true);
            adb.SetTitle(Resource.String.deleteTitle);
            adb.SetMessage(Resource.String.waypointsDeleteLastPrompt);
            adb.SetPositiveButton(handler: (sender, args) =>
            {
                var wpMgr = RouteManagerFactory.Create(this);
                wpMgr.DeleteLastWaypoint(AppSettings.RouteId);
                Toast.MakeText(this, Resource.String.waypointsLastDeletedMsg, ToastLength.Short)?.Show();
                RefreshMenu();
            });
            adb.SetNegativeButton();
            adb.Create().Show();
        }
        
        
        private void newRoute_Click(object? sender, EventArgs e)
        {
            var adb = new WearableAlertDialog.Builder(this);
            adb.SetCancelable(true);
            adb.SetTitle(Resource.String.newRouteTitle);
            adb.SetMessage(Resource.String.newRoutePrompt);
            adb.SetPositiveButton(handler: (sender, args) =>
            {
                // Replace the route, rather than just deleting its waypoints, so that the new route doesn't keep the old
                // one's name when it's sent to the phone. Name it with today's date, to tell it apart there.
                var date = Android.Text.Format.DateFormat.GetMediumDateFormat(this)!.Format(new Java.Util.Date());
                var wpMgr = RouteManagerFactory.Create(this);
                wpMgr.GetOrCreateRoute(AppSettings.RouteId, overwrite: true, label: this.GetFormattedString(Resource.String.newRouteDatedName, date));
                Toast.MakeText(this, Resource.String.newRouteStartedMsg, ToastLength.Short)?.Show();
                RefreshMenu();
            });
            adb.SetNegativeButton();
            adb.Create().Show();
        }


        private async Task sendToPhone_Click(object? sender, EventArgs e)
        {
            try
            {
                using (var capabilityClient = WearableClass.GetCapabilityClient(Android.App.Application.Context))
                {
                    var capabilityInfo = await capabilityClient.GetCapabilityAsync(WearMessage.RouteManagerCapability, CapabilityClient.FilterReachable);
                    var nodes = capabilityInfo?.Nodes;

                    if (nodes == null || nodes?.Count == 0)
                    {
                        // No paired device
                        var adb = new WearableAlertDialog.Builder(this);
                        adb.SetTitle(Resource.String.error);
                        adb.SetMessage(Resource.String.deviceOrAppNotFoundErrorMsg);
                        adb.SetPositiveButton();
                        adb.Create().Show();
                        return;
                    }
                    else if (nodes?.Count == 1 && nodes.ElementAt(0) != null)
                    {
                        // Single node. Just send it.
                        await sendToDeviceAndFinish(nodes.ElementAt(0).Id, nodes.ElementAt(0).DisplayName);
                        return;
                    }
                    else if (nodes?.Count > 1)
                    {
                        // Multiple nodes. Let the user choose one. The launcher's callback sends the route.
                        _selectDeviceLauncher.Launch(DeviceListActivity.CreateIntent(this, nodes));
                        return;
                    }

                }
            }
            catch(Exception)
            {
                var adb = new WearableAlertDialog.Builder(this);
                adb.SetTitle(Resource.String.error);
                adb.SetMessage(Resource.String.deviceOrAppCommsErrorMsg);
                adb.SetPositiveButton();
                adb.Create().Show();
                return;
            }
        }


        private async Task sendToDeviceAndFinish(
            string nodeId,
            string nodeDisplayName)
        {
            var mgr = RouteManagerFactory.Create(this);
            var route = mgr.GetRoute(AppSettings.RouteId);
            var payload = Encoding.UTF8.GetBytes((route ?? throw new InvalidOperationException("No route")).ToJson());

            try
            {
                await ThisApp.Instance().MessageClient.SendMessageAsync(nodeId, WearMessage.Put + WearMessage.Data_Route, payload);
                var msg = this.GetFormattedString(Resource.String.routeSentToDeviceMsg, nodeDisplayName);
                Toast.MakeText(this, msg, ToastLength.Short)?.Show();
                SetResult(Result.Ok);
                Finish();
            }
            catch(Exception)
            {
                var adb = new WearableAlertDialog.Builder(this);
                adb.SetTitle(Resource.String.error);
                adb.SetMessage(Resource.String.deviceOrAppCommsErrorMsg);
                adb.SetPositiveButton();
                adb.Create().Show();
            }
        }
    }
}
