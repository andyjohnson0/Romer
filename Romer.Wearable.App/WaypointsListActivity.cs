using Android.Content;
using Android.Views;
using AndroidX.RecyclerView.Widget;
using Romer.DataLib;
using Romer.GeoLib;
using Romer.CommonLib;


namespace Romer.Wearable.App
{
    /// <summary>
    /// Lists the route's waypoints, with checkboxes to tick them off as they're visited.
    /// </summary>
    [Activity]   // Note: title is provided by recycler view
    public class WaypointsListActivity : Activity
    {
        /// <inheritdoc/>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            // Load the night or day colours theme
            var userSettings = UserSettings.Load(this);
            SetTheme(userSettings.Navigation_NightColoursMode ? Resource.Style.Theme_App_NightColours : Resource.Style.Theme_App);

            //
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.waypoints_list_activity);

            // Initial heading
            this._wpMgr = RouteManagerFactory.Create(this);
            var headingTxt = RequireViewById<TextView>(Resource.Id.headerTxt);
            headingTxt.Text = GetString(Resource.String.waypointsListActivityTitle,
                                                  _wpMgr.GetTickedWaypointsCount(AppSettings.RouteId),
                                                  _wpMgr.GetWaypointsCount(AppSettings.RouteId));
            headingTxt.SetTextColor(userSettings.Navigation_NightColoursMode
                ? new Android.Graphics.Color(AndroidX.Core.Content.ContextCompat.GetColor(this, Resource.Color.nightColorAccent))
                : new Android.Graphics.Color(AndroidX.Core.Content.ContextCompat.GetColor(this, Resource.Color.colorAccent)));

            //
            var rv = RequireViewById<AndroidX.Wear.Widget.WearableRecyclerView>(Resource.Id.waypoints_list);
            rv.HasFixedSize = false;
            rv.SetLayoutManager(new LinearLayoutManager(this));
            var adapter = new WaypointsAdaptor(this, AppSettings.RouteId);
            rv.SetAdapter(adapter);
            adapter.TickChanged += Adapter_TickChanged;
        }


        private RouteManager _wpMgr = null!;  // Set in OnCreate()


        private void Adapter_TickChanged(object? sender, EventArgs e)
        {
            // Update list heading
            var headingTxt = this.RequireViewById<TextView>(Resource.Id.headerTxt);
            headingTxt.Text = GetString(Resource.String.waypointsListActivityTitle,
                                                  this._wpMgr.GetTickedWaypointsCount(AppSettings.RouteId),
                                                  this._wpMgr.GetWaypointsCount(AppSettings.RouteId));
        }


        /// <summary>
        /// RecyclerView adapter for a route's waypoints. Each shows its grid reference, altitude, label and ticked
        /// state. Ticking or unticking a waypoint stores the change.
        /// </summary>
        public class WaypointsAdaptor : RecyclerView.Adapter
        {
            /// <summary>
            /// Constructor. Initialise a WaypointsAdaptor object.
            /// </summary>
            /// <param name="context">Context</param>
            /// <param name="routeId">ID of the route whose waypoints to list</param>
            public WaypointsAdaptor(Context context, int routeId)
            {
                this._context = context;
                this._routeId = routeId;
                this._wpMgr = RouteManagerFactory.Create(this._context);
                this._userSettings = UserSettings.Load(this._context);
            }

            private readonly Context _context;
            private readonly int _routeId;
            private readonly RouteManager _wpMgr;
            private readonly UserSettings _userSettings;

            /// <summary>
            /// Fires after a waypoint has been ticked or unticked, and the change stored.
            /// </summary>
            public event EventHandler<EventArgs>? TickChanged;


            /// <inheritdoc/>
            public override int ItemCount
            {
                get 
                { 
                    return _wpMgr.GetWaypointsCount(this._routeId); 
                }
            }


            /// <inheritdoc/>
            public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
            {
                var v = LayoutInflater.From(parent.Context)!.Inflate(Resource.Layout.waypoint_list_item, parent, false)!;
                return new WaypointsViewHolder(v);
            }


            /// <inheritdoc/>
            public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
            {
                // Reset the view holder's check event as it may have been set before. It's connected again once the
                // check box is set, below, so that setting it doesn't write the waypoint back.
                var vh = (WaypointsViewHolder)holder;
                vh.Reset();

                var waypoint = _wpMgr.GetWaypointAt(this._routeId, position);
                var provider = GridSystemProviderManager.BestInArea(new GeoLib.Location().From(waypoint));

                var txt = vh.ItemView.RequireViewById<TextView>(Resource.Id.gridRef);
                txt.Text = provider.GetGridReference(new GeoLib.Location().From(waypoint), 10);
                txt = vh.ItemView.RequireViewById<TextView>(Resource.Id.altitude);
                if (waypoint.Altitude.HasValue)
                {
                    txt.Text = ValueFormatter.ToString(this._context, (decimal)waypoint.Altitude, LengthUnit.Metre, _userSettings.PreferredAltitudeUnit,
                                                        ValueFormatter.Options.UnitSymbol | ValueFormatter.Options.Compact);
                }
                else
                {
                    txt.Text = string.Empty;
                }
                txt = vh.ItemView.RequireViewById<TextView>(Resource.Id.label);
                txt.Text = waypoint.Label;
                var chk = vh.ItemView.RequireViewById<CheckBox>(Resource.Id.check);
                chk.Checked = waypoint.Ticked;
                vh.TickChanged += WaypointItemViewHolder_TickChanged;
            }


            private void WaypointItemViewHolder_TickChanged(object? sender, WaypointsViewHolder.TickChangedEventArgs args)
            {
                // Update the waypoint
                var wp = this._wpMgr.GetWaypointAt(this._routeId, args.Position);
                wp.Ticked = args.Ticked;
                _wpMgr.UpdateWaypoint(wp);

                this.TickChanged?.Invoke(this, EventArgs.Empty);
            }
        }


        /// <summary>
        /// View holder for a waypoint in the list.
        /// </summary>
        public class WaypointsViewHolder : RecyclerView.ViewHolder
        {
            /// <summary>
            /// Constructor. Initialise a WaypointsViewHolder object.
            /// </summary>
            /// <param name="view">Item view, containing a checkbox with the ID check</param>
            public WaypointsViewHolder(View view)
                : base(view)
            {
                this.ItemView = view;

                this.ItemView.RequireViewById<CheckBox>(Resource.Id.check).CheckedChange += Cb_CheckedChange;
            }



            /// <summary>
            /// Tick changed event arguments
            /// </summary>
            public class TickChangedEventArgs : EventArgs
            {
                /// <summary>Item position</summary>
                public int Position { get; set; }

                /// <summary>True if the waypoint is now ticked</summary>
                public bool Ticked { get; set; }
            }


            /// <summary>
            /// Fires when the waypoint's checkbox is ticked or unticked.
            /// </summary>
            public event EventHandler<TickChangedEventArgs>? TickChanged;


            /// <summary>
            /// Remove all <see cref="TickChanged"/> handlers, so that the view holder can be bound to another item, or
            /// its checkbox set, without raising the event.
            /// </summary>
            public void Reset()
            {
                this.TickChanged = null;
            }


            private void Cb_CheckedChange(object? sender, CompoundButton.CheckedChangeEventArgs e)
            {
                var ea = new TickChangedEventArgs()
                {
                    Position = this.BindingAdapterPosition,
                    Ticked = e.IsChecked
                };
                this.TickChanged?.Invoke(sender, ea);
            }
        }
    }
}
