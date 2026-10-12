using System;
using System.Globalization;
using System.Linq;

using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Content;
using Android.Content.PM;
using Android.Widget;
using Android.Media;

using AndroidX.RecyclerView.Widget;
using AndroidX.AppCompat.Widget;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.View;
using AndroidX.ViewPager2.Widget;
using ActionMode = AndroidX.AppCompat.View.ActionMode;  // Use androidx rather than Android.Views
using AlertDialog = AndroidX.AppCompat.App.AlertDialog;

using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.Tabs;

using Romer.DataLib;
using Kilsten.Android;
using Romer.CommonLib;
using Romer.GeoLib;



namespace Romer.Mobile.App
{
    /// <summary>
    /// Shows a route, on two tabs: route info (summary, map and charts) and the list of waypoints.
    /// </summary>
    [Activity(Theme = "@style/AppTheme", ScreenOrientation = ScreenOrientation.Portrait)]
    public class RouteActivity : AppCompatActivity, ActionMode.ICallback
    {
        /// <summary>
        /// Intent extra: ID of the route to show (int).
        /// </summary>
        public const string RouteIdKey = "RouteIdKey";


        /// <inheritdoc/>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.route_activity);
            EdgeToEdge.ApplySystemBarInsets(RequireViewById<ViewGroup>(Android.Resource.Id.Content).GetChildAt(0)!, RequireViewById<View>(Resource.Id.appBar));
            SetSupportActionBar(RequireViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.toolbar));
            SupportActionBar!.SetDisplayHomeAsUpEnabled(true);
            SupportActionBar!.SetDisplayShowHomeEnabled(true);

            var fab = RequireViewById<FloatingActionButton>(Resource.Id.fab);
            fab.Click += FabOnClick;

            var savedState = savedInstanceState ?? this.Intent?.Extras;
            if (savedState == null)
                throw new InvalidOperationException();
            this._routeId = savedState.GetInt(RouteIdKey);

            var mgr = RouteManagerFactory.Create(this);
            var route = mgr.GetRoute(_routeId);
            if (route == null)
            {
                // Route has been deleted
                Finish();
                return;
            }
            this.Title = route.Label;

            // Tab pages. Keep both pages attached, as ViewPager did, so that their views can always be found by ID.
            var viewPager = RequireViewById<ViewPager2>(Resource.Id.viewPager);
            viewPager.Adapter = new RouteActivityPagerAdapter(this);
            viewPager.OffscreenPageLimit = 1;
            viewPager.RegisterOnPageChangeCallback(new PageChangeCallback(this, viewPager));

            // Tabs, kept in step with the pages
            var tabLayout = RequireViewById<TabLayout>(Resource.Id.tabLayout);
            new TabLayoutMediator(tabLayout, viewPager, new TabConfigurationStrategy()).Attach();

