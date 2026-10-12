using System;

using Android.Content;
using Android.Views;

using AndroidX.RecyclerView.Widget;


namespace Romer.Mobile.App
{
    /// <summary>
    /// ViewPager2 adapter for RouteActivity's tab pages: route info and waypoints.
    /// The pages are plain views. RouteActivity fills them in when they are selected.
    /// </summary>
    public class RouteActivityPagerAdapter : RecyclerView.Adapter
    {
        /// <summary>Position of the route info page.</summary>
        public const int InfoPage = 0;

        /// <summary>Position of the waypoints page.</summary>
        public const int WaypointsPage = 1;


        /// <summary>
        /// Constructor. Initialise a RouteActivityPagerAdapter object.
        /// </summary>
        /// <param name="context">Context</param>
        public RouteActivityPagerAdapter(Context context)
        {
            this._context = context;
        }

        private readonly Context _context;


        /// <inheritdoc/>
        public override int ItemCount => 2;


        /// <inheritdoc/>
        /// <remarks>
        /// Each page has its own view type, so that the RecyclerView never binds one page's view to the other page.
        /// </remarks>
        public override int GetItemViewType(int position) => position;


        /// <inheritdoc/>
        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var layoutResId = viewType switch
            {
                InfoPage => Resource.Layout.route_activity_info_tab,
                WaypointsPage => Resource.Layout.route_activity_waypoints_tab,
                _ => throw new InvalidOperationException($"Invalid route activity page: {viewType}")
            };
            return new PageViewHolder(LayoutInflater.From(_context)!.Inflate(layoutResId, parent, false)!);
        }


        /// <inheritdoc/>
        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            // Nothing to bind. RouteActivity fills in the page when it's selected.
        }


        private class PageViewHolder : RecyclerView.ViewHolder
        {
            public PageViewHolder(View view)
                : base(view)
            {
            }
        }
    }
}
