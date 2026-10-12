using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using AndroidX.RecyclerView.Widget;
using AndroidX.CardView.Widget;

using Romer.DataLib;
using Romer.GeoLib;
using Romer.CommonLib;

using MikePhil.Charting;
using MikePhil.Charting.Charts;
using MikePhil.Charting.Data;
using Kilsten.Android;
using static AndroidX.RecyclerView.Widget.RecyclerView;
using Android.Util;


namespace Romer.Mobile.App
{
    /// <summary>
    /// RecyclerView adapter for the cards on RouteActivity's route info page: a summary, a map, waypoint altitudes
    /// and leg distances. A route with no waypoints has only the summary card.
    /// </summary>
    public class RouteActivityInfoCardListAdapter : RecyclerView.Adapter
    {
        /// <summary>
        /// Constructor. Initialise a RouteActivityInfoCardListAdapter object.
        /// Call <see cref="InitialiseDataSet"/> before the cards are shown.
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="routeId">ID of the route to show</param>
        public RouteActivityInfoCardListAdapter(
            Context context,
            int routeId)
        {
            this._context = context;
            this._routeId = routeId;
        }

        private readonly Context _context;
        private readonly int _routeId;


        // The following are set by InitialiseDataSet()
        private UserSettings _userSettings = new UserSettings();
        private Route _route = new Route();
        private Waypoint[] _waypoints = Array.Empty<Waypoint>();
        private Location[] _locations = Array.Empty<Location>();
        private RouteProperties _routeProps = RouteProperties.CreateFrom(Array.Empty<Location>());

        /// <summary>
        /// Load the route, and calculate the data used by individual cards.
        /// Call this before calling NotifyDataSetChanged(), so that the cards show the current route.
        /// </summary>
        public void InitialiseDataSet()
        {
            this._userSettings = UserSettings.Load(this._context);
            var mgr = RouteManagerFactory.Create(this._context);
            this._route = mgr.GetRoute(this._routeId) ?? new Route();  // Empty if deleted
            this._waypoints = _route.Waypoints.ToArray();
            this._locations = this._waypoints.Select(wp => wp.GetLocation()).ToArray();
            this._routeProps = RouteProperties.CreateFrom(this._locations);
        }


        private const int viewTypeText = 0;
        private const int viewTypeMap = 1;
        private const int viewTypeLineChart = 2;
        private const int viewTypeBarChart = 3;

        /// <summary>
        /// Fires when a waypoint is clicked on the map card.
        /// </summary>
        public event EventHandler<RouteMapView.WaypointClickEventArgs>? RouteMapWaypointClick;


        private class CardViewHolder : RecyclerView.ViewHolder
        {
            public CardViewHolder(View itemView)
                : base(itemView)
            {
            }
        }


        /// <inheritdoc/>
        public override int ItemCount
        {
            get
            {
                return this._waypoints?.Length > 0 ? 4 : 1;
            }
        }


        /// <inheritdoc/>
        public override int GetItemViewType(int position)
        {
            return position switch
            {
                0 => viewTypeText,
                1 => viewTypeMap,
                2 => viewTypeLineChart,
                3 => viewTypeBarChart,
                _ => throw new InvalidOperationException($"Invalid position: {position}")
            }; ;
        }


        /// <inheritdoc/>
        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var inflater = LayoutInflater.From(parent.Context)!;
            View itemView = viewType switch
            {
                viewTypeText => inflater.Inflate(Resource.Layout.route_activity_info_card_text, parent, false)!,
                viewTypeMap => inflater.Inflate(Resource.Layout.route_activity_info_card_map, parent, false)!,
                viewTypeLineChart => inflater.Inflate(Resource.Layout.route_activity_info_card_line_chart, parent, false)!,
                viewTypeBarChart => inflater.Inflate(Resource.Layout.route_activity_info_card_bar_chart, parent, false)!,
                _ => throw new InvalidOperationException($"Invalid view type: {viewType}")
            };
            if (viewType == viewTypeMap)
            {
                InitialiseRouteMapCard(itemView);
            }
            return new CardViewHolder(itemView);
        }


