using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Romer.GeoLib
{
    /// <summary>
    /// Length units
    /// </summary>
    public enum LengthUnit
    {
        /// <summary>Metres</summary>
        Metre = 0,
        /// <summary>Kilometres</summary>
        Kilometre = 1,
        /// <summary>Feet</summary>
        Foot = 3,
        /// <summary>Miles</summary>
        Mile = 4
    }


    /// <summary>
    /// Unit conversion
    /// </summary>
    public static class UnitConverter
    {
        /// <summary>
        /// Convert a length from one unit to another.
        /// </summary>
        /// <param name="fromValue">Value to convert from</param>
        /// <param name="fromUnit">Unit to convert from</param>
        /// <param name="toUnit">Unit to convert to</param>
        /// <returns>Converted value</returns>
        public static decimal Convert(
            decimal fromValue,
            LengthUnit fromUnit,
            LengthUnit toUnit)
        {
            decimal toValue = (fromUnit, toUnit) switch
            {
                //
                (LengthUnit.Metre, LengthUnit.Kilometre) => fromValue * 0.001M,
                (LengthUnit.Metre, LengthUnit.Foot) => fromValue * 3.2808399M,
                (LengthUnit.Metre, LengthUnit.Mile) => fromValue * 0.00062137M,
                //
                (LengthUnit.Kilometre, LengthUnit.Metre) => fromValue * 1000M,
                (LengthUnit.Kilometre, LengthUnit.Foot) => fromValue * 3280.8399M,
                (LengthUnit.Kilometre, LengthUnit.Mile) => fromValue * 0.62137119M,
                //
                (LengthUnit.Foot, LengthUnit.Metre) => fromValue * 0.3048M,
                (LengthUnit.Foot, LengthUnit.Kilometre) => fromValue * 0.0003048M,
                (LengthUnit.Foot, LengthUnit.Mile) => fromValue * 0.00018939M,
                //
                (LengthUnit.Mile, LengthUnit.Metre) => fromValue * 1609.344M,
                (LengthUnit.Mile, LengthUnit.Kilometre) => fromValue * 1.609344M,
                (LengthUnit.Mile, LengthUnit.Foot) => fromValue * 5280M,
                //
                _ => fromValue
            };
            return toValue;
        }
    }
}
