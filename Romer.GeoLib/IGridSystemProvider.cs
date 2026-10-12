using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Romer.GeoLib
{
    /// <summary>
    /// A grid system, such as a national grid, that converts locations to grid references.
    /// </summary>
    public interface IGridSystemProvider
    {
        /// <summary>
        /// Determine if the grid system applies to a location.
        /// </summary>
        /// <param name="location">Location</param>
        /// <returns>True if the grid system applies to the location, otherwise false</returns>
        bool IsInArea(Location location);

        /// <summary>
        /// Get the grid reference of a location.
        /// </summary>
        /// <param name="location">Location</param>
        /// <param name="precisionMetres">Precision in metres: 1, 10, 100, etc.</param>
        /// <returns>Grid reference</returns>
        /// <exception cref="ArgumentException">The location or precision is out of range</exception>
        string GetGridReference(
            Location location,
            int precisionMetres);

        /// <summary>
        /// Short identifier for the grid system, e.g. "OSGB", used to save and restore the user's choice of grid.
        /// </summary>
        string Tag { get; }
    }
}
