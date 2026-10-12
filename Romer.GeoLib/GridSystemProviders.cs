using System;
using System.Collections.Generic;
using System.Linq;

namespace Romer.GeoLib
{
    /// <summary>
    /// The available grid system providers, in order of preference.
    /// </summary>
    public static class GridSystemProviders
    {
        private static readonly IGridSystemProvider[] _providers = new IGridSystemProvider[]
        {
            // National grids. Their areas don't overlap.
            new BritishGridSystemProvider(),
            new IrishGridSystemProvider(),
            new UsngGridSystemProvider(),

            // Universal grids
            new UtmGridSystemProvider(),
            new LatLonGridSystemProvider()
        };


        /// <summary>
        /// Get all providers, in order of preference.
        /// </summary>
        /// <returns>Providers</returns>
        public static IEnumerable<IGridSystemProvider> GetProviders()
        {
            return _providers;
        }


        /// <summary>
        /// Find a provider by its tag.
        /// </summary>
        /// <param name="tag">Provider tag</param>
        /// <returns>The provider, or null if no provider has the tag (for example a tag saved by an older version)</returns>
        public static IGridSystemProvider? FindByTag(string tag)
        {
            return _providers.FirstOrDefault(p => p.Tag == tag);
        }


        /// <summary>
        /// Get the most specific provider for a location: a national grid if one applies, otherwise a universal one.
        /// </summary>
        /// <param name="location">Location</param>
        /// <returns>Provider. There is always one, because <see cref="LatLonGridSystemProvider"/> applies everywhere.</returns>
        public static IGridSystemProvider BestInArea(Location location)
        {
            return _providers.First(p => p.IsInArea(location));  // LatLonGridSystemProvider applies everywhere
        }


        /// <summary>
        /// Get all providers that apply to a location, in order of preference.
        /// </summary>
        /// <param name="location">Location</param>
        /// <returns>Providers. There is always at least one.</returns>
        public static IEnumerable<IGridSystemProvider> AllInArea(Location location)
        {
            return _providers.Where(p => p.IsInArea(location));
        }
    }
}