        /// <inheritdoc/>
        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            // Populate the card. Note that cards with graphs need to be done via a Post().
            var viewHolder = (CardViewHolder)holder;
            var card = (CardView)viewHolder.ItemView;
            switch (position)
            {
                case 0:
                    card.Post(() => { PopulateRouteInfoCard(card); });
                    break;
                case 1:
                    card.Post(() => { PopulateRouteMapCard(card); });
                    break;
                case 2:
                    card.Post(() => { PopulateRouteAltitudeCard(card); });
                    break;
                case 3:
                    card.Post(() => { PopulateRouteLegDistanceCard(card); });
                    break;
            }
        }


        private void PopulateRouteInfoCard(CardView card)
        {
            var tv = card.RequireViewById<TextView>(Resource.Id.cardHeading);
            tv.SetText(Resource.String.summaryTitle);
            card.RequireViewById<TextView>(Resource.Id.cardFooter).Text = string.Empty;  // A re-bound card keeps its previous footer

            // Build route summary from waypoints
            if (this._waypoints.Length > 0)
            {
                tv = card.RequireViewById<TextView>(Resource.Id.cardBody);

                var timeStr = _context.GetString(Resource.String.unknown);
                if (_routeProps.TotalWalkTime.HasValue)
                {
                    // Total hours, not the Hours component, which would drop whole days from a walk of 24 hours or more
                    var walkTime = _routeProps.TotalWalkTime.Value;
                    var hours = (int)walkTime.TotalHours;
                    var resources = this._context.RequireResources();
                    timeStr = this._context.GetFormattedString(Resource.String.hoursAndMinutes,
                                                              resources.GetQuantityString(Resource.Plurals.hoursCount, hours, hours),
                                                              resources.GetQuantityString(Resource.Plurals.minutesCount, walkTime.Minutes, walkTime.Minutes));
                }
                var bodyStr = this._context.GetFormattedString(
                    Resource.String.routeProperties,
                    _routeProps.TotalDistance.HasValue
                        ? ValueFormatter.ToString(this._context, _routeProps.TotalDistance.Value, LengthUnit.Kilometre, _userSettings.Navigation_PreferredDistanceUnit)
                        : _context.GetString(Resource.String.unknown),
                    _routeProps.TotalAscent.HasValue
                        ? ValueFormatter.ToString(this._context, _routeProps.TotalAscent.Value, LengthUnit.Metre, _userSettings.Navigation_PreferredAltitudeUnit)
                        : _context.GetString(Resource.String.unknown),
                    _routeProps.TotalDescent.HasValue
                        ? ValueFormatter.ToString(this._context, _routeProps.TotalDescent.Value, LengthUnit.Metre, _userSettings.Navigation_PreferredAltitudeUnit)
                        : _context.GetString(Resource.String.unknown),
                    timeStr);
                tv.Text = bodyStr;
                tv = card.RequireViewById<TextView>(Resource.Id.cardFooter);
                tv.SetText(Resource.String.estimatedValuesWarning);
            }
            else
            {
                // No waypoints
                tv = card.RequireViewById<TextView>(Resource.Id.cardBody);
                tv.SetText(Resource.String.insufficientWaypointsMsg);
            }
        }


        private void PopulateRouteMapCard(CardView card)
        {
            var tv = card.RequireViewById<TextView>(Resource.Id.cardHeading);
            tv.SetText(Resource.String.routeMapTitle);

            var map = card.RequireViewById<RouteMapView>(Resource.Id.chart);
            map.SetRoute(this._route);
        }


        /// <summary>
        /// Connect the map card's event handlers. Called once per card view, not on every bind, so that handlers
        /// don't accumulate when the card is re-bound.
        /// </summary>
        /// <param name="card">Map card view</param>
        private void InitialiseRouteMapCard(View card)
        {
            var map = card.RequireViewById<RouteMapView>(Resource.Id.chart);
            map.WaypointClick += OnRouteMap_WaypointClick;

            var zl = card.RequireViewById<MapZoomLayout>(Resource.Id.zoomLayout);
            zl.LaidOut += (o, e) => { map.ShowFirstWaypoint(); };
            zl.ZoomLevelChanged += (o, e) => { map.Invalidate(); };  // The map draws at a constant size on screen, so redraw when the zoom changes

            // Zoom buttons
            const float zoomIncrement = 0.1f;
            var btn = card.RequireViewById<ImageButton>(Resource.Id.zoomInBtn);
            btn.Click += (o, e) => { zl.ZoomLevel += zoomIncrement; };
            btn = card.RequireViewById<ImageButton>(Resource.Id.zoomOutBtn);
            btn.Click += (o, e) => { zl.ZoomLevel -= zoomIncrement; };
            btn = card.RequireViewById<ImageButton>(Resource.Id.zoomResetBtn);
            btn.Click += (o, e) => { zl.ResetZoom(); };
        }


        private void OnRouteMap_WaypointClick(object? sender, RouteMapView.WaypointClickEventArgs args)
        {
            RouteMapWaypointClick?.Invoke(this, args);
        }


        private void PopulateRouteAltitudeCard(CardView card)
        {
            var tv = card.RequireViewById<TextView>(Resource.Id.cardHeading);
            tv.SetText(Resource.String.routeWaypointAltitudesTitle);
            card.RequireViewById<TextView>(Resource.Id.cardFooter).Text = string.Empty;  // A re-bound card keeps its previous footer

            // Set chart height
            var chart = card.RequireViewById<LineChart>(Resource.Id.chart);
            var lp = chart.LayoutParameters!;  // Set by the layout
            lp.Height = (int)(chart.Width * 0.7);
            chart.LayoutParameters = lp;

            var elevations = this._waypoints.Select(wp => wp.Altitude).OfType<double>().ToArray();
            chart.Visibility = elevations.Length > 0 ? ViewStates.Visible : ViewStates.Gone;
            if (elevations.Length > 0)
            {
                var points = new Entry[elevations.Count()];
                for (var i = 0; i < elevations.Count(); i++)
                {
                    points[i] = new Entry(i, (float)UnitConverter.Convert((decimal)elevations[i], LengthUnit.Metre, _userSettings.Navigation_PreferredAltitudeUnit));
                }
                var ds = new LineDataSet(points, this._context.GetFormattedString(Resource.String.routeWaypointAltitudesChartLabel,
                                                                                  ValueFormatter.ToString(this._context, _userSettings.Navigation_PreferredAltitudeUnit)));
                ds.Color = AndroidX.Core.Content.ContextCompat.GetColor(this._context, Resource.Color.colorAccent);
                ds.LineWidth = 2f;
                var ld = new LineData(ds);
                chart.XAxis.Enabled = false;
                chart.Description.Enabled = false;
                chart.SetPinchZoom(true);
                chart.DragEnabled = true;
                chart.Data = ld;
            }
            else
            {
                // No elevations
                tv = card.RequireViewById<TextView>(Resource.Id.cardFooter);
                tv.SetText(Resource.String.noDataMsg);
            }
        }


        private void PopulateRouteLegDistanceCard(CardView card)
        {
            var tv = card.RequireViewById<TextView>(Resource.Id.cardHeading);
            tv.SetText(Resource.String.routeLegDistancesTitle);
            card.RequireViewById<TextView>(Resource.Id.cardFooter).Text = string.Empty;  // A re-bound card keeps its previous footer

            // Set chart height
            var chart = card.RequireViewById<BarChart>(Resource.Id.chart);
            var lp = chart.LayoutParameters!;  // Set by the layout
            lp.Height = (int)(chart.Width * 0.7);
            chart.LayoutParameters = lp;

            var distances = _routeProps.LegProperties.Select(wp => wp.Distance).ToArray();
            chart.Visibility = distances.Length > 0 ? ViewStates.Visible : ViewStates.Gone;
            if (distances.Length > 0)
            {
                var entries = new BarEntry[distances.Count()];
                for (var i = 0; i < distances.Count(); i++)
                {
                    entries[i] = new BarEntry(i, (float)UnitConverter.Convert((decimal)distances[i], LengthUnit.Kilometre, _userSettings.Navigation_PreferredDistanceUnit));
                }
                var ds = new BarDataSet(entries, this._context.GetFormattedString(Resource.String.routeLegDistancesChartLabel,
                                                                                  ValueFormatter.ToString(this._context, _userSettings.Navigation_PreferredDistanceUnit)));
                ds.Color = AndroidX.Core.Content.ContextCompat.GetColor(this._context, Resource.Color.colorAccent);
                var ld = new BarData(ds);
                chart.XAxis.Enabled = false;
                chart.Description.Enabled = false;
                chart.SetPinchZoom(true);
                chart.DragEnabled = true;
                chart.Data = ld;
            }
            else
            {
                // No legs
                tv = card.RequireViewById<TextView>(Resource.Id.cardFooter);
                tv.SetText(Resource.String.noDataMsg);
            }
        }
    }


    /// <summary>
    /// Extension methods for <see cref="Waypoint"/>.
    /// </summary>
    public static class WaypointExt
    {
        /// <summary>
        /// Get a waypoint's location. The time is the waypoint's creation time.
        /// </summary>
        /// <param name="self">Waypoint</param>
        /// <returns>Location</returns>
        public static GeoLib.Location GetLocation(
            this Waypoint self)
        {
            return new GeoLib.Location()
            {
                Time = self.CreationTime,
                Latitude = self.Latitude,
                Longitude = self.Longitude,
                Altitude = self.Altitude
            };
        }
    }
}
