using System;
using System.Linq;
using System.Collections.Generic;

using Android.Content;
using Android.Views;
using Android.Widget;

using static AndroidX.RecyclerView.Widget.RecyclerView;
using Android.Content.Res;
using Android.Util;
using Android.Graphics.Drawables;

using AndroidX.AppCompat.Content.Res;
using AndroidX.RecyclerView.Widget;
using AndroidX.AppCompat.Widget;
using Romer.CommonLib;



namespace Romer.Mobile.App
{
    /// <summary>
    /// RecyclerView adapter for items identified by numeric IDs.
    /// Each item's layout is shown with a checkbox, and items are selected by long press or checkbox.
    /// The client fills in each item's layout in the <see cref="ItemBind"/> event.
    /// </summary>
    public class ItemListAdapter : RecyclerView.Adapter
    {
        /// <summary>
        /// Constructor. Initialise an ItemListAdapter object.
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="itemLayoutResId">Layout resource for each item</param>
        public ItemListAdapter(
            Context context,
            int itemLayoutResId)
        {
            this._itemLayoutResId = itemLayoutResId;
            this._context = context;
        }

        private readonly Context _context;
        private readonly int _itemLayoutResId;

        private const int itemContainerCheckBoxId = 10;

        private RecyclerView? _recyclerView;  // Set in OnAttachedToRecyclerView()

        private class ItemInfo
        {
            public int ItemId { get; set; }
            public bool IsSelected { get; set; } = false;
        }

        private List<ItemInfo> _items = new List<ItemInfo>();  // Indexed by position



        /// <summary>
        /// Fires when an item is clicked (a short press).
        /// </summary>
        public event EventHandler<ClickEventArgs>? ItemClick;

        /// <summary>
        /// Fires when an item's view is bound to an item. The client fills in the view from the item.
        /// </summary>
        public event EventHandler<BindItemEventArgs>? ItemBind;

        /// <summary>
        /// Fires when the set of selected items changes.
        /// </summary>
        public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;


        /// <summary>
        /// Get an item's position given its ID.
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <returns>Position of the item, or -1 if the item ID is not found</returns>
        public int GetPositionForItemId(int itemId)
        {
            return _items.FindIndex(i => i.ItemId == itemId);
        }


        /// <summary>
        /// Get/set item IDs, in display order. These are values that identify items in the RecyclerView.
        /// Setting item IDs keeps the selection of items that are still present. If that changes the selection,
        /// a <see cref="SelectionChanged"/> event fires, so that the client can update or finish its action mode.
        /// </summary>
        public IEnumerable<int> ItemIds
        {
            get
            {
                return _items.Select(i => i.ItemId).ToArray();
            }
            set
            {
                var previouslySelected = SelectedIds;
                _items = value.Select(id => new ItemInfo() { ItemId = id, IsSelected = previouslySelected.Contains(id) }).ToList();
                NotifyDataSetChanged();

                if (SelectionCount != previouslySelected.Length)
                {
                    var args = new ItemListAdapter.SelectionChangedEventArgs()
                    {
                        SelectionCount = this.SelectionCount
                    };
                    SelectionChanged?.Invoke(this, args);
                }
            }
        }


        /// <summary>
        /// Count of selected items. Read only.
        /// </summary>
        public int SelectionCount
        {
            get
            {
                return _items.Where(item => item.IsSelected).Count();
            }
        }


        /// <summary>
        /// IDs of the selected items, in display order. Read only.
        /// </summary>
        public int[] SelectedIds
        {
            get
            {
                return _items.Where(item => item.IsSelected).Select(item => item.ItemId).ToArray();
            }
        }


        /// <summary>
        /// Select all items. Fires <see cref="SelectionChanged"/> if the selection changes.
        /// </summary>
        public void SelectAll()
        {
            SetAllSelectionState(true);
        }


        /// <summary>
        /// Deselect all items. Fires <see cref="SelectionChanged"/> if the selection changes.
        /// </summary>
        public void DeselectAll()
        {
            SetAllSelectionState(false);
        }


