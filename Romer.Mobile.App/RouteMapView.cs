using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Util;
using Android.Views;

using Romer.DataLib;
using Romer.CommonLib;
using Kilsten.Android;
using Kilsten.Android.Controls;




namespace Romer.Mobile.App
{
    /// <summary>
    /// A sketch map of a route: its waypoints, with arrows between them, and optionally their numbers and labels.
    /// Intended to be the child of a <see cref="ZoomLayout"/>. It fits the whole route in the view, and draws markers,
    /// lines and labels at a constant size on screen whatever the zoom, so redraw it when the zoom changes.
    /// </summary>
    public class RouteMapView : View, GestureDetector.IOnGestureListener
    {
        /// <inheritdoc/>
        public RouteMapView(Context context)
            : base(context)
        {
            Initialise(context, null);
        }

        /// <inheritdoc/>
        public RouteMapView(Context context, IAttributeSet? attrs)
            : base(context, attrs)
        {
            Initialise(context, attrs);
        }

        /// <inheritdoc/>
        public RouteMapView(Context context, IAttributeSet? attrs, int defStyle)
            : base(context, attrs, defStyle)
        {
            Initialise(context, attrs);
        }

        /// <inheritdoc/>
        public RouteMapView(Context context, IAttributeSet? attrs, int defStyleAttr, int defStyleRes)
            : base(context, attrs, defStyleAttr, defStyleRes)
        {
            Initialise(context, attrs);
        }


        /// <summary>
        /// Fires when the user taps on or near a waypoint.
        /// </summary>
        public event EventHandler<WaypointClickEventArgs>? WaypointClick;

        /// <summary>
        /// Waypoint click event arguments
        /// </summary>
        public class WaypointClickEventArgs : EventArgs
        {
            /// <summary>ID of the waypoint that was clicked</summary>
            public int WaypointId { get; set; }
        }



        // Styleables. The colours are opaque grey. Color(int) takes ARGB, so the previous 0x808080 was fully transparent.
        private Color _routeStrokeColour = new Android.Graphics.Color(unchecked((int)0xFF808080));
        private float _routeStrokeWidth = 1f;
        private Color _waypointMarkerColour = new Android.Graphics.Color(unchecked((int)0xFF808080));
        private float _waypointMarkerDiameter = 3f;
        private bool _showWaypointNumber = false;
        private bool _showWaypointLabel = false;
        private Color _waypointLabelColour = new Android.Graphics.Color(unchecked((int)0xFF808080));
        private int _waypointLabelMaxLength = 10;
        private int _waypointTouchRadius;  // in pixels


        private float _labelLargeTextSize;
        private float _labelSmallTextSize;

        private Route? _route;
        private float _minLon, _minLat, _maxLon, _maxLat;
        private float _lonScale = 1f;  // cos(mid-latitude): the length of a degree of longitude relative to one of latitude

