using System;
using System.Linq;
using System.IO;
using System.Xml;

using Android.Util;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Content;
using Android.Content.PM;
using Android.Widget;
using Android.Media;
using Android.Gms.Wearable;

using AndroidX.RecyclerView.Widget;
using AndroidX.AppCompat.Widget;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.View;
using AndroidX.Core.App;
using ActionMode = AndroidX.AppCompat.View.ActionMode;  // Use androidx rather than Android.Views
using AlertDialog = AndroidX.AppCompat.App.AlertDialog;

using Google.Android.Material.FloatingActionButton;

using Kilsten.Android;

using Romer.DataLib;
using Romer.CommonLib;
using Romer.Mobile.App.Preference;


namespace Romer.Mobile.App
{
    /// <summary>
    /// The phone app's main screen: the list of routes. Also imports GPX files that other apps send or open with it.
    /// </summary>
    [Activity(Label = "@string/appName", MainLauncher = true, ScreenOrientation = ScreenOrientation.Portrait)]
    // Many apps, including the Files app, don't recognise .gpx files and send them as application/octet-stream, sometimes
    // with a URI that doesn't contain the file name. So accept that type too: HandleIncomingIntent() checks the file name.
    [IntentFilter(new[] { Intent.ActionSend }, Categories = new[] { Intent.CategoryDefault },
                  DataMimeTypes = new[] { AppSettings.GpxMimeType, AppSettings.UnknownMimeType })]
    [IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
                  DataSchemes = new[] { "content" }, DataMimeTypes = new[] { AppSettings.GpxMimeType, AppSettings.UnknownMimeType })]
    [IntentFilter(new[] { Intent.ActionView }, Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
                  DataSchemes = new[] { "file", "content", "data" }, DataMimeType = "*/*", DataHost = "*", 
                  DataPathPatterns = new[] {
                      ".*\\.gpx", ".*\\.GPX",
                      /* See https://stackoverflow.com/q/3400072/67316 */
                      ".*\\..*\\.gpx",                     ".*\\..*\\.GPX",
                      ".*\\..*\\..*\\.gpx",                ".*\\..*\\..*\\.GPX",
                      ".*\\..*\\..*\\..*\\.gpx",           ".*\\..*\\..*\\..*\\.GPX",
                      ".*\\..*\\..*\\..*\\..*\\.gpx",      ".*\\..*\\..*\\..*\\..*\\.GPX",
                      ".*\\..*\\..*\\..*\\..*\\..*\\.gpx", ".*\\..*\\..*\\..*\\..*\\..*\\.GPX"
                  } )]
    public class MainActivity : AppCompatActivity, ActionMode.ICallback
    {
        // State
        private ActionMode? _actionMode;

        // Activity result launchers, registered in OnCreate()
        private AndroidX.Activity.Result.ActivityResultLauncher _notificationPermissionLauncher = null!;
        private AndroidX.Activity.Result.ActivityResultLauncher _openGpxLauncher = null!;

        // The routes list adapter, which OnCreate() sets up
        private ItemListAdapter RoutesAdapter => (ItemListAdapter)RequireViewById<RecyclerView>(Resource.Id.routesList).GetAdapter()!;


        /// <inheritdoc/>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            // Handle the splash screen transition.
            var splashScreen = AndroidX.Core.SplashScreen.SplashScreen.InstallSplashScreen(this);

            base.OnCreate(savedInstanceState);

            // Notification permission. Nothing to do with the result: if it's refused, routes still arrive, without a notification.
            _notificationPermissionLauncher = this.RegisterForPermissionResult(granted => { });

            // Choosing a GPX file to import. Null if the user backed out of the file picker.
            _openGpxLauncher = this.RegisterForResult<Android.Net.Uri>(new AndroidX.Activity.Result.Contract.ActivityResultContracts.OpenDocument(), gpxUri =>
            {
                if (gpxUri != null)
                    ImportGpxFile(gpxUri, null);
            });

            SetContentView(Resource.Layout.main_activity);
            EdgeToEdge.ApplySystemBarInsets(RequireViewById<ViewGroup>(Android.Resource.Id.Content).GetChildAt(0)!, RequireViewById<View>(Resource.Id.appBar));
            SetSupportActionBar(RequireViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.toolbar));

            var fab = RequireViewById<FloatingActionButton>(Resource.Id.fab);
            fab.Click += FabOnClick;

            // Ask for permission to post notifications when a route arrives from the watch. ThisApp creates the channel.
            if (savedInstanceState == null &&
                OperatingSystem.IsAndroidVersionAtLeast(33) &&
                CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
            {
                _notificationPermissionLauncher.Launch(Android.Manifest.Permission.PostNotifications);
            }

            var routesRv = RequireViewById<RecyclerView>(Resource.Id.routesList);
            routesRv.HasFixedSize = false;
            routesRv.SetLayoutManager(new LinearLayoutManager(this));
            routesRv.NestedScrollingEnabled = false;

            var adapter = new ItemListAdapter(this, Resource.Layout.route_list_item);
            adapter.ItemClick += RoutesAdapter_ItemClick;
            adapter.ItemBind += RoutesAdapter_ItemBind;
            adapter.SelectionChanged += RoutesAdapter_SelectionChanged;
            routesRv.SetAdapter(adapter);

            // Import a file that another app sent. Only on first creation: a recreated activity has the same intent, and
            // has already handled it.
            if (savedInstanceState == null && this.Intent != null)
            {
                HandleIncomingIntent(this.Intent);
            }

            RefreshRoutesList();
        }


        /// <inheritdoc/>
        protected override void OnResume()
        {
            base.OnResume();

            RefreshRoutesList();
            WearMessageListenerService.RouteReceived += WearMessageListenerService_RouteReceived;
        }


        /// <inheritdoc/>
        protected override void OnPause()
        {
            base.OnPause();

            WearMessageListenerService.RouteReceived -= WearMessageListenerService_RouteReceived;
        }


        private void WearMessageListenerService_RouteReceived(object? sender, EventArgs e)
        {
            // A route arrived from a watch while this activity is in the foreground.
            RunOnUiThread(RefreshRoutesList);
        }


        /// <inheritdoc/>
        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            if (intent != null)
            {
                HandleIncomingIntent(intent);
            }
        }


        #region Options menu

        /// <inheritdoc/>
        public override bool OnCreateOptionsMenu(IMenu? menu)
        {
            MenuInflater.Inflate(Resource.Menu.main_activity_menu, menu);

#if DEBUG
            var debugMenu = menu?.AddSubMenu("Debug");
            if (debugMenu != null)
            {
                debugMenu.Add(IMenu.None, 900, IMenu.None, "Demo Data")?.SetShowAsAction(ShowAsAction.Never);
                debugMenu.Add(IMenu.None, 901, IMenu.None, "Dump DB")?.SetShowAsAction(ShowAsAction.Never);
                debugMenu.Add(IMenu.None, 902, IMenu.None, "Clear DB")?.SetShowAsAction(ShowAsAction.Never);
            }
#endif

            return true;
        }


        /// <inheritdoc/>
        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch(item.ItemId)
            {
                case Resource.Id.settings:
                    StartActivity(new Intent(this, typeof(Preference.PreferenceActivity)));
                    return true;
#if DEBUG
                case 900:
                    {
                        // Create demo route
                        var mgr = RouteManagerFactory.Create(this);
                        var routes = DemoData.CreateRoutes(this);
                        foreach (var route in routes)
                        {
                            mgr.CreateRoute(route);
                        }
                        RefreshRoutesList();
                        this._actionMode?.Finish();
                        return true;
                    }
                case 901:
                    {
                        Log.Debug("LOC", "Start of database dump ---------------------------------------------");
                        var dump = RouteManagerFactory.Create(this).Dump();
                        using (var rdr = new System.IO.StringReader(dump))
                        {
                            string? line;
                            while((line = rdr.ReadLine()) != null)
                            {
                                Log.Debug("LOC", line);
                            }
                        }
                        Log.Debug("LOC", "End of database dump -----------------------------------------------");
                        return true;
                    }
                case 902:
                    {
                        // Clear database
                        var mgr = RouteManagerFactory.Create(this);
                        foreach (var routeId in mgr.GetRouteIds())
                        {
                            mgr.DeleteAllWaypoints(routeId);
                            mgr.DeleteRoute(routeId);
                        }
                        RefreshRoutesList();
                        this._actionMode?.Finish();
                        return true;
                    }
#endif
            }

            return base.OnOptionsItemSelected(item);
        }

        #endregion Options menu


        private void FabOnClick(object? sender, EventArgs eventArgs)
        {
            this._actionMode?.Finish();

            // Offer a choice between a new, empty route and importing one from a GPX file.
            var popup = new AndroidX.AppCompat.Widget.PopupMenu(this, (View)sender!);
            popup.Inflate(Resource.Menu.add_route_menu);
            popup.MenuItemClick += (o, e) =>
            {
                switch (e.Item?.ItemId)
                {
                    case Resource.Id.newRoute:
                        ActionAddRoute();
                        break;
                    case Resource.Id.importGpx:
                        ActionImportGpx();
                        break;
                }
            };
            popup.Show();
        }


        private void RefreshRoutesList()
        {
            var mgr = RouteManagerFactory.Create(this);
            RoutesAdapter.ItemIds = mgr.GetRouteIds();
            RoutesAdapter.NotifyDataSetChanged();
        }


        private void RefreshRoutesListItem(int routeId)
        {
            var pos = RoutesAdapter.GetPositionForItemId(routeId);
            if (pos != -1)
            {
                RoutesAdapter.NotifyItemChanged(pos);
            }
        }


        private void RoutesAdapter_ItemClick(object? sender, ItemListAdapter.ClickEventArgs e)
        {
            var routesAdapter = RoutesAdapter;
            var intent = new Intent(this, typeof(RouteActivity));
            intent.PutExtra(RouteActivity.RouteIdKey, routesAdapter.ItemIds.ElementAt(e.Position));
            StartActivity(intent);
        }


        private void RoutesAdapter_ItemBind(object? sender, ItemListAdapter.BindItemEventArgs e)
        {
            var mgr = RouteManagerFactory.Create(this);
            var route = mgr.GetRoute(e.ItemId);
            if (route == null)
                return;  // Deleted
            e.ItemView.RequireViewById<TextView>(Resource.Id.routeName).Text = route.Label;
            e.ItemView.RequireViewById<TextView>(Resource.Id.routeDescription).Text = route.Notes;
        }


        private void RoutesAdapter_SelectionChanged(object? sender, ItemListAdapter.SelectionChangedEventArgs e)
        {
            var adapter = (ItemListAdapter)sender!;
            if (adapter.SelectionCount > 0)
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


        private void ActionEditRouteProperties(int routeId)
        {
            var mgr = RouteManagerFactory.Create(this);
            var route = mgr.GetRoute(routeId);
            if (route == null)
                return;  // Deleted

            var adb = new AlertDialog.Builder(this, Resource.Style.AppTheme_Dialog_Alert);
            adb.SetTitle(this.RequireResources().GetQuantityString(Resource.Plurals.routeEdit, 1));
            var dlgView = this.LayoutInflater.Inflate(Resource.Layout.dlg_route_properties, null)!;
            dlgView.RequireViewById<EditText>(Resource.Id.nameTxt).Text = route.Label;
            dlgView.RequireViewById<EditText>(Resource.Id.descriptionTxt).Text = route.Notes;
            adb.SetView(dlgView);
            adb.SetPositiveButton(Android.Resource.String.Ok, (IDialogInterfaceOnClickListener?)null);
            adb.SetNegativeButton(Android.Resource.String.Cancel, (IDialogInterfaceOnClickListener?)null);
            var validator = Kilsten.Android.Validation.DialogFormValidator.CreateFrom(adb, dlgView);
            validator.SetLengthMin(Resource.Id.nameTxt, 3);  // Routes can share a name
            validator.Valid += (o, e) =>
            {
                route.Label = dlgView.RequireViewById<EditText>(Resource.Id.nameTxt).Text?.Trim() ?? string.Empty;
                route.Notes = dlgView.RequireViewById<EditText>(Resource.Id.descriptionTxt).Text?.Trim() ?? string.Empty;
                mgr.UpdateRoute(route);
                RefreshRoutesListItem(route.Id);
                this._actionMode?.Finish();
            };                
        }


        private void ActionAddRoute()
        {
            var mgr = RouteManagerFactory.Create(this);

            var adb = new AlertDialog.Builder(this, Resource.Style.AppTheme_Dialog_Alert);
            adb.SetTitle(this.RequireResources().GetQuantityString(Resource.Plurals.routeAdd, 1));
            var dlgView = this.LayoutInflater.Inflate(Resource.Layout.dlg_route_properties, null)!;
            dlgView.RequireViewById<EditText>(Resource.Id.nameTxt).Text = GetString(Resource.String.newRouteName);
            adb.SetView(dlgView);
            adb.SetPositiveButton(Android.Resource.String.Ok, (o, e) => { });  // We substitute this below
            adb.SetNegativeButton(Android.Resource.String.Cancel, (IDialogInterfaceOnClickListener?)null);
            var validator = Kilsten.Android.Validation.DialogFormValidator.CreateFrom(adb, dlgView);
            validator.SetLengthMin(Resource.Id.nameTxt, 3);  // Routes can share a name
            validator.Valid += (o, e) =>
            {
                var route = new DataLib.Route()
                {
                    CreationTime = DateTime.UtcNow,
                    Label = dlgView.RequireViewById<EditText>(Resource.Id.nameTxt).Text?.Trim() ?? string.Empty,
                    Notes = dlgView.RequireViewById<EditText>(Resource.Id.descriptionTxt).Text?.Trim() ?? string.Empty
                };
                mgr.CreateRoute(route);
                RefreshRoutesList();
                this._actionMode?.Finish();
            };
        }


        private void ActionImportGpx()
        {
            // Accept any type: many document providers don't recognise .gpx files, and report them as
            // application/octet-stream or similar. ImportGpxFile() checks the file name instead.
            _openGpxLauncher.Launch(new[] { "*/*" });
        }


        private void ActionShareRoute(int routeId)
        {
            this._actionMode?.Finish();

            var intent = new Intent(this, typeof(ShareActivity));
            intent.PutExtra(ShareActivity.RouteIdKey, routeId);
            StartActivity(intent);
        }


        private void ActionDeleteRoutes(int[] routeIds)
        {
            var adb = new AlertDialog.Builder(this, Resource.Style.AppTheme_Dialog_Alert);
            adb.SetTitle(this.RequireResources().GetQuantityString(Resource.Plurals.routeDelete, routeIds.Length));
            adb.SetMessage(this.RequireResources().MakeQuestionQuantityString(Resource.Plurals.routeDeleteCount, routeIds.Length, routeIds.Length));
            adb.SetPositiveButton(Android.Resource.String.Ok, (o, e) => 
            {
                var mgr = RouteManagerFactory.Create(this);
                foreach(var routeId in routeIds)
                {
                    mgr.DeleteRoute(routeId);
                }
                this._actionMode?.Finish();
                RefreshRoutesList();
            });
            adb.SetNegativeButton(Android.Resource.String.Cancel, (IDialogInterfaceOnClickListener?)null);
            adb.Show();
        }



        #region ActionMode.ICallback

        bool ActionMode.ICallback.OnCreateActionMode(ActionMode? mode, IMenu? menu)
        {
            MenuInflater.Inflate(Resource.Menu.routes_action_mode_menu, menu);
            return true;
        }


        bool ActionMode.ICallback.OnPrepareActionMode(ActionMode? mode, IMenu? menu)
        {
            if (mode == null || menu == null)
                return false;

            var numSelectedItems = RoutesAdapter.SelectionCount;
            if (numSelectedItems > 0)
            {
                mode.Title = numSelectedItems.ToString();  // this._actionMode is null here
            }

            menu.FindItem(Resource.Id.edit)?.SetVisible(numSelectedItems == 1);
            menu.FindItem(Resource.Id.share)?.SetVisible(numSelectedItems == 1);
            menu.FindItem(Resource.Id.delete)?.SetVisible(numSelectedItems > 0);
            menu.FindItem(Resource.Id.selectAll)?.SetVisible(numSelectedItems > 0);

            return true;
        }


        bool ActionMode.ICallback.OnActionItemClicked(ActionMode? mode, IMenuItem? item)
        {
            var routesAdapter = RoutesAdapter;

            switch (item?.ItemId)
            {
                case Resource.Id.edit:
                    ActionEditRouteProperties(routesAdapter.SelectedIds[0]);
                    return true;
                case Resource.Id.share:
                    ActionShareRoute(routesAdapter.SelectedIds[0]);
                    return true;
                case Resource.Id.delete:
                    ActionDeleteRoutes(routesAdapter.SelectedIds);
                    return true;
                case Resource.Id.selectAll:
                    routesAdapter.SelectAll();
                    return true;
                default:
                    return false;
            }
        }


        void ActionMode.ICallback.OnDestroyActionMode(ActionMode? mode)
        {
            var routesAdapter = RoutesAdapter;
            routesAdapter.DeselectAll();

            this._actionMode = null;
        }


        #endregion ActionMode.ICallback


        private void HandleIncomingIntent(Intent intent)
        {
            Android.Net.Uri? gpxUri = intent.Action switch
            {
                Intent.ActionSend => AndroidX.Core.Content.IntentCompat.GetParcelableExtra(intent, Intent.ExtraStream, Java.Lang.Class.FromType(typeof(Android.Net.Uri))) as Android.Net.Uri,
                Intent.ActionView => intent.Data,
                _ => null  // E.g. started from the launcher
            };
            if (gpxUri == null)
                return;

            ImportGpxFile(gpxUri, intent.Type);
        }


        /// <summary>
        /// Import a GPX file that another app sent, or that the user chose with the file picker.
        /// </summary>
        /// <param name="gpxUri">File URI</param>
        /// <param name="mimeType">The file's MIME type, if the sender says what it is</param>
        private void ImportGpxFile(Android.Net.Uri gpxUri, string? mimeType)
        {
            // Unless the sender says it's GPX, check the file name. Other files can arrive because the intent filters
            // accept application/octet-stream, and the file picker accepts any type.
            if (mimeType != AppSettings.GpxMimeType)
            {
                var fileName = GetDisplayName(gpxUri);
                if (fileName != null && !fileName.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase))
                {
                    var adb = new AlertDialog.Builder(this, Resource.Style.AppTheme_Dialog_Alert);
                    adb.SetTitle(Resource.String.error);
                    adb.SetMessage(this.GetFormattedString(Resource.String.notGpxFileMsg, fileName));
                    adb.SetPositiveButton(Android.Resource.String.Ok, (IDialogInterfaceOnClickListener?)null);
                    adb.Show();
                    return;
                }
            }

            ImportRoute(gpxUri);
        }


        /// <summary>
        /// Get the name of a file that another app sent, or that the user chose.
        /// </summary>
        /// <param name="uri">File URI</param>
        /// <returns>File name, or null if it can't be determined</returns>
        private string? GetDisplayName(Android.Net.Uri uri)
        {
            try
            {
                using (var cursor = ContentResolver?.Query(uri, new[] { Android.Provider.IOpenableColumns.DisplayName }, null, null, null))
                {
                    if (cursor != null && cursor.MoveToFirst() && !cursor.IsNull(0))
                        return cursor.GetString(0);
                }
            }
            catch (Exception ex)
            {
                // Some providers don't support the query. Fall back to the URI.
                Log.Warn("LOC", $"Can't get display name for {uri}: {ex.Message}");
            }
            return uri.Scheme == "file" ? uri.LastPathSegment : null;
        }


        private void ImportRoute(Android.Net.Uri gpxUri)
        {
            DataLib.Route route;
            try
            {
                // Opening the stream can fail too, e.g. if the sending app has revoked access.
                using (var stm = ContentResolver?.OpenInputStream(gpxUri) ?? throw new IOException($"Could not open {gpxUri}"))
                {
                    route = RouteConverter.FromGpx(this, stm);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("LOC", $"Route import failed: {ex}");
                var adb2 = new AlertDialog.Builder(this, Resource.Style.AppTheme_Dialog_Alert);
                adb2.SetTitle(Resource.String.error);
                adb2.SetMessage(Resource.String.routeImportErrorMsg);
                adb2.SetPositiveButton(Android.Resource.String.Ok, (IDialogInterfaceOnClickListener?)null);
                adb2.Show();
                return;
            }

            var adb = new AlertDialog.Builder(this, Resource.Style.AppTheme_Dialog_Alert);
            adb.SetTitle(Resource.String.importTitle);
            adb.SetMessage(this.GetFormattedString(Resource.String.routeImportPrompt, route.Label));
            adb.SetPositiveButton(Android.Resource.String.Ok, (o, e) =>
            {
                var mgr = RouteManagerFactory.Create(this);
                mgr.CreateRoute(route);

                RefreshRoutesList();

                var msg = this.GetFormattedString(Resource.String.newRouteAddedMsg, route.Label);
                Toast.MakeText(this, msg, ToastLength.Short)?.Show();
            });
            adb.SetNegativeButton(Android.Resource.String.Cancel, (IDialogInterfaceOnClickListener?)null);
            adb.Show();
        }
    }
}