        private void SetAllSelectionState(bool newSelectionState)
        {
            // Find the positions of items who's state needs to change
            var positionsToChange = _items.Select((item, index) => new { Item = item, Index = index }).Where(x => x.Item.IsSelected != newSelectionState).Select(x => x.Index);
            if (positionsToChange.Count() > 0)
            {
                // Iterate over each, setting the checkbox checked state.
                foreach (var pos in positionsToChange)
                {
                    _items[pos].IsSelected = newSelectionState;
                    var vh = _recyclerView?.FindViewHolderForAdapterPosition(pos) as ItemListAdapterViewHolder;
                    if (vh?.ItemView != null)
                    {
                        // Set the checkbox selection state, ensuring that the event handler eats the event.
                        vh.MaskCheckedChangeEvent = true;
                        vh.ItemView.RequireViewById<CheckBox>(itemContainerCheckBoxId).Checked = newSelectionState;
                        vh.MaskCheckedChangeEvent = false;
                    }
                }

                var args = new ItemListAdapter.SelectionChangedEventArgs()
                {
                    SelectionCount = this.SelectionCount
                };
                SelectionChanged?.Invoke(this, args);
            }
        }



        #region RecyclerView.Adapter

        /// <inheritdoc/>
        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            // Create a background drawable for the container that is the equivalent of the layout xml:
            //     android:background = "?android:attr/selectableItemBackground"
            // This gives us a ripple effect on select.
            var outValue = new TypedValue();
            _context.Theme?.ResolveAttribute(Android.Resource.Attribute.SelectableItemBackground, outValue, true);
            var itemContainerBackground = AppCompatResources.GetDrawable(_context, outValue.ResourceId);

            // Create the top-level container. This will hold the checkbox and the layout supplied to the ctor.
            var marginPx = _context.RequireResources().GetDimensionPixelSize(Resource.Dimension.listItemMargin);
            var paddingPx = _context.RequireResources().GetDimensionPixelSize(Resource.Dimension.listItemPadding);
            var itemContainerLayoutParams = new LinearLayout.LayoutParams(width: LinearLayout.LayoutParams.MatchParent, height: LinearLayout.LayoutParams.WrapContent);
            itemContainerLayoutParams.SetMargins(left: 0, top: marginPx, right: 0, marginPx);
            var itemContainer = new LinearLayout(this._context)
            {
                LayoutParameters = itemContainerLayoutParams,
                Orientation = Android.Widget.Orientation.Horizontal,
                Background = itemContainerBackground
            };
            itemContainer.SetPadding(left: paddingPx, top: paddingPx, right: paddingPx, bottom: paddingPx);

            // Add a checkbox to the container.
            var cbx = new CheckBox(this._context)
            {
                Id = itemContainerCheckBoxId,
                LayoutParameters = new ViewGroup.LayoutParams(width: ViewGroup.LayoutParams.WrapContent, height: ViewGroup.LayoutParams.WrapContent),
            };
            itemContainer.AddView(cbx);

            // Inflate the layout and add to the container.
            var itemView = LayoutInflater.From(_context)!.Inflate(_itemLayoutResId, parent, false)!;
            if (itemView.FindViewById(itemContainerCheckBoxId) != null)
                throw new InvalidOperationException($"The item layout contains a view with ID {itemContainerCheckBoxId}, which the adapter uses for its checkbox");
            itemContainer.AddView(itemView);

            // Create view holder and set-up event handlers for the container elements.
            var vh = new ItemListAdapterViewHolder(itemContainer);
            itemContainer.Click += (o,e) => { OnItemClicked(vh); };
            itemContainer.LongClick += (o,e) => { OnItemLongClicked(vh); };
            cbx.CheckedChange += (o,e) => { OnItemCheckedChange(vh); };

            return vh;
        }


