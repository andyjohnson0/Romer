using System;
using System.IO;
using System.Text;
using System.Linq;

using Android;
using Android.Content.PM;
using Android.App;
using Android.Widget;
using Android.Views;
using Android.OS;
using Android.Runtime;
using Android.Locations;
using Android.Content;
using Android.Content.Res;
using Android.Gms.Wearable;
using Android.Util;
using Location = Android.Locations.Location;

using AndroidX.Core.App;

using Romer.Wearable.App.Preferences;
using Romer.GeoLib;
using Romer.CommonLib;
using LocationManager = Romer.CommonLib.LocationManager;
using Romer.DataLib;
using Kilsten.Android;
using Kilsten.WearOS.Controls;



namespace Romer.Wearable.App
{
    /// <summary>
    /// The watch app's main screen: the current location as a grid reference, with its altitude and age, and a
    /// compass. Its menu opens the route and the settings.
    /// </summary>
    [Activity(Label = "@string/appName", MainLauncher = true)]
    public class MainActivity : AndroidX.Activity.ComponentActivity
    {
        // Instance state persist keys
        private const string lastKnownLocationKey = "LastKnownLocation";
        private const string lastKnownLocationAltitudeSourceKey = "LastKnownLocationAltitudeSource";
        private const string gridSystemProviderTagKey = "GridSystemProviderTag";
#if DEBUG
        private const string isDemoModeKey = "IsDemoMode";
#endif

        // Instance state
        private Location? _lastKnownLocation;
        private AltitudeSource _lastKnownLocationAltitudeSource;
        private string? _gridSystemProviderTag;
#if DEBUG
        private bool _isDemoMode = false;
#endif

        // Other state
        private CompassManager _compassManager = null!;  // Set in OnCreate()
        private System.Threading.Timer? _refreshTimer;
        private const int refreshTimerPeriodSec = 60;
        private bool _isNightColoursTheme;  // The theme that OnCreate() applied

        // Activity result launchers, registered in OnCreate()
        private AndroidX.Activity.Result.ActivityResultLauncher _settingsLauncher = null!;
        private AndroidX.Activity.Result.ActivityResultLauncher _enableGpsLauncher = null!;
        private AndroidX.Activity.Result.ActivityResultLauncher _selectGridLauncher = null!;
        private AndroidX.Activity.Result.ActivityResultLauncher _notificationPermissionLauncher = null!;
        private AndroidX.Activity.Result.ActivityResultLauncher _locationPermissionLauncher = null!;


        /// <inheritdoc/>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            // Handle the splash screen transition.
            var splashScreen = AndroidX.Core.SplashScreen.SplashScreen.InstallSplashScreen(this);

            // Load the night or day colours theme
            var userSettings = UserSettings.Load(this);
            this._isNightColoursTheme = userSettings.Navigation_NightColoursMode;
            SetTheme(this._isNightColoursTheme ? Resource.Style.Theme_App_NightColours : Resource.Style.Theme_App);

            //
            base.OnCreate(savedInstanceState);
            RegisterActivityResultLaunchers();
            SetContentView(Resource.Layout.main_activity);
#if DEBUG
            var contentView = ((ViewGroup)this.RequireViewById<View>(Android.Resource.Id.Content)).GetChildAt(0)!;
            contentView.KeepScreenOn = true;
#endif

            //
            RequireViewById<View>(Resource.Id.locationRow).Click += Location_Click;
            RequireViewById<ImageButton>(Resource.Id.refreshBtn).Click += RefreshBtn_Click;
#if DEBUG
            RequireViewById<ImageButton>(Resource.Id.refreshBtn).LongClick += ToggleDemoMode_Click;
#endif
            RequireViewById<ImageButton>(Resource.Id.menuBtn).Click += MenuBtn_Click;

            //
            RequireViewById<TextView>(Resource.Id.timeTv).Text = "";
            RequireViewById<TextView>(Resource.Id.locationTv).SetText(Resource.String.noLocationMsg);
            RequireViewById<TextView>(Resource.Id.altitudeTv).Text = "";

            // The type of north is shown below the compass bearing, at half its size
            var bearingTv = RequireViewById<TextView>(Resource.Id.compassBearingTv);
            RequireViewById<TextView>(Resource.Id.compassNorthTypeTv).SetTextSize(ComplexUnitType.Px, bearingTv.TextSize * 0.5f);

