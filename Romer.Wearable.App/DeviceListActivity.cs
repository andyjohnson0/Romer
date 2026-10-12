using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.Locations;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using Android.Gms.Wearable;

using AndroidX.Activity.Result;
using AndroidX.RecyclerView.Widget;
using AndroidX.AppCompat.Widget;



namespace Romer.Wearable.App
{
    /// <summary>
    /// Lets the user choose a phone to send the route to, when more than one is connected.
    /// Start it with <see cref="CreateIntent"/>, and read the choice with <see cref="ReadResult"/>.
    /// </summary>
    [Activity]   // Note: title is provided by recycler view in OnBindViewHolder()
    public class DeviceListActivity : Activity
    {
        // Input keys
        private const string NodeIdsKey = "NodeIdsKey";
        private const string NodeDisplayNamesKey = "NodeDisplayNamesKey";

        // Output keys
        private const string NodeIdKey = "NodeIdKey";
        private const string NodeDisplayNameKey = "NodeDisplayNameKey";


        /// <summary>
        /// Create an intent that starts this activity to let the user choose between devices.
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="nodes">Devices to choose between</param>
        /// <returns>Intent</returns>
        public static Intent CreateIntent(Context context, IEnumerable<INode> nodes)
        {
            var intent = new Intent(context, typeof(DeviceListActivity));
            intent.PutExtra(NodeIdsKey, nodes.Select(n => n.Id).ToArray());
            intent.PutExtra(NodeDisplayNamesKey, nodes.Select(n => n.DisplayName).ToArray());
            return intent;
        }


        /// <summary>
        /// Get the chosen device from this activity's result.
        /// </summary>
        /// <param name="result">Activity result</param>
        /// <returns>The chosen device's node ID and display name, or null if the user didn't choose one</returns>
        public static (string NodeId, string DisplayName)? ReadResult(ActivityResult result)
        {
            var nodeId = result.Data?.GetStringExtra(NodeIdKey);
            if (result.ResultCode != (int)Result.Ok || nodeId == null)
                return null;
            return (nodeId, result.Data?.GetStringExtra(NodeDisplayNameKey) ?? nodeId);
        }


        /// <inheritdoc/>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            // Load the night or day colours theme
            var userSettings = UserSettings.Load(this);
            SetTheme(userSettings.Navigation_NightColoursMode ? Resource.Style.Theme_App_NightColours : Resource.Style.Theme_App);

            //
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.device_list_activity);

            // Always read the devices from the intent. Saved instance state doesn't hold them.
            var extras = this.Intent?.Extras ?? throw new InvalidOperationException("No devices specified");

            var rv = RequireViewById<AndroidX.Wear.Widget.WearableRecyclerView>(Resource.Id.device_list);
            rv.HasFixedSize = false;
            rv.SetLayoutManager(new LinearLayoutManager(this));
            rv.SetAdapter(new DeviceListAdaptor(this, extras.GetStringArray(NodeIdsKey) ?? Array.Empty<string>(), extras.GetStringArray(NodeDisplayNamesKey) ?? Array.Empty<string>()));
        }


        private void OnNodeListItemClicked(object? sender, DeviceListClickEventArgs ea)
        {
            var intent = new Intent();
            intent.PutExtra(NodeIdKey, ea.DeviceId);
            intent.PutExtra(NodeDisplayNameKey, ea.DeviceDisplayName);
            SetResult(Result.Ok, intent);
            Finish();
        }



        private class DeviceListAdaptor : RecyclerView.Adapter
        {
            public DeviceListAdaptor(
                DeviceListActivity activity,
                string[] nodeIds,
                string[] nodeDisplayNames)
            {
                this._activity = activity;
                this._nodeIds = nodeIds;
                this._nodeDisplayNames = nodeDisplayNames;
            }

            private readonly DeviceListActivity _activity;
            private string[] _nodeIds;
            private string[] _nodeDisplayNames;


            /// <inheritdoc/>
            public override int ItemCount
            {
                get { return this._nodeIds.Length + 1; }
            }


            /// <summary>View type of the heading, at position 0.</summary>
            public const int headingItemType = 0;

            /// <summary>View type of a device.</summary>
            public const int menuItemType = 1;



            /// <inheritdoc/>
            public override int GetItemViewType(int position)
            {
                return position == 0 ? headingItemType : menuItemType;
            }


            /// <inheritdoc/>
            public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
            {
                // Clear the view holder's click event as it may have been set before.
                var vh = (DeviceListViewHolder)holder;
                vh.ResetClickHandler();

                var txt = vh.ItemView.RequireViewById<TextView>(Resource.Id.item_text);
                var img = vh.ItemView.RequireViewById<ImageView>(Resource.Id.item_img);

                if (position == 0)
                {
                    txt.Text = _activity.GetString(Resource.String.deviceListActivityTitle);
                }
                else
                {
                    img.SetImageResource(Resource.Drawable.ic_phone_android);
                    txt.Text = this._nodeDisplayNames[position - 1];
                    vh.Click += (o, e) => 
                    {
                        e.DeviceId = this._nodeIds[e.Position - 1];
                        e.DeviceDisplayName = this._nodeDisplayNames[e.Position - 1];
                        _activity.OnNodeListItemClicked(o, e);
                    };
                }
            }


            /// <inheritdoc/>
            public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
            {
                View v;
                if (viewType == headingItemType)
                    v = LayoutInflater.From(parent.Context)!.Inflate(Resource.Layout.list_heading_item, parent, false)!;
                else
                    v = LayoutInflater.From(parent.Context)!.Inflate(Resource.Layout.list_menu_item, parent, false)!;
                return new DeviceListViewHolder(v);
            }
        }


        /// <summary>
        /// View holder for the heading or a device in the list.
        /// </summary>
        public class DeviceListViewHolder : RecyclerView.ViewHolder
        {
            /// <summary>
            /// Constructor. Initialise a DeviceListViewHolder object.
            /// </summary>
            /// <param name="view">Item view. A click on any of its views raises <see cref="Click"/>.</param>
            public DeviceListViewHolder(View view)
                : base(view)
            {
                this.ItemView = view;
                AttachClickHandler(view);
            }


            /// <summary>
            /// Fires when the item is clicked. The arguments contain only the position.
            /// </summary>
            public event EventHandler<DeviceListClickEventArgs>? Click;


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
                    view.Click += (o, e) =>
                    {
                        var ea = new DeviceListClickEventArgs() { Position = this.BindingAdapterPosition };
                        this.Click?.Invoke(this, ea);
                    };
                }
            }
        }


        /// <summary>
        /// Device click event arguments
        /// </summary>
        public class DeviceListClickEventArgs : EventArgs
        {
            /// <summary>Item position. The heading is at position 0.</summary>
            public int Position { get; set; }

            /// <summary>
            /// Device's Wear OS node ID
            /// </summary>
            public string DeviceId { get; set; } = string.Empty;

            /// <summary>
            /// Device's display name
            /// </summary>
            public string DeviceDisplayName { get; set; } = string.Empty;
        }
    }
}
