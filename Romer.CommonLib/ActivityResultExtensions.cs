using AndroidX.Activity;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;


namespace Romer.CommonLib
{
    /// <summary>
    /// Typed wrappers for the AndroidX Activity Result API.
    /// Register in <c>OnCreate()</c>, unconditionally and in the same order every time, so that results delivered after the
    /// activity is recreated reach the right callback. Callbacks run on the UI thread.
    /// </summary>
    public static class ActivityResultExtensions
    {
        /// <summary>
        /// Register a callback for the result of an activity that the returned launcher starts with an Intent.
        /// </summary>
        /// <param name="activity">Activity</param>
        /// <param name="callback">Receives the activity's result</param>
        /// <returns>Launcher. Call <c>Launch(intent)</c>.</returns>
        public static ActivityResultLauncher RegisterForActivityResult(this ComponentActivity activity, Action<ActivityResult> callback)
        {
            return activity.RegisterForResult<ActivityResult>(new ActivityResultContracts.StartActivityForResult(), result => callback(result!));
        }


        /// <summary>
        /// Register a callback for the result of a single permission request.
        /// </summary>
        /// <param name="activity">Activity</param>
        /// <param name="callback">Receives true if the permission was granted</param>
        /// <returns>Launcher. Call <c>Launch(permission)</c>.</returns>
        public static ActivityResultLauncher RegisterForPermissionResult(this ComponentActivity activity, Action<bool> callback)
        {
            return activity.RegisterForResult<Java.Lang.Boolean>(new ActivityResultContracts.RequestPermission(), granted => callback(granted?.BooleanValue() == true));
        }


        /// <summary>
        /// Register a callback for a result of any contract.
        /// </summary>
        /// <typeparam name="TResult">The contract's Java result type</typeparam>
        /// <param name="activity">Activity</param>
        /// <param name="contract">Contract</param>
        /// <param name="callback">Receives the result, or null if the contract produced none</param>
        /// <returns>Launcher</returns>
        public static ActivityResultLauncher RegisterForResult<TResult>(this ComponentActivity activity, ActivityResultContract contract, Action<TResult?> callback)
            where TResult : Java.Lang.Object
        {
            return activity.RegisterForActivityResult(contract, new Callback(result => callback(result as TResult)));
        }


        // Java callable wrappers can't be generic, so this takes an untyped result.
        private sealed class Callback : Java.Lang.Object, IActivityResultCallback
        {
            public Callback(Action<Java.Lang.Object?> action)
            {
                this._action = action;
            }

            private readonly Action<Java.Lang.Object?> _action;

            public void OnActivityResult(Java.Lang.Object? result)
            {
                _action(result);
            }
        }
    }
}