            // Create default route, if it doesn't already exist
            var mgr = RouteManagerFactory.Create(this);
            mgr.GetOrCreateRoute(AppSettings.RouteId, overwrite: false);

            // Sensor check
            this._compassManager = new CompassManager(this);
            this._compassManager.CompassBearing += CompassManager_CompassBearing;
            this._compassManager.CompassAccuracy += CompassManager_CompassAccuracy;
            if (!LocationManager.HasGps(this))
            {
                userSettings.Advanced_LocationSource = LocationSource.Fused;
                userSettings.Save(this);

                var adb = new WearableAlertDialog.Builder(this);
                adb.SetTitle(Resource.String.gps);
                adb.SetMessage(Resource.String.gpsNotDetectedMsg);
                adb.SetPositiveButton();
                adb.Create().Show();
            }
            if (!CompassManager.HasMagnetometer(this))
            {
                userSettings.Navigation_CompassBezelMode = CompassView.BezelModes.None;
                userSettings.Save(this);

                var adb = new WearableAlertDialog.Builder(this);
                adb.SetTitle(Resource.String.compass);
                adb.SetMessage(Resource.String.compassNotDetectedMsg);
                adb.SetPositiveButton();
                adb.Create().Show();
            }
            SetCompassBezelMode(userSettings.Navigation_CompassBezelMode);

            if (savedInstanceState != null)
            {
                this._lastKnownLocation = AndroidX.Core.OS.BundleCompat.GetParcelable(savedInstanceState, lastKnownLocationKey, Java.Lang.Class.FromType(typeof(Android.Locations.Location))) as Android.Locations.Location;
                this._lastKnownLocationAltitudeSource = (AltitudeSource)savedInstanceState.GetInt(lastKnownLocationAltitudeSourceKey);
                this._gridSystemProviderTag = savedInstanceState.GetString(gridSystemProviderTagKey);
#if DEBUG
                this._isDemoMode = savedInstanceState.GetBoolean(isDemoModeKey);
#endif
            }
            else
            {
                // Ask for permission to post notifications, which are shown when a route arrives from the phone.
                // Only one permission request can be shown at a time, so the startup location fetch (which may
                // request location permission) waits until this one completes.
                if (OperatingSystem.IsAndroidVersionAtLeast(33) &&
                    CheckSelfPermission(Manifest.Permission.PostNotifications) != Permission.Granted)
                {
                    _notificationPermissionLauncher.Launch(Manifest.Permission.PostNotifications);
                }
                else if (userSettings.Advanced_LocationFetchOnStartup)
                {
                    GetAndDisplayLocation();
                }
            }
        }


        /// <inheritdoc/>
        protected override void OnPause()
        {
            base.OnPause();

            // Stop the refresh timer
            StopRefreshTimer();
        }


        /// <inheritdoc/>
        protected override void OnResume()
        {
            base.OnResume();

            // Show location. Don't open the grid list if no grid is chosen: this runs on returning from the grid list
            // too, and reopening it would stop the user backing out of it.
            DisplayLastKnownLocation(chooseGridIfNone: false);

            // Start the refresh timer
            StartRefreshTimer();
        }


        /// <inheritdoc/>
        protected override void OnStop()
        {
            base.OnStop();

            // Pause compass
            SetCompassBezelMode(CompassView.BezelModes.None);
        }


        /// <inheritdoc/>
        protected override void OnStart()
        {
            base.OnStart();

            // Resume compass
            var settings = UserSettings.Load(this);
            SetCompassBezelMode(settings.Navigation_CompassBezelMode);
        }


        /// <inheritdoc/>
        protected override void OnSaveInstanceState(Bundle saveState)
        {
            if (this._lastKnownLocation != null)
                saveState.PutParcelable(lastKnownLocationKey, this._lastKnownLocation);
            saveState.PutInt(lastKnownLocationAltitudeSourceKey, (int)this._lastKnownLocationAltitudeSource);
            if (this._gridSystemProviderTag != null)
                saveState.PutString(gridSystemProviderTagKey, this._gridSystemProviderTag);
#if DEBUG
            saveState.PutBoolean(isDemoModeKey, this._isDemoMode);
#endif

            base.OnSaveInstanceState(saveState);
        }


