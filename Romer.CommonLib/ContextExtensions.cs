using Android.Content;
using Android.Content.Res;

namespace Romer.CommonLib
{
    /// <summary>
    /// Extension methods for <see cref="Context"/>.
    /// </summary>
    public static class ContextExtensions
    {
        /// <summary>
        /// Get a context's resources.
        /// <see cref="Context.Resources"/> is annotated as nullable, but it is never null for an activity, service or
        /// application.
        /// </summary>
        /// <param name="context">Context</param>
        /// <returns>Resources</returns>
        /// <exception cref="InvalidOperationException">The context has no resources</exception>
        public static Resources RequireResources(this Context context)
        {
            return context.Resources ?? throw new InvalidOperationException($"{context.GetType().Name} has no resources");
        }


        /// <summary>
        /// Get the app's version name, e.g. "1.0".
        /// </summary>
        /// <param name="context">Context</param>
        /// <returns>Version name, or an empty string if it isn't available</returns>
        public static string GetAppVersionName(this Context context)
        {
            var packageManager = context.PackageManager;
            var packageName = context.PackageName;
            if (packageManager == null || packageName == null)
                return string.Empty;

            Android.Content.PM.PackageInfo? info;
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                info = packageManager.GetPackageInfo(packageName, Android.Content.PM.PackageManager.PackageInfoFlags.Of(0L));
            }
            else
            {
#pragma warning disable CA1422 // Replaced by the overload above on API 33+
                info = packageManager.GetPackageInfo(packageName, 0);
#pragma warning restore CA1422
            }
            return info?.VersionName ?? string.Empty;
        }


        /// <summary>
        /// Get a string resource formatted with string arguments.
        /// Equivalent to <see cref="Context.GetString(int, Java.Lang.Object[])"/>, but avoids the nullable implicit
        /// conversion from string to Java.Lang.Object.
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="resId">String resource ID</param>
        /// <param name="args">Format arguments</param>
        /// <returns>Formatted string</returns>
        public static string GetFormattedString(this Context context, int resId, params string[] args)
        {
            return context.GetString(resId, args.Select(a => (Java.Lang.Object)new Java.Lang.String(a)).ToArray());
        }
    }
}
