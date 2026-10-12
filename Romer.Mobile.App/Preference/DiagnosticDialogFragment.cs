using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Android.Content;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;

using Android.Gms.Wearable;

using Romer.CommonLib;


namespace Romer.Mobile.App.Preference
{
    /// <summary>
    /// Dialog that collects diagnostic information from this phone and, if selected, from each reachable watch
    /// running Romer, and copies it to the clipboard.
    /// </summary>
    public class DiagnosticDialogFragment : AndroidX.Fragment.App.DialogFragment
    {
        /// <summary>
        /// A device that can provide diagnostic information.
        /// </summary>
        /// <param name="NodeId">Wear OS node ID, or null for this phone</param>
        /// <param name="DisplayName">Name shown in the device list and the diagnostic text</param>
        private sealed record Device(string? NodeId, string DisplayName);

        private Device[] _devices = Array.Empty<Device>();


        /// <inheritdoc/>
        public override View OnCreateView(LayoutInflater? inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            return inflater!.Inflate(Resource.Layout.diagnostic_dialog, container, false)!;
        }


        /// <inheritdoc/>
        public override void OnStart()
        {
            base.OnStart();

            // A dialog fragment's window wraps its content by default, which makes this dialog very narrow.
            Dialog?.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
        }


        /// <inheritdoc/>
        public override void OnViewCreated(View? view, Bundle? savedInstanceState)
        {
            base.OnViewCreated(view!, savedInstanceState);
            if (view == null)
                return;

            var copyBtn = view.RequireViewById<ImageButton>(Resource.Id.copyBtn);
            copyBtn.Enabled = false;
            copyBtn.Click += async (o, e) => await CopyDiagnosticsAsync();

            _ = LoadDeviceListAsync();
        }


        /// <summary>
        /// Populate the device list with this phone and the reachable watches running Romer.
        /// </summary>
        private async Task LoadDeviceListAsync()
        {
            var context = RequireContext()!;
            SetBusy(true);

            var deviceList = new List<Device>()
            {
                new Device(null, context.GetFormattedString(Resource.String.diagnosticsMobileDeviceListItem, Build.Manufacturer ?? "", Build.Model ?? ""))
            };
            try
            {
                using var capabilityClient = WearableClass.GetCapabilityClient(context);
                var capabilityInfo = await capabilityClient
                    .GetCapabilityAsync(WearMessage.RouteManagerCapability, CapabilityClient.FilterReachable)
                    .WaitAsync(WearMessage.ReplyTimeout);
                foreach (var node in capabilityInfo?.Nodes ?? Enumerable.Empty<INode>())
                {
                    if (node.Id != null)
                        deviceList.Add(new Device(node.Id, context.GetFormattedString(Resource.String.diagnosticsWearDeviceListItem, node.DisplayName ?? node.Id)));
                }
            }
            catch (Exception ex)
            {
                Log.Warn("LOC", $"Couldn't find watches: {ex}");
                Toast.MakeText(context, Resource.String.diagnosticsNoWatchesMsg, ToastLength.Long)?.Show();
            }

            if (!IsAdded || View == null)
                return;  // Dismissed while waiting
            this._devices = deviceList.ToArray();

            var listView = View.RequireViewById<ListView>(Resource.Id.deviceList);
            listView.Adapter = new ArrayAdapter<string>(context, Android.Resource.Layout.SimpleListItemMultipleChoice, _devices.Select(d => d.DisplayName).ToArray());
            for (var i = 0; i < _devices.Length; i++)
            {
                listView.SetItemChecked(i, true);
            }

            View.RequireViewById<ImageButton>(Resource.Id.copyBtn).Enabled = true;
            SetBusy(false);
        }