        /// <inheritdoc/>
        protected override void OnRestoreInstanceState(Bundle savedState)
        {
            base.OnRestoreInstanceState(savedState);

            this._lastKnownLocation = AndroidX.Core.OS.BundleCompat.GetParcelable(savedState, lastKnownLocationKey, Java.Lang.Class.FromType(typeof(Android.Locations.Location))) as Android.Locations.Location;
            this._lastKnownLocationAltitudeSource = (AltitudeSource)savedState.GetInt(lastKnownLocationAltitudeSourceKey);
            this._gridSystemProviderTag = savedState.GetString(gridSystemProviderTagKey);
#if DEBUG
            this._isDemoMode = savedState.GetBoolean(isDemoModeKey);
#endif
        }


        /// <summary>
        /// Register the callbacks for the activities and permission requests that this activity launches.
        /// Called from OnCreate(), so that results delivered after the activity is recreated reach the callbacks.
        /// </summary>
        private void RegisterActivityResultLaunchers()
        {
            // Returning from PreferencesActivity
            _settingsLauncher = this.RegisterForActivityResult(result =>
            {
                var userSettings = UserSettings.Load(this);
                if (userSettings.Navigation_NightColoursMode != this._isNightColoursTheme)
                {
                    this.Recreate();  // OnCreate() applies the new theme
                    return;
                }
                SetCompassBezelMode(userSettings.Navigation_CompassBezelMode);
            });

            // Returning from the system's location settings
            _enableGpsLauncher = this.RegisterForActivityResult(result =>
            {
                if (LocationManager.IsGpsEnabled(this))
                {
                    Toast.MakeText(this, Resource.String.enableGpsSucceededMsg, ToastLength.Short)?.Show();
                }
                GetAndDisplayLocation();
            });

            // Returning from SelectGridActivity
            _selectGridLauncher = this.RegisterForActivityResult(result =>
            {
                var tag = SelectGridActivity.ReadResult(result);
                if (tag != null)
                {
                    this._gridSystemProviderTag = tag;
                    if (this._lastKnownLocation != null)
                        DisplayLastKnownLocation();
                    else
                        GetAndDisplayLocation();
                }
            });

            // Notification permission, requested in OnCreate()
            _notificationPermissionLauncher = this.RegisterForPermissionResult(granted =>
            {
                // Granted or not, continue with the startup location fetch that OnCreate() deferred.
                if (UserSettings.Load(this).Advanced_LocationFetchOnStartup)
                {
                    GetAndDisplayLocation();
                }
            });

            // Location permission, requested by CheckHasLocationPermissions()
            _locationPermissionLauncher = this.RegisterForPermissionResult(LocationPermissionResult);
        }



        #region Location display

        private void Location_Click(object? sender, EventArgs e)
        {
            if (this._lastKnownLocation != null)
            {
                // Enable user to select a grid system
                _selectGridLauncher.Launch(SelectGridActivity.CreateIntent(this, this._lastKnownLocation));
            }
        }


        private void RefreshBtn_Click(object? sender, EventArgs e)
        {
            StopRefreshTimer();
            GetAndDisplayLocation();
            StartRefreshTimer();
        }


        private void GetAndDisplayLocation()
        {
#if DEBUG
            if (_isDemoMode)
            {
                DisplayLastKnownLocation();
                return;
            }
#endif

            // Check permissions.
            if (!CheckHasLocationPermissions())
            {
                return;
            }

            // If using GPS, check it is enabled and attempt to enable if necessary.
            var settings = UserSettings.Load(this);
            if (settings.Advanced_LocationSource == LocationSource.BuiltInGps)
            {
                if (!LocationManager.IsGpsEnabled(this))
                {
                    var adb = new WearableAlertDialog.Builder(this);
                    adb.SetTitle(Resource.String.enableGpsTitle);
                    adb.SetMessage(Resource.String.enableGpsPrompt);
                    adb.SetPositiveButton(handler: (o, e) =>
                        {
                            _enableGpsLauncher.Launch(new Intent(Android.Provider.Settings.ActionLocationSourceSettings));
                        });
                    adb.SetNegativeButton();
                    adb.Create().Show();
                    return;
                }
            }

            ResolveAndDisplayLocation(settings);
        }


        // Non-null while a location fetch is in progress
        private CancellationTokenSource? _locationFetchCts;


