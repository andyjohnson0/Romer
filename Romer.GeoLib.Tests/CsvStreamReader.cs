using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Romer.GeoLib.Tests
{
    /// <summary>
    /// Reads simple comma-separated test data. Values can't contain commas or quotes.
    /// </summary>
    internal class CsvStreamReader : StreamReader
    {
        /// <summary>
        /// Constructor. Initialise a CsvStreamReader object.
        /// </summary>
        /// <param name="stm">Stream to read</param>
        public CsvStreamReader(Stream stm)
            : base(stm)
        {
        }


        /// <summary>
        /// Read the values on the next line that has any. Everything after a '#' is a comment, and blank lines are
        /// skipped.
        /// </summary>
        /// <returns>Values, trimmed, or null at the end of the stream</returns>
        public string[]? ReadValues()
        {
            while (!this.EndOfStream)
            {
                var line = this.ReadLine();
                if (line == null)
                    return null;

                // Strip comments starting at first instance pf # character
                var iComment = line.IndexOf('#');
                if (iComment != -1)
                {
                    line = line.Substring(0, iComment);
                }

                // Skip empty lines
                line = line.Trim();
                if (line == string.Empty)
                    continue;

                // Split line and return
                var values = line.Split(',', StringSplitOptions.TrimEntries);
                return values;
            }

            return null;  // end of stream
        }
    }
}
