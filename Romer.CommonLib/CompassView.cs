using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Views.Animations;



namespace Romer.CommonLib
{
    /// <summary>
    /// A compass bezel: a ring of ticks, bearings and cardinal points, which rotates so that its north marker points
    /// north. The bezel is styled with the bezelMode, bezelColor and accentColor attributes.
    /// </summary>
    public class CompassView : View
    {
        /// <inheritdoc/>
        public CompassView(Context context) :
            base(context)
        {
            Initialise(context, null);
        }

        /// <inheritdoc/>
        public CompassView(Context context, IAttributeSet? attrs)
            : base(context, attrs)
        {
            Initialise(context, attrs);
        }

        /// <inheritdoc/>
        public CompassView(Context context, IAttributeSet? attrs, int defStyle)
            : base(context, attrs, defStyle)
        {
            Initialise(context, attrs);
        }

        /// <inheritdoc/>
        public CompassView(Context context, IAttributeSet? attrs, int defStyle, int defStyleRes)
            : base(context, attrs, defStyle, defStyleRes)
        {
            Initialise(context, attrs);
        }

        // Opaque grey. Color(int) takes ARGB, so the previous 0x808080 was fully transparent.
        private Color _bezelColour = new Android.Graphics.Color(unchecked((int)0xFF808080));
        private Color _accentColour = new Android.Graphics.Color(unchecked((int)0xFF808080));
        private float _labelLargeTextSize;
        private float _labelSmallTextSize;
        private string[] _cardinalLabels = Array.Empty<string>();  // N, E, S, W



        private void Initialise(
            Context context,
            IAttributeSet? attrs)
        {
            if (attrs != null)
            {
                var a = context.ObtainStyledAttributes(attrs, Resource.Styleable.CompassView);
                _bezelMode = (BezelModes)a.GetInt(Resource.Styleable.CompassView_bezelMode, (int)this._bezelMode);
                _bezelColour = a.GetColor(Resource.Styleable.CompassView_bezelColor, this._bezelColour);
                _accentColour = a.GetColor(Resource.Styleable.CompassView_accentColor, this._accentColour);
                a.Recycle();
            }

            var ta = context.ObtainStyledAttributes(Android.Resource.Style.TextAppearanceSmall, new int[] { Android.Resource.Attribute.TextSize });
            _labelLargeTextSize = (float)ta.GetDimensionPixelSize(0, 16);
            _labelSmallTextSize = _labelLargeTextSize * 0.5f;
            ta.Recycle();

            _cardinalLabels = new[]
            {
                context.GetString(Resource.String.northSymbol),
                context.GetString(Resource.String.eastSymbol),
                context.GetString(Resource.String.southSymbol),
                context.GetString(Resource.String.westSymbol)
            };
        }





        // Time to animate a half-turn. Shorter rotations take proportionally less time.
        private const float halfTurnAnimationMsec = 500f;


        /// <summary>
        /// Rotate the bezel so that its north marker points north, given the device's compass bearing.
        /// Animates by the shortest route from the current orientation, replacing any animation in progress.
        /// </summary>
        /// <param name="bearing">Compass bearing in degrees</param>
        public void SetOrientation(
            int bearing)
        {
            // If the compass says 45deg then the north marker should be at 315deg, etc.
            var targetRotation = GeoLib.Bearing.Normalise(-bearing);

            this.Animate()!.Cancel();
            var currentRotation = (float)GeoLib.Bearing.Normalise(this.Rotation);
            this.Rotation = currentRotation;
            var deltaDeg = (float)GeoLib.Bearing.ShortestRotation(currentRotation, targetRotation);
            if (Math.Abs(deltaDeg) < 0.5f)
                return;

            this.Animate()!
                .Rotation(currentRotation + deltaDeg)!
                .SetDuration((long)(halfTurnAnimationMsec * Math.Abs(deltaDeg) / 180f))!
                .SetInterpolator(new DecelerateInterpolator())!
                .Start();
        }