        private async void ResolveAndDisplayLocation(UserSettings settings)
        {
            if (this._locationFetchCts != null)
                return;  // Already fetching
            using var cts = new CancellationTokenSource();
            this._locationFetchCts = cts;

            // Display progress dialog. Its cancel button, or the back button, cancels the fetch.
            var tdb = new WearableTaskDialog.Builder(this);
            tdb.SetCancelButton(handler: (o, e) => cts.Cancel(), delaySecs: 3);
            tdb.SetKeepScreenOn(settings.Advanced_KeepScreenOnDuringLocationFetch);
            using (var ta = this.ObtainStyledAttributes(new int[] { Resource.Attribute.colorAccent, Resource.Attribute.buttonTint, Android.Resource.Attribute.TextColorPrimary }))
            {
                tdb.SetCancelButtonBackgroundTint(ta.GetColor(0, -1));
                tdb.SetCancelButtonForegroundTint(ta.GetColor(1, -1));
                tdb.SetProgressBarTint(ta.GetColor(2, -1));
                ta.Recycle();
            }
            var dlg = tdb.Create();
            dlg.Show();

            int? failureMsgResId = null;
            try
            {
                var resolved = await LocationResolver.ResolveCurrentLocationAsync(this, settings.Advanced_LocationFixTimeout, settings.Advanced_LocationSource, cts.Token);
                this._lastKnownLocation = resolved.Location;
                this._lastKnownLocationAltitudeSource = resolved.AltitudeSource;
            }
            catch (System.OperationCanceledException)
            {
                failureMsgResId = Resource.String.operationCancelledMsg;
            }
            catch (TimeoutException)
            {
                failureMsgResId = Resource.String.operationTimedOutMsg;
            }
            catch (Exception ex)
            {
                Log.Warn("LOC", $"Location fetch failed: {ex}");
                failureMsgResId = Resource.String.locateFailedMsg;
            }
            finally
            {
                this._locationFetchCts = null;
            }

            if (this.IsDestroyed)
                return;
            if (dlg.IsShowing)
                dlg.Dismiss();
            DisplayLastKnownLocation();
            if (failureMsgResId.HasValue)
            {
                Toast.MakeText(this, failureMsgResId.Value, ToastLength.Short)?.Show();
            }
            else if (this._lastKnownLocation != null)
            {
                // Check for waypoints near the new location. Done only for new fixes, not when the display refreshes,
                // so that the prompt isn't repeated every minute.
                DoAdjacentWaypointCheck(new GeoLib.Location().From(this._lastKnownLocation));
            }
        }



