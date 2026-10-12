using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.Preference;
using Romer.CommonLib;


namespace Romer.Mobile.App.Preference
{
    /// <summary>
    /// Datastore class used by the preferences UI framework.
    /// Acts as a front-end to the <see cref="UserSettings"/> class, which does the actual persistence.
    /// Note that the Android preferences framework stores list preference values as strings by calling GetString() and PutString().
    /// </summary>
    public class PreferenceDataStore : AndroidX.Preference.PreferenceDataStore
    {
        /// <summary>
        /// Constructor. Initialise a PreferenceDataStore object.
        /// </summary>
        /// <param name="context">Context</param>
        public PreferenceDataStore(Context context)
        {
            this._context = context;
        }

        private readonly Context _context;


        /// <inheritdoc/>
        public override string? GetString(string? key, string? defValue)
        {
            var settings = UserSettings.Load(this._context);
            if (key == this._context.GetString(Resource.String.prefs_navigation_measurementUnitSystem_key))
                return ((int)settings.Navigation_MeasurementUnitSystem).ToString();
            else
                return defValue;
        }


        /// <inheritdoc/>
        public override void PutString(string? key, string? value)
        {
            if (key == null || value == null)
                return;
            var settings = UserSettings.Load(this._context);
            if (key == this._context.GetString(Resource.String.prefs_navigation_measurementUnitSystem_key))
                settings.Navigation_MeasurementUnitSystem = (MeasurementUnitSystem)int.Parse(value);
            settings.Save(this._context);
        }

    }
}
