using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Android.Content;
using Android.Content.Res;
using Android.Util;
using Kotlin;


using Romer.GeoLib;


namespace Romer.CommonLib
{
    /// <summary>
    /// Measurement unit system.
    /// Note: These values are persisted and should not change.
    /// </summary>
    public enum MeasurementUnitSystem
    {
        /// <summary>Kilometres and metres</summary>
        Metric = 0,
        /// <summary>Miles and feet</summary>
        UsImperial = 1
    }


    /// <summary>
    /// Formats lengths, with their units, for display.
    /// </summary>
    public static class ValueFormatter
    {
        /// <summary>
        /// Formatting options
        /// </summary>
        [Flags]
        public enum Options
        {
            /// <summary>Default behaviour</summary>
            None = 0,
            /// <summary>Allow unit downgrade (e.g. from km to m) if converted value less than 1</summary>
            AllowLesserUnit = 1,
            /// <summary>Use the name of the specified unit (e.g. "kilometres")</summary>
            UnitName = 2,
            /// <summary>Use the symbol of the specified unit (e.g. "m" for metres)</summary>
            UnitSymbol = 4,
            /// <summary>Omit space between value and unit name/symbol (if specified)</summary>
            Compact = 8,

            /// <summary>General defaults</summary>
            Default = Options.AllowLesserUnit | Options.UnitSymbol
        }


        /// <summary>
        /// Convert a length in a specified unit to a different unit and format it as a string, e.g. "1.5 km".
        /// Kilometres and miles are shown to one decimal place, metres and feet to none.
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="fromValue">Value to convert from</param>
        /// <param name="fromUnit">Unit to convert from</param>
        /// <param name="toUnit">Unit to convert to</param>
        /// <param name="options">Options</param>
        /// <returns>Formatted string</returns>
        /// <exception cref="ArgumentNullException">Argument null</exception>
        /// <exception cref="ArgumentException">Both <see cref="Options.UnitName"/> and <see cref="Options.UnitSymbol"/> are specified</exception>
        public static string ToString(
            Context context,
            decimal fromValue,
            LengthUnit fromUnit,
            LengthUnit toUnit,
            Options options = Options.Default)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if ((options & (Options.UnitName | Options.UnitSymbol)) == (Options.UnitName | Options.UnitSymbol))
                throw new ArgumentException("Only one of Options.UnitName and Options.UnitSymbol should be specified", nameof(options));

            // Build value string
            var toValue = UnitConverter.Convert(fromValue, fromUnit, toUnit);
            if (toValue < 1M && 
                (options & Options.AllowLesserUnit) != Options.None && 
                (toUnit == LengthUnit.Kilometre || toUnit == LengthUnit.Mile))
            {
                // Downgrade to a compatable lesser unit
                toUnit = toUnit switch
                {
                    LengthUnit.Kilometre => LengthUnit.Metre,
                    LengthUnit.Mile => LengthUnit.Foot,
                    _ => toUnit  // shouldn't happen
                };
                toValue = UnitConverter.Convert(fromValue, fromUnit, toUnit);
            }

            // Format value as a string. Unit determines number of decimal places.
            int numDp = toUnit switch
            {
                LengthUnit.Metre => 0,
                LengthUnit.Kilometre => 1,
                LengthUnit.Foot => 0,
                LengthUnit.Mile => 1,
                _ => 0
            };
            var toValueStr = toValue.ToString($"N{numDp}");

            // Unit suffix string
            var unitSuffix = ValueFormatter.ToString(context, toUnit, toValue, options);
            if (!string.IsNullOrEmpty(unitSuffix))
            {
                if ((options & Options.Compact) == Options.None)
                {
                    toValueStr += " ";
                }
                toValueStr += unitSuffix;
            }

            return toValueStr;
        }


        /// <summary>
        /// Get the name or symbol of a length unit, as chosen by the options.
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="unit">Unit</param>
        /// <param name="value">Value. Determines the pluralisation of the unit name if <see cref="Options.UnitName"/> is specified</param>
        /// <param name="options">Options</param>
        /// <returns>Unit name or symbol, or an empty string if the options specify neither</returns>
        public static string ToString(
            Context context,
            LengthUnit unit,
            decimal value = 1M,
            Options options = Options.Default)
        {
            var unitStr = string.Empty;
            if ((options & Options.UnitName) != Options.None)
            {
                unitStr = unit switch
                {
                    LengthUnit.Metre => context.RequireResources().GetQuantityString(Resource.Plurals.metre, (int)value),
                    LengthUnit.Kilometre => context.RequireResources().GetQuantityString(Resource.Plurals.kilometre, (int)value),
                    LengthUnit.Foot => context.RequireResources().GetQuantityString(Resource.Plurals.foot, (int)value),
                    LengthUnit.Mile => context.RequireResources().GetQuantityString(Resource.Plurals.mile, (int)value),
                    _ => ""
                };
            }
            else if ((options & Options.UnitSymbol) != Options.None)
            {
                unitStr = unit switch
                {
                    LengthUnit.Metre => context.GetString(Resource.String.metreSymbol),
                    LengthUnit.Kilometre => context.GetString(Resource.String.kilometreSymbol),
                    LengthUnit.Foot => context.GetString(Resource.String.footSymbol),
                    LengthUnit.Mile => context.GetString(Resource.String.mileSymbol),
                    _ => ""
                };
            }
            return unitStr;
        }
    }
}
