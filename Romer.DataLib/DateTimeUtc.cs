using System;

namespace Romer.DataLib
{
    /// <summary>
    /// Normalisation of stored times to UTC.
    /// </summary>
    internal static class DateTimeUtc
    {
        /// <summary>
        /// Normalise a DateTime to UTC.
        /// Local times are converted. Times of unspecified kind are assumed to already be UTC,
        /// which is the case for values read back from the database.
        /// </summary>
        /// <param name="dt">DateTime of any kind</param>
        /// <returns>UTC DateTime</returns>
        public static DateTime From(DateTime dt)
        {
            return dt.Kind switch
            {
                DateTimeKind.Utc => dt,
                DateTimeKind.Local => dt.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            };
        }
    }
}