        /// <summary>
        /// Display the last known location.
        /// </summary>
        /// <param name="chooseGridIfNone">
        /// If no grid has been chosen, open the grid list. False for the periodic refresh, so that the list doesn't
        /// keep reopening after the user has backed out of it.
        /// </param>
        private void DisplayLastKnownLocation(bool chooseGridIfNone = true)
        {
            var userSettings = UserSettings.Load(this);

            if (this._lastKnownLocation != null)
            {
                if (this._gridSystemProviderTag == null)
                {
                    if (chooseGridIfNone)
                    {
                        // No current provider. Start the SelectGridActivity activity to allow user to choose one.
                        _selectGridLauncher.Launch(SelectGridActivity.CreateIntent(this, this._lastKnownLocation));
                    }

                    // Until a grid is chosen, say so. Tapping the message opens the grid list (Location_Click).
                    RequireViewById<TextView>(Resource.Id.timeTv).Text = ToTimeDiffDescription(this, ToFixTimeUtc(this._lastKnownLocation));
                    RequireViewById<TextView>(Resource.Id.locationTv).SetText(Resource.String.chooseGridMsg);
                    RequireViewById<TextView>(Resource.Id.locationAccTv).Text = "";
                    RequireViewById<TextView>(Resource.Id.altitudeTv).Text = "";
                    RequireViewById<TextView>(Resource.Id.altitudeAccTv).Text = "";
                    return;
                }
                var provider = GridSystemProviderManager.FindByTag(this._gridSystemProviderTag);

                // Time
                RequireViewById<TextView>(Resource.Id.timeTv).Text = ToTimeDiffDescription(this, ToFixTimeUtc(this._lastKnownLocation));

                // Location and altitude
                RequireViewById<TextView>(Resource.Id.locationAccTv).Text = "";
                RequireViewById<TextView>(Resource.Id.altitudeTv).Text = "";
                RequireViewById<TextView>(Resource.Id.altitudeAccTv).Text = "";
                var geoLocation = new GeoLib.Location().From(this._lastKnownLocation);
                if (provider != null && provider.IsInArea(geoLocation))
                {
                    // Location and optional accuracy
                    RequireViewById<TextView>(Resource.Id.locationTv).Text = provider.GetGridReference(geoLocation, userSettings.Navigation_GridReferencePrecision);
                    if (this._lastKnownLocation.HasAccuracy)
                    {
                        var str = ValueFormatter.ToString(this, (decimal)this._lastKnownLocation.Accuracy, LengthUnit.Metre, userSettings.PreferredDistanceUnit,
                                                          ValueFormatter.Options.Default | ValueFormatter.Options.Compact);
                        RequireViewById<TextView>(Resource.Id.locationAccTv).Text = " ±" + str;
                    }

                    // Altitude and optional accuracy
                    if (this._lastKnownLocation.HasAltitude && provider != null && provider.IsInArea(geoLocation))
                    {
                        var str = ValueFormatter.ToString(this, (decimal)this._lastKnownLocation.Altitude, LengthUnit.Metre, userSettings.PreferredAltitudeUnit,
                                                          ValueFormatter.Options.Default | ValueFormatter.Options.Compact);
                        RequireViewById<TextView>(Resource.Id.altitudeTv).Text = str;
                        if (this._lastKnownLocation.HasVerticalAccuracy)
                        {
                            str = ValueFormatter.ToString(this, (decimal)this._lastKnownLocation.VerticalAccuracyMeters, LengthUnit.Metre, userSettings.PreferredAltitudeUnit,
                                                          ValueFormatter.Options.Default | ValueFormatter.Options.Compact);
                            RequireViewById<TextView>(Resource.Id.altitudeAccTv).Text = " ±" + str;
                        }
                    }
                }
                else
                {
                    // Out of grid area
                    RequireViewById<TextView>(Resource.Id.locationTv).Text = GetString(Resource.String.notInGridArea);
                }

            }
            else
            {
                // No location
                RequireViewById<TextView>(Resource.Id.timeTv).Text = "";
                RequireViewById<TextView>(Resource.Id.locationTv).SetText(Resource.String.noLocationMsg);
                RequireViewById<TextView>(Resource.Id.locationAccTv).Text = "";
                RequireViewById<TextView>(Resource.Id.altitudeTv).Text = "";
            }
        }


        private void DoAdjacentWaypointCheck(GeoLib.Location currentLocation)
        {
            const double adjacentDistKm = 0.05;  // 50M

            // Check for adjacent waypoints
            var wpMgr = RouteManagerFactory.Create(this);
            var route = wpMgr.GetRoute(AppSettings.RouteId);
            if (route == null)
                return;
            var adjacentWaypoints = new List<Waypoint>();
            foreach(var wp in route.Waypoints.Where(wp => !wp.Ticked))
            {
                var wpLocation = new GeoLib.Location().From(wp);
                var distKm = GeoLib.Utils.HaversineKm(currentLocation, wpLocation);
                if (distKm <= adjacentDistKm)
                {
                    adjacentWaypoints.Add(wp);
                }
            }

            if (adjacentWaypoints.Count > 0)
            {
                var adb = new WearableAlertDialog.Builder(this);
                adb.SetTitle(Resource.String.adjacentWaypointsFoundTitle);
                var msg = this.GetFormattedString(Resource.String.adjacentWaypointsFoundPrompt,
                                              this.RequireResources().GetQuantityString(Resource.Plurals.waypointCount, adjacentWaypoints.Count, adjacentWaypoints.Count),
                                              this.RequireResources().GetQuantityString(Resource.Plurals.are, adjacentWaypoints.Count),
                                              this.RequireResources().GetQuantityString(Resource.Plurals.them, adjacentWaypoints.Count));
                adb.SetMessage(msg);
                adb.SetPositiveButton(handler: (o, e) =>
                {
                    // Mark waypoints as visited
                    foreach (var wp in adjacentWaypoints)
                    {
                        wp.Ticked = true;
                        wpMgr.UpdateWaypoint(wp);
                    }
                    var msg = this.GetFormattedString(Resource.String.adjacentWaypointsVisitedMsg,
                                                  this.RequireResources().GetQuantityString(Resource.Plurals.waypointCount, adjacentWaypoints.Count, adjacentWaypoints.Count));
                    Toast.MakeText(this, msg, ToastLength.Short)?.Show();
                });
                adb.SetNegativeButton();
                adb.Create().Show();
            }
        }