        /// <inheritdoc/>
        public override void OnBindViewHolder(RecyclerView.ViewHolder viewHolder, int position)
        {
            var vh = (ItemListAdapterViewHolder)viewHolder;

            // Recycled view holders keep the previous item's checkbox state, so always set it.
            vh.MaskCheckedChangeEvent = true;
            vh.ItemView.RequireViewById<CheckBox>(itemContainerCheckBoxId).Checked = _items[position].IsSelected;
            vh.MaskCheckedChangeEvent = false;

            var ea = new ItemListAdapter.BindItemEventArgs()
            {
                ItemView = vh.ItemView,
                ItemId = _items[position].ItemId
            };
            ItemBind?.Invoke(this, ea);
        }


        /// <inheritdoc/>
        public override int ItemCount
        {
            get { return _items.Count; }
        }


        /// <inheritdoc/>
        public override void OnAttachedToRecyclerView(RecyclerView recyclerView)
        {
            base.OnAttachedToRecyclerView(recyclerView);
            this._recyclerView = recyclerView;
        }


        /// <inheritdoc/>
        public override void OnDetachedFromRecyclerView(RecyclerView recyclerView)
        {
            base.OnDetachedFromRecyclerView(recyclerView);
            this._recyclerView = null;
        }

        #endregion RecyclerView.Adapter


        #region Implementation

        /// <summary>
        /// Called when an item is clicked
        /// </summary>
        /// <param name="vh">Item view holder</param>
        private void OnItemClicked(ItemListAdapterViewHolder vh)
        {
            if (vh.BindingAdapterPosition == RecyclerView.NoPosition)
                return;
            var ea = new ClickEventArgs()
            {
                ItemView = vh.ItemView,
                Position = vh.BindingAdapterPosition
            };
            this.ItemClick?.Invoke(this, ea);
        }


        /// <summary>
        /// Called when an item is long-clicked
        /// </summary>
        /// <param name="vh">Item view holder</param>
        private void OnItemLongClicked(ItemListAdapterViewHolder vh)
        {
            // Toggle checkbox checked state.
            // Fires checked changed event and calls OnItemCheckedChange() to do the actual work.
            var cbx = vh.ItemView.RequireViewById<CheckBox>(itemContainerCheckBoxId);
            cbx.Checked = !cbx.Checked;  
        }


        /// <summary>
        /// Called when an item's checkbox has changed its checked state
        /// </summary>
        /// <param name="vh">Item view holder</param>
        private void OnItemCheckedChange(ItemListAdapterViewHolder vh)
        {
            if (vh.MaskCheckedChangeEvent || vh.BindingAdapterPosition == RecyclerView.NoPosition)
                return;

            // Update the items collection
            var cbx = vh.ItemView.RequireViewById<CheckBox>(itemContainerCheckBoxId);
            _items[vh.BindingAdapterPosition].IsSelected = cbx.Checked;

            // Fire a SelectionChanged event
            var args = new ItemListAdapter.SelectionChangedEventArgs()
            {
                SelectionCount = this.SelectionCount
            };
            SelectionChanged?.Invoke(this, args);
        }

        #endregion Implementation



        #region Event Args

        /// <summary>
        /// Item click event arguments
        /// </summary>
        public class ClickEventArgs : EventArgs
        {
            /// <summary>Item view</summary>
            public required View ItemView { get; init; }

            /// <summary>Item position</summary>
            public int Position { get; set; }
        }


        /// <summary>
        /// Bind item event arguments
        /// </summary>
        public class BindItemEventArgs : EventArgs
        {
            /// <summary>Item view</summary>
            public required View ItemView { get; init; }

            /// <summary>Item ID</summary>
            public int ItemId { get; set; }
        }


        /// <summary>
        /// Selection changed event arguments
        /// </summary>
        public class SelectionChangedEventArgs : EventArgs
        {
            /// <summary>Selection count</summary>
            public int SelectionCount { get; set; }
        }

        #endregion Event Args


        private class ItemListAdapterViewHolder : RecyclerView.ViewHolder
        {
            public ItemListAdapterViewHolder(View itemView)
                : base(itemView)
            {
            }


            public bool MaskCheckedChangeEvent { get; set; } = false;
        }
    }
}