        private GestureDetector _gestureDetector;


        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_gestureDetector))]
        private void Initialise(
            Context context,
            IAttributeSet? attrs)
        {
            this._gestureDetector = new GestureDetector(context, this as GestureDetector.IOnGestureListener);

            // Set the default touch radius here rather than with the other defaults, because it depends on the display
            // density. It must be set even without attributes, or a view created in code would have a radius of 0 and
            // its waypoints couldn't be tapped.
            this._waypointTouchRadius = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, 32f/*dp*/, context.RequireResources().DisplayMetrics);  // 48dp or 56dp are good touch radi

            if (attrs != null)
            {
                var a = context.ObtainStyledAttributes(attrs, Resource.Styleable.RouteMapView);
                this._routeStrokeColour = a.GetColor(Resource.Styleable.RouteMapView_routeStrokeColor, this._routeStrokeColour);
                this._routeStrokeWidth = a.GetFloat(Resource.Styleable.RouteMapView_routeStrokeWidth, this._routeStrokeWidth);
                this._waypointMarkerColour = a.GetColor(Resource.Styleable.RouteMapView_waypointMarkerColor, this._waypointMarkerColour);
                this._waypointMarkerDiameter = a.GetFloat(Resource.Styleable.RouteMapView_waypointMarkerDiameter, this._waypointMarkerDiameter);
                this._showWaypointNumber = a.GetBoolean(Resource.Styleable.RouteMapView_showWaypointNumber, this._showWaypointNumber);
                this._showWaypointLabel = a.GetBoolean(Resource.Styleable.RouteMapView_showWaypointLabel, this._showWaypointLabel);
                this._waypointLabelColour = a.GetColor(Resource.Styleable.RouteMapView_waypointLabelColor, this._waypointLabelColour);
                this._waypointLabelMaxLength = a.GetInt(Resource.Styleable.RouteMapView_waypointLabelMaxLength, this._waypointLabelMaxLength);
                this._waypointTouchRadius = a.GetDimensionPixelSize(Resource.Styleable.RouteMapView_waypointTouchRadius, this._waypointTouchRadius);
                a.Recycle();
            }

            var ta = context.ObtainStyledAttributes(Android.Resource.Style.TextAppearanceSmall, new int[] { Android.Resource.Attribute.TextSize });
            _labelLargeTextSize = (float)ta.GetDimensionPixelSize(0, 16);
            _labelSmallTextSize = _labelLargeTextSize * 0.5f;
            ta.Recycle();
        }


        /// <summary>
        /// Set the route to show, and redraw.
        /// </summary>
        /// <param name="route">Route, with at least one waypoint</param>
        /// <exception cref="InvalidOperationException">The route has no waypoints</exception>
        public void SetRoute(Route route)
        {
            const float marginPge = 5f;  // The margin on each side is the span divided by this, so 20% of it
            const float minDiff = 0.01f;

            this._route = route;

            // Determine min and max longitude
            this._minLon = (float) route.Waypoints.Select(wp => wp.Longitude).Min();
            this._maxLon = (float) route.Waypoints.Select(wp => wp.Longitude).Max();
            if (this._minLon == this._maxLon)
            {
                this._minLon -= minDiff;
                this._maxLon += minDiff;
            }
            var margin = (this._maxLon - this._minLon) / marginPge;
            this._minLon -= margin;
            this._maxLon += margin;

            // Determine min and max latitude
            this._minLat = (float) route.Waypoints.Select(wp => wp.Latitude).Min();
            this._maxLat = (float) route.Waypoints.Select(wp => wp.Latitude).Max();
            if (this._minLat == this._maxLat)
            {
                this._minLat -= minDiff;
                this._maxLat += minDiff;
            }
            margin = (this._maxLat - this._minLat) / marginPge;
            this._minLat -= margin;
            this._maxLat += margin;

            var midLatRad = (this._minLat + this._maxLat) / 2.0 * Math.PI / 180.0;
            this._lonScale = (float)Math.Cos(midLatRad);

            // Redraw. Also lay out again, so that the zoom layout's LaidOut event fires for the new route.
            this.RequestLayout();
            this.Invalidate();
        }


        /// <summary>
        /// Scroll the parent <see cref="ZoomLayout"/> so that the route's first waypoint is visible.
        /// Does nothing if there's no route, or the parent isn't a ZoomLayout.
        /// </summary>
        public void ShowFirstWaypoint()
        {
            var wp = this._route?.Waypoints?.FirstOrDefault();
            var zl = this.Parent as ZoomLayout;
            if (wp != null && zl != null)
            {
                var coords = ToCanvasCoords(wp);
                zl.EnsureVisible(new PointF(x: coords.x, y: coords.y));
            }
        }


        /// <inheritdoc/>
        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            // Take all the space offered. ToCanvasCoords() fits the route inside it, so the whole route is visible
            // at the zoom layout's minimum zoom, and zooming in shows more detail.
            SetMeasuredDimension(MeasureSpec.GetSize(widthMeasureSpec), MeasureSpec.GetSize(heightMeasureSpec));
        }


        /// <inheritdoc/>
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);

            if (_route == null || _route.Waypoints.Count() == 0)
            {
                return;
            }

            var wpa = _route.Waypoints.ToArray();
            var wpc = wpa.Select(wp => ToCanvasCoords(wp)).ToArray();

            // The zoom layout scales this whole view. Divide sizes by the scale, so that markers, lines and labels stay
            // the same size on screen at any zoom. Zooming in then spreads waypoints apart without enlarging them,
            // which reveals labels that were skipped because they overlapped.
            var screenScale = 1f / Math.Max(Math.Max(this.ScaleX, this.ScaleY), 0.01f);

            // Draw waypoint markers, if required
            if (this._waypointMarkerDiameter > 0f)
            {
                Paint waypointMarkerPaint = new Paint(PaintFlags.AntiAlias)
                {
                    Color = _waypointMarkerColour
                };
                waypointMarkerPaint.SetStyle(Paint.Style.Fill);

                var waypointMarkerRad = this._waypointMarkerDiameter * 0.5f * screenScale;
                for (int i = 0; i < wpc.Length; i++)
                {
                    canvas.DrawCircle(wpc[i].x, wpc[i].y, waypointMarkerRad, waypointMarkerPaint);
                }
            }

            // Draw connecting lines, if required
            if (this._routeStrokeWidth > 0)
            {
                var routePaint = new Paint(PaintFlags.AntiAlias)
                {
                    Color = _routeStrokeColour,
                    StrokeWidth = _routeStrokeWidth * screenScale,
                };
                routePaint.SetStyle(Paint.Style.Stroke);

                var arrowHeadRad = this._waypointMarkerDiameter * 0.75f * screenScale;
                for (int i = 0; i < wpc.Length - 1; i++)
                {
                    canvas.DrawArrow(wpc[i].x, wpc[i].y, wpc[i+1].x, wpc[i+1].y, arrowHeadRad, 35f, ArrowHeadStyle.Filled, routePaint);
                }
            }

            // Draw labels, if required
            if (this._showWaypointNumber || this._showWaypointLabel)
            {
                // Initialse paint for labels
                var waypointLabelPaint = new Paint(PaintFlags.AntiAlias)
                {
                    Color = _waypointLabelColour,
                    TextSize = _labelSmallTextSize * screenScale
                };
                var waypointLabelBackgroundPaint = new Paint(PaintFlags.AntiAlias)
                {
                    Color = this.Background is ColorDrawable cd ? cd.Color : Color.Transparent,  // fallback to transparent if background not a solid colour drawable
                    Alpha = 192, // 75% opaque
                };
                var waypointLabelSb = new StringBuilder();
                var waypointLabelBounds = new Rect();
                var waypointLabelOffset = this._waypointMarkerDiameter * 0.75f * screenScale;
                var labelGap = waypointLabelOffset * 0.5f;  // Minimum space between labels

                // Label the first and last waypoints first, so that they're always labelled, then the others in order.
                // Skip a label that would overlap one already drawn: on a dense route, labelling every waypoint
                // makes them all unreadable.
                var drawnLabels = new List<RectF>();
                var order = wpc.Length == 1 ? new[] { 0 } : new[] { 0, wpc.Length - 1 }.Concat(Enumerable.Range(1, wpc.Length - 2));
                foreach (var i in order)
                {
                    // Build the label
                    waypointLabelSb.Clear();
                    if (this._showWaypointNumber)
                    {
                        waypointLabelSb.Append((i + 1).ToString());
                    }
                    if (this._showWaypointLabel)
                    {
                        if (waypointLabelSb.Length > 0)
                        {
                            waypointLabelSb.Append(". ");
                        }
                        var label = wpa[i].Label.Length > _waypointLabelMaxLength ? wpa[i].Label.Substring(0, _waypointLabelMaxLength) + "..." : wpa[i].Label;
                        waypointLabelSb.Append(label);
                    }
                    var text = waypointLabelSb.ToString();

                    // Measure the label and determine position wrt waypoint.
                    // By default we position to the right of the waypoint marker.
                    var lc = wpc[i];
                    waypointLabelPaint.GetTextBounds(text, 0, text.Length, waypointLabelBounds);
                    if (lc.x + waypointLabelOffset + waypointLabelBounds.Width() > canvas.Width)
                    {
                        // Label would be clipped by canvas rhs. Position to left
                        lc.x -= (waypointLabelOffset + waypointLabelBounds.Width());
                        lc.x = Math.Max(lc.x, 0f);
                    }
                    var labelRect = new RectF(lc.x + waypointLabelOffset, lc.y - waypointLabelBounds.Height(),
                                              lc.x + waypointLabelOffset + waypointLabelBounds.Width(), lc.y);

                    // Skip the label if it would overlap one already drawn
                    var spaced = new RectF(labelRect.Left - labelGap, labelRect.Top - labelGap, labelRect.Right + labelGap, labelRect.Bottom + labelGap);
                    if (drawnLabels.Any(r => RectF.Intersects(r, spaced)))
                        continue;
                    drawnLabels.Add(labelRect);

                    // Draw a rectangle behind the waypoint label, then the label
                    canvas.DrawRect(labelRect, waypointLabelBackgroundPaint);
                    canvas.DrawText(text, labelRect.Left, labelRect.Bottom, waypointLabelPaint);
                }
            }
        }


        /// <summary>
        /// Convert lat/lon to canvas coordinates with origin at top/left.
        /// Uses _minLat/_maxLat and _minLon/_maxLon for scaling.
        /// </summary>
        /// <param name="wp">Waypoint</param>
        /// <returns>Canvas coordinates with origin at top/left</returns>
        private (float x, float y) ToCanvasCoords(Waypoint wp)
        {
            // A degree of longitude is shorter than a degree of latitude, by a factor of cos(latitude), so scale
            // longitudes by that to get the route's true shape. Use the same scale for both axes, fitting the route
            // in the view, and centre it.
            var lonSpan = (this._maxLon - this._minLon) * this._lonScale;
            var latSpan = this._maxLat - this._minLat;
            var scale = Math.Min(this.Width / lonSpan, this.Height / latSpan);
            var x = (((float)wp.Longitude - this._minLon) * this._lonScale * scale) + ((this.Width - (lonSpan * scale)) / 2f);
            var y = ((this._maxLat - (float)wp.Latitude) * scale) + ((this.Height - (latSpan * scale)) / 2f);
            return (x, y);
        }


        /// <inheritdoc/>
        public override bool OnTouchEvent(MotionEvent? e)
        {
            // Pass event to the gesture detector.
            if (e != null)
                _gestureDetector.OnTouchEvent(e);

            // Return false to allow event propagation to parent views.
            return false;
        }


        #region GestureDetector.IOnGestureListener

        bool GestureDetector.IOnGestureListener.OnSingleTapUp(MotionEvent e)
        {
            if (_route == null)
                return false;

            // Determine the closest waypoint to the click point.
            var bestIdx = -1;
            var bestDist = float.MaxValue;
            for (int i = 0; i < _route.Waypoints.Count(); i++)
            {
                var wpc = ToCanvasCoords(_route.Waypoints.ElementAt(i));
                var dx = Math.Abs(wpc.x - e.GetX());
                var dy = Math.Abs(wpc.y - e.GetY());
                var dist = (float)Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2));
                if (dist < bestDist)
                {
                    bestIdx = i;
                    bestDist = dist;
                }
            }

            // The touch radius is in screen pixels, and the zoom layout scales this view, so convert it to view units
            var r = this._waypointTouchRadius / Math.Max(Math.Max(this.ScaleX, this.ScaleY), 0.01f);
            if (bestIdx != -1 && bestDist <= r)
            {
                var ea = new WaypointClickEventArgs()
                {
                    WaypointId = _route.Waypoints.ElementAt(bestIdx).Id
                };
                this.WaypointClick?.Invoke(this, ea);
                return true;
            }
            else
            {
                return false;
            }
        }


        bool GestureDetector.IOnGestureListener.OnDown(MotionEvent e)
        {
            // Intentionally empty
            return false;
        }

        bool GestureDetector.IOnGestureListener.OnFling(MotionEvent? e1, MotionEvent e2, float velocityX, float velocityY)
        {
            // Intentionally empty
            return false;
        }

        void GestureDetector.IOnGestureListener.OnLongPress(MotionEvent e)
        {
            // Intentionally empty
        }

        bool GestureDetector.IOnGestureListener.OnScroll(MotionEvent? e1, MotionEvent e2, float distanceX, float distanceY)
        {
            // Intentionally empty
            return false;
        }

        void GestureDetector.IOnGestureListener.OnShowPress(MotionEvent e)
        {
            // Intentionally empty
        }

        #endregion GestureDetector.IOnGestureListener
    }
}