        /// <summary>
        /// Get the time of a location fix.
        /// </summary>
        /// <param name="self">Location</param>
        /// <returns>Fix time (UTC)</returns>
        public static DateTime ToFixTimeUtc(
            Android.Locations.Location self)
        {
            return DateTime.UnixEpoch.AddMilliseconds((double)self.Time);
        }


        private static string ToTimeDiffDescription(
            Context context,
            DateTime dt)
        {
            if (dt.Kind != DateTimeKind.Utc)
                throw new ArgumentException($"Incorrect date/time kind: {dt.Kind}", nameof(dt));

            var now = DateTime.UtcNow;
            var delta = (dt <= now) ? now - dt : TimeSpan.Zero;
            if (delta.TotalSeconds < 20)
            {
                return context.GetString(Resource.String.justNowMsg);
            }
            else if (delta.TotalSeconds < 60)
            {
                var secs = (int)delta.TotalSeconds;
                return context.RequireResources().GetQuantityString(Resource.Plurals.secondsAgoCount, secs, secs);
            }
            else if (delta.TotalMinutes < 60)
            {
                var mins = (int)delta.TotalMinutes;
                return context.RequireResources().GetQuantityString(Resource.Plurals.minutesAgoCount, mins, mins);
            }
            else if (delta.TotalHours < 24)
            {
                var hours = (int)delta.TotalHours;
                return context.RequireResources().GetQuantityString(Resource.Plurals.hoursAgoCount, hours, hours);
            }
            else
            {
                var days = (int)delta.TotalDays;
                return context.RequireResources().GetQuantityString(Resource.Plurals.daysAgoCount, days, days);
            }
        }


        private void StartRefreshTimer()
        {
            _refreshTimer = new System.Threading.Timer((o) =>
            {
                this.RunOnUiThread(() =>
                {
                    DisplayLastKnownLocation(chooseGridIfNone: false);
                });
            },
            null, TimeSpan.FromSeconds(refreshTimerPeriodSec), TimeSpan.FromSeconds(refreshTimerPeriodSec));
        }