            // ViewPager2 doesn't always report the initial page as selected, so fill it in once the pages are laid out
            viewPager.Post(() => PageSelected(viewPager.CurrentItem));
        }


        private int _routeId;
        private ActionMode? _actionMode;

        // Adapters for the tab pages. Null until the page's tab has been selected.
        private ItemListAdapter? WaypointsAdapter => RequireViewById<RecyclerView>(Resource.Id.waypointsList).GetAdapter() as ItemListAdapter;
        private RouteActivityInfoCardListAdapter? InfoCardsAdapter => RequireViewById<RecyclerView>(Resource.Id.infoCardsList).GetAdapter() as RouteActivityInfoCardListAdapter;


        /// <inheritdoc/>
        protected override void OnSaveInstanceState(Bundle saveState)
        {
            saveState.PutInt(RouteIdKey, this._routeId);

            base.OnSaveInstanceState(saveState);
        }


        #region Tab layout

        private class TabConfigurationStrategy : Java.Lang.Object, TabLayoutMediator.ITabConfigurationStrategy
        {
            public void OnConfigureTab(TabLayout.Tab tab, int position)
            {
                tab.SetText(position == RouteActivityPagerAdapter.InfoPage ? Resource.String.routeInfoTabTitle : Resource.String.routeWaypointsTabTitle);
            }
        }


        private class PageChangeCallback : ViewPager2.OnPageChangeCallback
        {
            public PageChangeCallback(RouteActivity activity, ViewPager2 viewPager)
            {
                this._activity = activity;
                this._viewPager = viewPager;
            }

            private readonly RouteActivity _activity;
            private readonly ViewPager2 _viewPager;

            /// <inheritdoc/>
            public override void OnPageSelected(int position)
            {
                // Post, so that the page's views exist when PageSelected() looks for them
                _viewPager.Post(() => _activity.PageSelected(position));
            }
        }


        // The page that PageSelected() last filled in. Used to skip the duplicate call for the initial page.
        private int _filledPage = -1;


        /// <summary>
        /// Fill in a page when it's selected. Pages are refilled each time, so they show changes made on the other page.
        /// </summary>
        /// <param name="position">Page position</param>
        private void PageSelected(int position)
        {
            if (position == _filledPage || IsFinishing || IsDestroyed)
                return;
            _filledPage = position;
            _actionMode?.Finish();

            if (position == RouteActivityPagerAdapter.InfoPage)
            {
                // Info
                var infoListAdapter = new RouteActivityInfoCardListAdapter(this, this._routeId);
                infoListAdapter.InitialiseDataSet();
                infoListAdapter.RouteMapWaypointClick += (o, e) => { ActionEditWaypointProperties(e.WaypointId); };

                var infoRv = RequireViewById<RecyclerView>(Resource.Id.infoCardsList);
                infoRv.HasFixedSize = true;
                infoRv.SetLayoutManager(new LinearLayoutManager(this));
                infoRv.NestedScrollingEnabled = false;
                infoRv.SetAdapter(infoListAdapter);
            }
            else if (position == RouteActivityPagerAdapter.WaypointsPage)
            {
                // Waypoints
                var waypointsListAdapter = new ItemListAdapter(this, Resource.Layout.waypoint_list_item);
                waypointsListAdapter.ItemClick += WaypointsAdapter_ItemClick;
                waypointsListAdapter.ItemBind += WaypointsAdapter_ItemBind;
                waypointsListAdapter.SelectionChanged += WaypointsAdapter_SelectionChanged;

                var waypointsRv = RequireViewById<RecyclerView>(Resource.Id.waypointsList);
                waypointsRv.HasFixedSize = false;
                waypointsRv.SetLayoutManager(new LinearLayoutManager(this));
                waypointsRv.NestedScrollingEnabled = false;
                waypointsRv.SetAdapter(waypointsListAdapter);

                var mgr = RouteManagerFactory.Create(this);
                waypointsListAdapter.ItemIds = mgr.GetWaypointIds(this._routeId);
            }
        }

        #endregion Tab layout


        private void FabOnClick(object? sender, EventArgs eventArgs)
        {
            this._actionMode?.Finish();

            var intent = new Intent(this, typeof(ShareActivity));
            intent.PutExtra(ShareActivity.RouteIdKey, _routeId);
            StartActivity(intent);
        }


        private void WaypointsAdapter_ItemClick(object? sender, ItemListAdapter.ClickEventArgs e)
        {
            var mgr = RouteManagerFactory.Create(this);
            var waypointId = mgr.GetWaypointIds(this._routeId)[e.Position];
            ActionEditWaypointProperties(waypointId);
        }


        private void WaypointsAdapter_ItemBind(object? sender, ItemListAdapter.BindItemEventArgs e)
        {
            var mgr = RouteManagerFactory.Create(this);
            var waypoint = mgr.GetWaypoint(this._routeId, e.ItemId);
            if (waypoint == null)
                return;  // Deleted
            e.ItemView.RequireViewById<TextView>(Resource.Id.waypointName).Text = waypoint.Label;
            e.ItemView.RequireViewById<TextView>(Resource.Id.waypointDescription).Text = waypoint.Notes;
        }


        private void WaypointsAdapter_SelectionChanged(object? sender, ItemListAdapter.SelectionChangedEventArgs e)
        {
            var waypointsAdapter = (ItemListAdapter)sender!;
            if (waypointsAdapter.SelectionCount > 0)
            {
                if (_actionMode == null)
                {
                    _actionMode = StartSupportActionMode(this as ActionMode.ICallback);
                }
                else
                {
                    _actionMode.Invalidate();
                }
                // At this point ActionMode.ICallback.OnPrepareActionMode() is called
            }
            else
            {
                if (_actionMode != null)
                {
                    _actionMode.Finish();
                }
            }
        }


        #region Options menu


        /// <inheritdoc/>
        public override bool OnCreateOptionsMenu(IMenu? menu)
        {
            MenuInflater.Inflate(Resource.Menu.route_activity_menu, menu);
            return true;
        }


        /// <inheritdoc/>
        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch(item.ItemId)
            {
                case Android.Resource.Id.Home:
                    Finish();
                    return true;
                case Resource.Id.reverseWaypoints:
                    // Load, reverse, and save the route
                    var mgr = RouteManagerFactory.Create(this);
                    var route = mgr.GetRoute(this._routeId);
                    if (route == null)
                        return true;  // Deleted
                    mgr.UpdateRoute(route.ReverseWaypoints());  // Persists the new order atomically
                    // Refresh the waypoint list
                    RefreshWaypointsList();
                    this._actionMode?.Finish();
                    // Refresh the info tab
                    var adapter = InfoCardsAdapter;
                    adapter?.InitialiseDataSet();
                    adapter?.NotifyDataSetChanged();
                    return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        #endregion Options menu


        private void ActionEditWaypointProperties(int waypointId)
        {
            var userSettings = UserSettings.Load(this);
            var altitudeUnitSymbol = ValueFormatter.ToString(this, userSettings.Navigation_PreferredAltitudeUnit);

            var mgr = RouteManagerFactory.Create(this);
            var waypoint = mgr.GetWaypoint(_routeId, waypointId);
            if (waypoint == null)
                return;  // Deleted

            var adb = new AlertDialog.Builder(this, Resource.Style.AppTheme_Dialog_Alert);
            adb.SetTitle(this.RequireResources().GetQuantityString(Resource.Plurals.waypointEdit, 1));
            var dlgView = this.LayoutInflater.Inflate(Resource.Layout.dlg_waypoint_properties, null)!;
            dlgView.RequireViewById<EditText>(Resource.Id.nameTxt).Text = waypoint.Label;
            dlgView.RequireViewById<EditText>(Resource.Id.descriptionTxt).Text = waypoint.Notes;
            // Numbers are shown without digit grouping, which the validator doesn't accept.
            var latitudeStr = waypoint.Latitude.ToString("0.######", CultureInfo.CurrentCulture);
            var longitudeStr = waypoint.Longitude.ToString("0.######", CultureInfo.CurrentCulture);
            var altitudeStr = waypoint.Altitude.HasValue
                ? UnitConverter.Convert((decimal)waypoint.Altitude.Value, LengthUnit.Metre, userSettings.Navigation_PreferredAltitudeUnit).ToString("0.#", CultureInfo.CurrentCulture)
                : string.Empty;
            dlgView.RequireViewById<EditText>(Resource.Id.latitudeTxt).Text = latitudeStr;
            dlgView.RequireViewById<EditText>(Resource.Id.longitudeTxt).Text = longitudeStr;
            var txt = dlgView.RequireViewById<TextView>(Resource.Id.altitudeLbl);
            txt.Text = txt.Text?.Replace("$", altitudeUnitSymbol);
            txt = dlgView.RequireViewById<TextView>(Resource.Id.altitudeTxt);
            txt.Hint = txt.Hint?.Replace("$", altitudeUnitSymbol);
            txt.Text = altitudeStr;
            adb.SetView(dlgView);
            adb.SetPositiveButton(Android.Resource.String.Ok, (IDialogInterfaceOnClickListener?)null);
            adb.SetNegativeButton(Android.Resource.String.Cancel, (IDialogInterfaceOnClickListener?)null);
            var validator = Kilsten.Android.Validation.DialogFormValidator.CreateFrom(adb, dlgView);
            validator.SetLengthMin(Resource.Id.nameTxt, 3)
                .SetNumValueRange(Resource.Id.latitudeTxt, valueMin: -90, valueMax: +90)
                .SetNumValueRange(Resource.Id.longitudeTxt, valueMin: -180, valueMax: +180)
                .SetNumValueRange(Resource.Id.altitudeTxt, valueMin: -1500, valueMax: 30000, required: false);  // Metres or feet
            validator.Valid += (o, e) =>
            {
                // Update the waypoint in the db.
                // Values that weren't edited are kept as they were, so that rounding for display doesn't change them.
                waypoint.Label = dlgView.RequireViewById<EditText>(Resource.Id.nameTxt).Text?.Trim() ?? string.Empty;
                waypoint.Notes = dlgView.RequireViewById<EditText>(Resource.Id.descriptionTxt).Text?.Trim() ?? string.Empty;
                var str = dlgView.RequireViewById<EditText>(Resource.Id.latitudeTxt).Text?.Trim() ?? string.Empty;
                if (str != latitudeStr)
                    waypoint.Latitude = ParseNumber(str);
                str = dlgView.RequireViewById<EditText>(Resource.Id.longitudeTxt).Text?.Trim() ?? string.Empty;
                if (str != longitudeStr)
                    waypoint.Longitude = ParseNumber(str);
                str = dlgView.RequireViewById<EditText>(Resource.Id.altitudeTxt).Text?.Trim() ?? string.Empty;
                if (str != altitudeStr)
                {
                    waypoint.Altitude = string.IsNullOrEmpty(str)
                        ? null
                        : (double)UnitConverter.Convert((decimal)ParseNumber(str), userSettings.Navigation_PreferredAltitudeUnit, LengthUnit.Metre);
                }
                mgr.UpdateWaypoint(waypoint);

                // Refresh the waypoint list
                RefreshWaypointsListItem(waypoint.Id);
                this._actionMode?.Finish();

                // Refresh the info tab
                var adapter = InfoCardsAdapter;
                adapter?.InitialiseDataSet();
                adapter?.NotifyDataSetChanged();
            };
        }


        /// <summary>
        /// Parse a number the same way as the Kilsten form validator: in the current culture, then the invariant culture.
        /// </summary>
        private static double ParseNumber(string str)
        {
            if (double.TryParse(str, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ||
                double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }
            throw new FormatException($"Not a number: {str}");
        }


        private void ActionShareWaypoints(int[] waypointIds)
        {
            this._actionMode?.Finish();

            var intent = new Intent(this, typeof(ShareActivity));
            intent.PutExtra(ShareActivity.RouteIdKey, _routeId);
            intent.PutExtra(ShareActivity.WaypointIdsKey, waypointIds);
            intent.PutExtra(ShareActivity.ShareAsRouteKey, waypointIds.Length != 1);
            StartActivity(intent);
        }


        private void ActionDeleteWaypoints(int[] waypointIds)
        {
            var adb = new AlertDialog.Builder(this, Resource.Style.AppTheme_Dialog_Alert);
            adb.SetTitle(this.RequireResources().GetQuantityString(Resource.Plurals.waypointDelete, waypointIds.Length));
            adb.SetMessage(this.RequireResources().MakeQuestionQuantityString(Resource.Plurals.waypointDeleteCount, waypointIds.Length, waypointIds.Length));
            adb.SetPositiveButton(Android.Resource.String.Ok, (o, e) =>
            {
                var mgr = RouteManagerFactory.Create(this);
                foreach (var waypointId in waypointIds)
                {
                    mgr.DeleteWaypoint(this._routeId, waypointId);
                }
                this._actionMode?.Finish();
                RefreshWaypointsList();
            });
            adb.SetNegativeButton(Android.Resource.String.Cancel, (IDialogInterfaceOnClickListener?)null);
            adb.Show();
        }


        private void RefreshWaypointsList()
        {
            var adapter = WaypointsAdapter;
            if (adapter != null)
            {
                var mgr = RouteManagerFactory.Create(this);
                adapter.ItemIds = mgr.GetWaypointIds(this._routeId);
                adapter.NotifyDataSetChanged();
            }
        }


        private void RefreshWaypointsListItem(int waypointId)
        {
            var adapter = WaypointsAdapter;
            if (adapter != null)
            {
                var pos = adapter.GetPositionForItemId(waypointId);
                if (pos != -1)
                {
                    adapter.NotifyItemChanged(pos);
                }
            }
        }


        #region ActionMode.ICallback

        bool ActionMode.ICallback.OnCreateActionMode(ActionMode? mode, IMenu? menu)
        {
            MenuInflater.Inflate(Resource.Menu.waypoints_action_mode_menu, menu);
            return true;
        }


        bool ActionMode.ICallback.OnPrepareActionMode(ActionMode? mode, IMenu? menu)
        {
            var waypointsAdapter = WaypointsAdapter;
            if (mode == null || menu == null || waypointsAdapter == null)
                return false;

            var numSelectedItems = waypointsAdapter.SelectionCount;
            if (numSelectedItems > 0)
            {
                mode.Title = numSelectedItems.ToString();  // this._actionMode is null here
            }

            menu.FindItem(Resource.Id.edit)?.SetVisible(numSelectedItems == 1);
            menu.FindItem(Resource.Id.share)?.SetVisible(waypointsAdapter.ItemCount > 0);
            menu.FindItem(Resource.Id.delete)?.SetVisible(numSelectedItems > 0);
            menu.FindItem(Resource.Id.selectAll)?.SetVisible(numSelectedItems > 0);

            return true;
        }


        bool ActionMode.ICallback.OnActionItemClicked(ActionMode? mode, IMenuItem? item)
        {
            var waypointsAdapter = WaypointsAdapter;
            if (waypointsAdapter == null)
                return false;

            switch (item?.ItemId)
            {
                case Resource.Id.edit:
                    ActionEditWaypointProperties(waypointsAdapter.SelectedIds[0]);
                    return true;
                case Resource.Id.share:
                    ActionShareWaypoints(waypointsAdapter.SelectedIds);
                    return true;
                case Resource.Id.delete:
                    ActionDeleteWaypoints(waypointsAdapter.SelectedIds);
                    return true;
                case Resource.Id.selectAll:
                    waypointsAdapter.SelectAll();
                    return true;
                default:
                    return false;
            }
        }


        void ActionMode.ICallback.OnDestroyActionMode(ActionMode? mode)
        {
            WaypointsAdapter?.DeselectAll();

            this._actionMode = null;
        }

        #endregion ActionMode.ICallback
    }
}