        /// <summary>
        /// Bezel modes. Names and values should match those defined in attrs.xml.
        /// </summary>
        public enum BezelModes
        {
            /// <summary>No bezel</summary>
            None = 0,
            /// <summary>Cardinal points, with short ticks</summary>
            Simple = 1,
            /// <summary>Cardinal points with long ticks, and ticks and bearings at the intercardinal points</summary>
            Standard = 2,
            /// <summary>Cardinal points, a tick every 5 degrees, and a bearing every 20 degrees</summary>
            Detailed = 3
        }

        /// <summary>
        /// How much detail the bezel shows. Setting it redraws the view.
        /// </summary>
        public BezelModes BezelMode
        {
            get
            {
                return _bezelMode;
            }
            set
            {
                this._bezelMode = value;
                this.Invalidate();
            }
        }
        private BezelModes _bezelMode = CompassView.BezelModes.Standard;


        /// <inheritdoc/>
        protected override void OnDraw(Canvas canvas)
        {
            base.OnDraw(canvas);

            if (this._bezelMode == BezelModes.None)
                return;

            var radius = Math.Min(this.Width / 2f, this.Height / 2f);
            var shortTickLen = radius * 0.03f;
            var longTickLen = radius * 0.06f;
            var labelMargin = radius * 0.02f;
            var labelRect = new Rect();

            var tickPaint = new Paint(PaintFlags.AntiAlias)
            {
                StrokeWidth = 2f
            };

            var labelPaint = new Paint(PaintFlags.AntiAlias)
            {
                TextAlign = Paint.Align.Center
            };


            canvas.Save();
            const float incrementDeg = 5f;
            for(var thetaDeg = 0f; thetaDeg < 360f; thetaDeg += incrementDeg)
            {
                // Customise tick length and label according to bezel mode
                float tickLen = 0f;   // default no tick
                string? label = null;  // default no label
                switch(_bezelMode)
                {
                    case BezelModes.Simple:
                        tickLen = (thetaDeg % 90f == 0) ? shortTickLen : 0f;
                        label = null;
                        break;
                    case BezelModes.Standard:
                        if (thetaDeg % 90f == 0)
                        {
                            tickLen = longTickLen;
                        }
                        else if (thetaDeg % 45f == 0)
                        {
                            tickLen = shortTickLen;
                            label = thetaDeg.ToString("N0");
                        }
                        break;
                    case BezelModes.Detailed:
                        tickLen = (thetaDeg % 10f == 0f && thetaDeg % 20f != 0) ? longTickLen : shortTickLen;
                        label = (thetaDeg % 20f == 0f) ? thetaDeg.ToString("N0") : null;
                        break;
                }

                // Override label for cardinal directions
                label = thetaDeg switch
                {
                    0f => _cardinalLabels[0],
                    90f => _cardinalLabels[1],
                    180f => _cardinalLabels[2],
                    270f => _cardinalLabels[3],
                    _ => label
                }; ;

                // Draw tick
                if (tickLen > 0f)
                {
                    tickPaint.Color = thetaDeg == 0 ? this._accentColour : this._bezelColour;
                    canvas.DrawLine(radius, 0, radius, tickLen, tickPaint);
                }

                // Draw label
                if (label != null)
                {
                    // Draw a label
                    labelPaint.Color = thetaDeg == 0 ? this._accentColour : this._bezelColour;
                    labelPaint.SetTypeface(thetaDeg == 0 ? Typeface.DefaultBold : Typeface.Default);
                    labelPaint.TextSize = thetaDeg % 90 == 0 ? _labelLargeTextSize : _labelSmallTextSize;
                    labelPaint.GetTextBounds(label, 0, label.Length, labelRect);
                    canvas.DrawText(label, radius, tickLen + labelMargin + labelRect.Height(), labelPaint);
                }

                //
                canvas.Rotate(incrementDeg, radius, radius);
            }
            canvas.Restore();
        }
    }
}