        private void StopRefreshTimer()
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                _refreshTimer.Dispose();
                _refreshTimer = null;
            }
        }


        #endregion Location display


        #region Permissions

        /// <summary>
        /// Check location permissions, and request them if they haven't been granted.
        /// </summary>
        /// <returns>
        /// true if permissions have all been granted, otherwise false. If false, a request has been started, and
        /// <see cref="LocationPermissionResult"/> receives the user's response.
        /// </returns>
        private bool CheckHasLocationPermissions()
        {
            if (this.CheckSelfPermission(Manifest.Permission.AccessFineLocation) != Permission.Granted)
            {
                if (this.ShouldShowRequestPermissionRationale(Manifest.Permission.AccessFineLocation))
                {
                    // Explain to the user why we want the permission.
                    var adb = new WearableAlertDialog.Builder(this);
                    adb.SetCancelable(true);
                    adb.SetTitle(Resource.String.locationPermissionPrompt);
                    adb.SetMessage(Resource.String.locationPermissionAskMsg);
                    adb.SetPositiveButton(handler: (sender, args) =>
                    {
                        _locationPermissionLauncher.Launch(Manifest.Permission.AccessFineLocation);
                    });
                    adb.SetNegativeButton();
                    adb.Create().Show();
                    return false;
                }
                else
                {
                    // No need to explain. Just acquire the premission.
                    // System uses its own UI and calls LocationPermissionResult() with grant/deny.
                    _locationPermissionLauncher.Launch(Manifest.Permission.AccessFineLocation);
                    return false;
                }
            }

            return true;
        }


        /// <summary>
        /// Handle the user's response to a location permission request.
        /// </summary>
        /// <param name="granted">true if the permission was granted</param>
        private void LocationPermissionResult(bool granted)
        {
            if (granted)
            {
                GetAndDisplayLocation();
                return;
            }
            else
            {
                if (this.ShouldShowRequestPermissionRationale(Manifest.Permission.AccessFineLocation))
                {
                    // Permission not granted. Explain to the user why we want the permission.
                    var adb = new WearableAlertDialog.Builder(this);
                    adb.SetCancelable(true);
                    adb.SetTitle(Resource.String.locationPermissionPrompt);
                    adb.SetMessage(Resource.String.locationPermissionExplainMsg);
                    adb.SetPositiveButton();
                    adb.Create().Show();
                    return;
                }
                else
                {
                    // Permission denied permenantly.
                    // Prompt the user to enable it in the app settings - see https://stackoverflow.com/a/51514740/67316
                    var adb = new WearableAlertDialog.Builder(this);
                    adb.SetCancelable(true);
                    adb.SetTitle(Resource.String.locationPermissionPrompt);
                    adb.SetMessage(Resource.String.locationPermissionDeniedMsg);
                    adb.SetPositiveButton(handler: (sender, args) =>
                    {
                        var intent = new Intent(Android.Provider.Settings.ActionApplicationDetailsSettings,
                                                 Android.Net.Uri.Parse("package:" + this.PackageName));
                        StartActivity(intent);
                    });
                    adb.SetNegativeButton();
                    adb.Create().Show();
                    return;
                }
            }
        }

        #endregion Permissions


        #region Menu

        private void MenuBtn_Click(object? sender, EventArgs e)
        {
            var settings = UserSettings.Load(this);

            var dialogView = LayoutInflater.From(this)!.Inflate(Resource.Layout.main_menu_dialog, null)!;
            var theme = settings.Navigation_NightColoursMode ? Resource.Style.Theme_App_ContextDialog_NightColours : Resource.Style.Theme_App_ContextDialog;
            var contextDlg = new AndroidX.AppCompat.App.AlertDialog.Builder(this, theme)
                .SetView(dialogView)!
                .Create()!;
            var btn = dialogView.RequireViewById<ImageButton>(Resource.Id.routeBtn);
            btn.Click += (o, e) =>
            {
                contextDlg.Dismiss();

                // Start R|outeActivity
                var intent = new Intent(this, typeof(RouteActivity));
                if (_lastKnownLocation != null)
                    intent.PutExtra(RouteActivity.CurrentLocationKey, _lastKnownLocation);
                StartActivity(intent);                
            };
            btn.LongClick += (o, e) =>
            {
                contextDlg.Dismiss();

                CreateWaypointForCurrentLocation();
            };
            btn = dialogView.RequireViewById<ImageButton>(Resource.Id.settingsBtn);
            btn.Click += (o, e) =>
            {
                contextDlg.Dismiss();

                // Start PreferencesActivity
                _settingsLauncher.Launch(new Intent(this, typeof(PreferencesActivity)));
            };

            contextDlg.Show();
        }


        private void CreateWaypointForCurrentLocation()
        {
            if (this._lastKnownLocation != null)
            {
                var gsp = new LatLonGridSystemProvider() as IGridSystemProvider;
                var waypoint = new Waypoint().From(this._lastKnownLocation);
                waypoint.Label = gsp.GetGridReference(new GeoLib.Location().From(this._lastKnownLocation), 100);

                var wpMgr = RouteManagerFactory.Create(this);
                var route = wpMgr.GetOrCreateRoute(AppSettings.RouteId, overwrite: false, label: GetString(Resource.String.newRouteName));
                route.Add(waypoint);
                wpMgr.UpdateRoute(route);
                Toast.MakeText(this, Resource.String.waypointAddedMsg, ToastLength.Short)?.Show();
            }
            else
            {
                Toast.MakeText(this, Resource.String.noLocationForWaypointMsg, ToastLength.Long)?.Show();
            }
        }

        #endregion Menu







        #region Compass

        /// <summary>
        /// Set compass bezel mode.
        /// If mode is BezelModes.None then disable the compass, otherwise enable it.
        /// </summary>
        /// <param name="bezelMode">Bezel mode</param>
        private void SetCompassBezelMode(CompassView.BezelModes bezelMode)
        {
            var compassView = RequireViewById<CompassView>(Resource.Id.compassVw);

            if (bezelMode != CompassView.BezelModes.None)
            {
                compassView.Visibility = ViewStates.Visible;
                compassView.BezelMode = bezelMode;
                this._compassManager.SetCompassEnabled(true);
                Log.Debug("LOC", $"{DateTime.Now} Compass enabled");
            }
            else
            {
                compassView.Visibility = ViewStates.Invisible;
                compassView.BezelMode = bezelMode;
                this._compassManager.SetCompassEnabled(false);
                Log.Debug("LOC", $"{DateTime.Now} Compass disabled");
            }

            // Numeric bearing, shown with the compass
            RequireViewById<View>(Resource.Id.compassBearingLayout).Visibility = bezelMode != CompassView.BezelModes.None ? ViewStates.Visible : ViewStates.Invisible;
        }


        private void CompassManager_CompassBearing(object? sender, CompassBearingEventArgs e)
        {
            Log.Debug("LOC", $"{DateTime.Now} Compass bearing: {e.Bearing}");

            var compassView = RequireViewById<CompassView>(Resource.Id.compassVw);
            compassView.SetOrientation(e.Bearing);

            var bearingTv = RequireViewById<TextView>(Resource.Id.compassBearingTv);
            bearingTv.Text = this.GetFormattedString(Resource.String.compassBearing, e.Bearing.ToString());  // Magnetic bearing
        }


        // Opacity of the compass while its accuracy is low
        private const float lowAccuracyCompassAlpha = 0.35f;

        // Whether the calibration hint has been shown since the activity was created
        private bool _calibrationHintShown = false;


        private void CompassManager_CompassAccuracy(object? sender, CompassAccuracyEventArgs e)
        {
            // Dim the compass while its accuracy is low, rather than turning it off. It still points roughly the right
            // way, and it brightens again by itself when the sensor recovers, e.g. after a figure-of-eight movement.
            var isLowAccuracy = e.Accuracy == Android.Hardware.SensorStatus.AccuracyLow || e.Accuracy == Android.Hardware.SensorStatus.Unreliable;
            RequireViewById<CompassView>(Resource.Id.compassVw).Alpha = isLowAccuracy ? lowAccuracyCompassAlpha : 1f;
            RequireViewById<View>(Resource.Id.compassBearingLayout).Alpha = isLowAccuracy ? lowAccuracyCompassAlpha : 1f;

            // Say how to fix it, once
            if (isLowAccuracy && !_calibrationHintShown)
            {
                _calibrationHintShown = true;
                Toast.MakeText(this, Resource.String.compassAccuracyMsg, ToastLength.Long)?.Show();
            }
        }

