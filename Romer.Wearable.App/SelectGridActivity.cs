using System;
using System.Collections.Generic;
using System.Linq;

using Android.App;
using Android.Content;
using Android.Locations;
using Android.OS;
using Android.Views;
using Android.Widget;
using Location = Android.Locations.Location;

using AndroidX.AppCompat.Widget;
using AndroidX.RecyclerView.Widget;

using Romer.GeoLib;
using Romer.CommonLib;


namespace Romer.Wearable.App
{
    /// <summary>
    /// Lets the user choose the grid system for showing the location: those that apply at the current location, or
    /// all of them if the location isn't known. Debug builds also list the others.
    /// Start it with <see cref="CreateIntent"/>, and read the choice with <see cref="ReadResult"/>.
    /// </summary>
    [Activity(Label = "@string/selectGridTitle")]
    public class SelectGridActivity : Activity
    {
        private const string CurrentLocationKey = "CurrentLocation";  // in
        private const string SelectedProviderTag = "SelectedProviderTag";  // out

        private Android.Locations.Location? _currentLocation;


        /// <summary>
        /// Create an intent that starts this activity to let the user choose a grid system.
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="currentLocation">Current location, used to offer only the grid systems that cover it, or null to offer all</param>
        /// <returns>Intent</returns>
        public static Intent CreateIntent(Context context, Location? currentLocation)
        {
            var intent = new Intent(context, typeof(SelectGridActivity));
            if (currentLocation != null)
                intent.PutExtra(CurrentLocationKey, currentLocation);
            return intent;
        }


        /// <summary>
        /// Get the chosen grid system from this activity's result.
        /// </summary>
        /// <param name="result">Activity result</param>
        /// <returns>The chosen grid system provider's tag, or null if the user didn't choose one</returns>
        public static string? ReadResult(AndroidX.Activity.Result.ActivityResult result)
        {
            return result.ResultCode == (int)Result.Ok ? result.Data?.GetStringExtra(SelectedProviderTag) : null;
        }


        /// <inheritdoc/>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            // Load the night or day colours theme
            var userSettings = UserSettings.Load(this);
            SetTheme(userSettings.Navigation_NightColoursMode ? Resource.Style.Theme_App_NightColours : Resource.Style.Theme_App);

            //
            base.OnCreate(savedInstanceState);
            this.SetContentView(Resource.Layout.select_grid_activity);

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

            IEnumerable<IGridSystemProvider> gridProviders;
            if (_currentLocation != null)
            {
                gridProviders = GridSystemProviderManager.AllInArea(new GeoLib.Location().From(_currentLocation));
            }
            else
            {
                gridProviders = GridSystemProviderManager.GetProviders();
            }
#if DEBUG
            // Debug only: Append out-of-area providers
            if (_currentLocation != null)
            {
                var tags = gridProviders.Select(p => p.Tag);
                var ooaGridProviders = GridSystemProviderManager.GetProviders().Where(p => !tags.Contains(p.Tag));
                gridProviders = gridProviders.Concat(ooaGridProviders);
            }
            
#endif

            var rv = RequireViewById<AndroidX.Wear.Widget.WearableRecyclerView>(Resource.Id.grid_list);
            rv.HasFixedSize = true;
            rv.SetLayoutManager(new LinearLayoutManager(this));
            rv.SetAdapter(new SelectGridMenuItemsAdaptor(this, gridProviders));
        }


        /// <inheritdoc/>
        protected override void OnSaveInstanceState(Bundle saveState)
        {
            if (_currentLocation != null)
                saveState.PutParcelable(CurrentLocationKey, _currentLocation);

            base.OnSaveInstanceState(saveState);
        }


        /// <inheritdoc/>
        protected override void OnRestoreInstanceState(Bundle savedState)
        {
            base.OnRestoreInstanceState(savedState);

            if (savedState.ContainsKey(CurrentLocationKey))
                _currentLocation = AndroidX.Core.OS.BundleCompat.GetParcelable(savedState, CurrentLocationKey, Java.Lang.Class.FromType(typeof(Android.Locations.Location))) as Android.Locations.Location;
            else
                _currentLocation = null;
        }

        
        private void ViewHolder_Click(object? sender, EventArgs e)
        {
            var gridProviderTag = (string?)((SelectGridMenuItemViewHolder)sender!).ItemView.Tag;
            
            var intent = new Intent();
            intent.PutExtra(SelectedProviderTag, gridProviderTag);
            this.SetResult(Result.Ok, intent);
            this.Finish();
        }


        /// <summary>
        /// RecyclerView adapter for the grid systems. Each shows its icon and description.
        /// </summary>
        public class SelectGridMenuItemsAdaptor : RecyclerView.Adapter
        {
            /// <summary>
            /// Constructor. Initialise a SelectGridMenuItemsAdaptor object.
            /// </summary>
            /// <param name="activity">Activity, which handles clicks on the items</param>
            /// <param name="providers">Grid system providers to list</param>
            public SelectGridMenuItemsAdaptor(
                SelectGridActivity activity,
                IEnumerable<IGridSystemProvider> providers)
            {
                this._activity = activity;
                this._providers = providers;
            }

            private readonly SelectGridActivity _activity;
            private readonly IEnumerable<IGridSystemProvider> _providers;

            /// <inheritdoc/>
            public override int ItemCount
            {
                get { return _providers.Count(); }
            }


            /// <inheritdoc/>
            public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
            {
                var vh = (SelectGridMenuItemViewHolder)holder;
                vh.ResetClickHandler();  // Clear the view holder's click event as it may have been set before.

                var img = vh.ItemView.RequireViewById<ImageView>(Resource.Id.item_img);
                img.SetImageResource(GridSystemProviderManager.GetIconDrawableResId(_providers.ElementAt(position)));

                var txt = vh.ItemView.RequireViewById<TextView>(Resource.Id.item_text);
                txt.SetText(GridSystemProviderManager.GetDescriptionStringResId(_providers.ElementAt(position)));

                vh.ItemView.Tag = _providers.ElementAt(position).Tag;
                vh.Click += this._activity.ViewHolder_Click;
            }


            /// <inheritdoc/>
            public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
            {
                View v = LayoutInflater.From(parent.Context)!.Inflate(Resource.Layout.list_image_item, parent, false)!;
                return new SelectGridMenuItemViewHolder(v);
            }
        }


        /// <summary>
        /// View holder for a grid system in the list. The item view's tag is the provider's tag.
        /// </summary>
        public class SelectGridMenuItemViewHolder : RecyclerView.ViewHolder
        {
            /// <summary>
            /// Constructor. Initialise a SelectGridMenuItemViewHolder object.
            /// </summary>
            /// <param name="view">Item view. A click on any of its views raises <see cref="Click"/>.</param>
            public SelectGridMenuItemViewHolder(View view)
                : base(view)
            {
                this.ItemView = view;
                AttachClickHandler(view);
            }


            /// <summary>
            /// Fires when the item is clicked. The sender is the view holder.
            /// </summary>
            public event EventHandler? Click;


            /// <summary>
            /// Remove all <see cref="Click"/> handlers, so that a recycled view holder can be bound to another item.
            /// </summary>
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
    }
}