        /// <summary>
        /// Collect diagnostic information from the selected devices, copy it to the clipboard, and close the dialog.
        /// </summary>
        private async Task CopyDiagnosticsAsync()
        {
            var context = RequireContext()!;
            var listView = RequireView()!.RequireViewById<ListView>(Resource.Id.deviceList);
            var selected = _devices.Where((d, i) => listView.IsItemChecked(i)).ToArray();
            if (selected.Length == 0)
                return;

            SetBusy(true);
            var sb = new StringBuilder();
            foreach (var device in selected)
            {
                sb.AppendLine(context.GetFormattedString(Resource.String.diagnosticsDeviceHeader, device.DisplayName));
                if (device.NodeId == null)
                {
                    sb.AppendLine(DiagnosticInfo.CreateFrom(context).ToJson(humanReadable: true));
                }
                else
                {
                    try
                    {
                        sb.AppendLine(await RequestWatchDiagnosticsAsync(device.NodeId, WearMessage.ReplyTimeout));
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("LOC", $"Couldn't get diagnostics from {device.DisplayName}: {ex}");
                        var reason = ex is TimeoutException ? context.GetString(Resource.String.diagnosticsWatchNoReply) : ex.Message;
                        sb.AppendLine(context.GetFormattedString(Resource.String.diagnosticsWatchFailed, reason));
                    }
                }
                sb.AppendLine();
            }

            if (!IsAdded)
                return;  // Dismissed while waiting

            var clipboard = (ClipboardManager?)context.GetSystemService(Context.ClipboardService);
            clipboard?.PrimaryClip = ClipData.NewPlainText(context.GetString(Resource.String.diagnosticsClipLabel), sb.ToString());

            // Android 13 and later show their own confirmation when an app copies to the clipboard.
            if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                Toast.MakeText(context, Resource.String.diagnosticsCopiedMsg, ToastLength.Short)?.Show();
            }
            Dismiss();
        }


        /// <summary>
        /// Ask a watch for its diagnostic information.
        /// The watch's WearMessageListenerService replies with a Config_Diagnostics message containing JSON.
        /// </summary>
        /// <param name="nodeId">Watch node ID</param>
        /// <param name="timeout">Maximum time to wait for the reply</param>
        /// <returns>Diagnostic information as JSON</returns>
        /// <exception cref="TimeoutException">The watch didn't reply in time</exception>
        private static async Task<string> RequestWatchDiagnosticsAsync(string nodeId, TimeSpan timeout)
        {
            var messageClient = ThisApp.Instance().MessageClient;
            var listener = new ReplyListener(nodeId, WearMessage.Config_Diagnostics);
            await messageClient.AddListenerAsync(listener);
            try
            {
                await messageClient.SendMessageAsync(nodeId, WearMessage.Get + WearMessage.Config_Diagnostics, Array.Empty<byte>());
                return await listener.Reply.WaitAsync(timeout);
            }
            finally
            {
                await messageClient.RemoveListenerAsync(listener);
            }
        }


        /// <summary>
        /// Listens for a reply message with a given path from a given node.
        /// </summary>
        private sealed class ReplyListener : Java.Lang.Object, MessageClient.IOnMessageReceivedListener
        {
            public ReplyListener(string nodeId, string path)
            {
                this._nodeId = nodeId;
                this._path = path;
            }

            private readonly string _nodeId;
            private readonly string _path;
            private readonly TaskCompletionSource<string> _tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<string> Reply => _tcs.Task;

            public void OnMessageReceived(IMessageEvent messageEvent)
            {
                if (messageEvent.Path == _path && messageEvent.SourceNodeId == _nodeId)
                {
                    _tcs.TrySetResult(Encoding.UTF8.GetString(messageEvent.GetData() ?? Array.Empty<byte>()));
                }
            }
        }


        private void SetBusy(bool busy)
        {
            View?.RequireViewById<View>(Resource.Id.progress).Visibility = busy ? ViewStates.Visible : ViewStates.Invisible;
            View?.RequireViewById<ImageButton>(Resource.Id.copyBtn).Enabled = !busy && _devices.Length > 0;
        }
    }
}