#endregion Compass


#if DEBUG

        private void ToggleDemoMode_Click(object? sender, EventArgs e)
        {
            _isDemoMode = !_isDemoMode;
            if (_isDemoMode)
            {
                var mgr = RouteManagerFactory.Create(this);
                var route = mgr.GetOrCreateRoute(AppSettings.RouteId, overwrite: true, label: GetString(Resource.String.newRouteName));
                route = DemoData.CreateRoute();
                route.Id = AppSettings.RouteId;
                mgr.UpdateRoute(route);

                // Set current location
                var waypoint = route.Waypoints.Last();
                this._lastKnownLocation = new Location("").From(waypoint);
                this._lastKnownLocationAltitudeSource = AltitudeManager.HasAltitudeSensor(this) ? AltitudeSource.PressureSensor : AltitudeSource.Gps;
                this._gridSystemProviderTag = GridSystemProviderManager.BestInArea(new GeoLib.Location().From(waypoint)).Tag;
                DisplayLastKnownLocation();
                Toast.MakeText(this, "Entered demo mode", ToastLength.Short)?.Show();
                DumpDatabase(mgr);
            }
            else
            {
                var wpMgr = RouteManagerFactory.Create(this);
                DumpDatabase(wpMgr);
                wpMgr.DeleteAllWaypoints(AppSettings.RouteId);
                this._lastKnownLocation = null;
                this._lastKnownLocationAltitudeSource = AltitudeSource.None;
                this._gridSystemProviderTag = null;
                DisplayLastKnownLocation();
                Toast.MakeText(this, "Exited demo mode", ToastLength.Short)?.Show();
            }
        }


        private void DumpDatabase(RouteManager routeManager)
        {
            Log.Debug("LOC", "Start of database dump ---------------------------------------------");
            var dumpStr = routeManager.Dump();
            using (var rdr = new System.IO.StringReader(dumpStr))
            {
                string? line;
                while ((line = rdr.ReadLine()) != null)
                {
                    Log.Debug("LOC", line);
                }
            }
            Log.Debug("LOC", "End of database dump -----------------------------------------------");
        }

#endif
    }
}
