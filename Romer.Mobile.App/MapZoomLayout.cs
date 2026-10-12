using System;

using Android.Content;
using Android.Util;
using Android.Views;

using Kilsten.Android.Controls;


namespace Romer.Mobile.App
{
    /// <summary>
    /// ZoomLayout for a map inside scrolling and paging containers.
    /// ZoomLayout keeps every gesture that starts on it, so a drag that starts on the map can't scroll the page or
    /// swipe to another tab. This class keeps a one-finger drag only if the map can pan in the drag's direction, and
    /// otherwise lets the ancestors have it. Two-finger gestures (pinch-zoom) and taps are unchanged.
    /// </summary>
    public class MapZoomLayout : ZoomLayout
    {
        /// <inheritdoc/>
        public MapZoomLayout(Context context)
            : base(context)
        {
            _touchSlop = ViewConfiguration.Get(context)!.ScaledTouchSlop;
        }

        /// <inheritdoc/>
        public MapZoomLayout(Context context, IAttributeSet attrs)
            : base(context, attrs)
        {
            _touchSlop = ViewConfiguration.Get(context)!.ScaledTouchSlop;
        }

        /// <inheritdoc/>
        public MapZoomLayout(Context context, IAttributeSet attrs, int defStyle)
            : base(context, attrs, defStyle)
        {
            _touchSlop = ViewConfiguration.Get(context)!.ScaledTouchSlop;
        }

        /// <inheritdoc/>
        public MapZoomLayout(Context context, IAttributeSet attrs, int defStyle, int defStyleRes)
            : base(context, attrs, defStyle, defStyleRes)
        {
            _touchSlop = ViewConfiguration.Get(context)!.ScaledTouchSlop;
        }


        private enum Decision
        {
            Undecided,  // One finger down, not yet moved far enough to tell what the gesture is
            Keep,       // The map pans or zooms
            Release     // An ancestor scrolls or pages
        }

        private readonly int _touchSlop;
        private Decision _decision = Decision.Undecided;
        private float _downX, _downY;


        /// <inheritdoc/>
        /// <remarks>
        /// Decides here rather than in OnInterceptTouchEvent: once this view handles the down event itself, it no
        /// longer gets OnInterceptTouchEvent calls for the rest of the gesture.
        /// </remarks>
        public override bool DispatchTouchEvent(MotionEvent? e)
        {
            if (e == null)
                return base.DispatchTouchEvent(e);

            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    // ZoomLayout stops the ancestors intercepting while the gesture is undecided
                    _decision = Decision.Undecided;
                    _downX = e.GetX();
                    _downY = e.GetY();
                    break;

                case MotionEventActions.PointerDown:
                    // A second finger: pinch-zoom, so the map keeps the gesture
                    _decision = Decision.Keep;
                    break;

                case MotionEventActions.Move:
                    if (_decision == Decision.Undecided)
                    {
                        var dx = Math.Abs(e.GetX() - _downX);
                        var dy = Math.Abs(e.GetY() - _downY);
                        if (dx <= _touchSlop && dy <= _touchSlop)
                            return true;  // Not a drag yet. Don't let the map creep before it's decided.

                        _decision = CanPan(horizontal: dx > dy) ? Decision.Keep : Decision.Release;
                        if (_decision == Decision.Release)
                        {
                            // Let an ancestor intercept the next move. It will then send this view a cancel event.
                            Parent?.RequestDisallowInterceptTouchEvent(false);
                            return true;
                        }
                    }
                    else if (_decision == Decision.Release)
                    {
                        return true;
                    }
                    break;
            }

            return base.DispatchTouchEvent(e);
        }


        /// <summary>
        /// Whether the map's content extends beyond this view in a direction, so that there's something to pan to.
        /// </summary>
        /// <param name="horizontal">true for horizontal, false for vertical</param>
        private bool CanPan(bool horizontal)
        {
            var child = GetChildAt(0);
            if (child == null)
                return false;

            // Allow a pixel for rounding
            return horizontal ? child.Width * ZoomLevel > Width + 1 : child.Height * ZoomLevel > Height + 1;
        }
    }
}
