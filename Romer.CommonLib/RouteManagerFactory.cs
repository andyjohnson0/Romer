using Android.Content;

using Romer.DataLib;


namespace Romer.CommonLib
{
    /// <summary>
    /// Factory class for RouteManager
    /// </summary>
    public static class RouteManagerFactory
    {
        /// <summary>
        /// Create a RouteManager that stores routes in the app's private files directory.
        /// </summary>
        /// <param name="context">Context</param>
        /// <returns>RouteManager object</returns>
        public static RouteManager Create(Context context)
        {
            return new RouteManager(context.FilesDir?.Path ?? throw new InvalidOperationException("App has no files directory"));
        }
    }
}
