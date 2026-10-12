using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android;
using Android.Content;

using Romer.GeoLib;



namespace Romer.CommonLib
{
    /// <summary>
    /// The grid system providers in <see cref="GridSystemProviders"/>, with the app resources that describe them.
    /// </summary>
    public static class GridSystemProviderManager
    {
        /// <inheritdoc cref="GridSystemProviders.GetProviders"/>
        public static IEnumerable<IGridSystemProvider> GetProviders() => GridSystemProviders.GetProviders();

        /// <inheritdoc cref="GridSystemProviders.FindByTag"/>
        public static IGridSystemProvider? FindByTag(string tag) => GridSystemProviders.FindByTag(tag);

        /// <inheritdoc cref="GridSystemProviders.BestInArea"/>
        public static IGridSystemProvider BestInArea(Location location) => GridSystemProviders.BestInArea(location);

        /// <inheritdoc cref="GridSystemProviders.AllInArea"/>
        public static IEnumerable<IGridSystemProvider> AllInArea(Location location) => GridSystemProviders.AllInArea(location);

        /// <summary>
        /// Get the string resource that describes a provider's grid system.
        /// </summary>
        /// <param name="provider">Provider</param>
        /// <returns>String resource ID</returns>
        /// <exception cref="InvalidOperationException">Unknown provider type</exception>
        public static int GetDescriptionStringResId(IGridSystemProvider provider)
        {
            if (provider is LatLonGridSystemProvider)
                return Resource.String.wgs84ProviderDesc;
            else if (provider is BritishGridSystemProvider)
                return Resource.String.britishProviderDesc;
            else if (provider is UsngGridSystemProvider)
                return Resource.String.usngProviderDesc;
            else if (provider is IrishGridSystemProvider)
                return Resource.String.irishProviderDesc;
            else if (provider is UtmGridSystemProvider)
                return Resource.String.utmProviderDesc;
            else throw new InvalidOperationException($"Unknown provider type: {provider.GetType().Name}");
        }

        /// <summary>
        /// Get the icon for a provider's grid system: the country for a national grid, or a globe for a universal one.
        /// </summary>
        /// <param name="provider">Provider</param>
        /// <returns>Drawable resource ID</returns>
        /// <exception cref="InvalidOperationException">Unknown provider type</exception>
        public static int GetIconDrawableResId(IGridSystemProvider provider)
        {
            if (provider is BritishGridSystemProvider)
                return Resource.Drawable.ic_flag_gb;
            else if (provider is IrishGridSystemProvider)
                return Resource.Drawable.ic_ireland;
            else if (provider is UsngGridSystemProvider)
                return Resource.Drawable.ic_flag_us;
            else if (provider is UtmGridSystemProvider)
                return Resource.Drawable.ic_language;
            else if (provider is LatLonGridSystemProvider)
                return Resource.Drawable.ic_language;
            else throw new InvalidOperationException($"Unknown provider type: {provider.GetType().Name}");
        }
    }
}
