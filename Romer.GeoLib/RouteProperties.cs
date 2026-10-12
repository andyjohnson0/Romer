using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace Romer.GeoLib
{
    /// <summary>
    /// Properties of a route, calculated from its waypoints' locations: distance, ascent, descent and estimated
    /// walking time.
    /// </summary>
    public class RouteProperties
    {
        /// <summary>
        /// Total distance in kilometres, or null if not known.
        /// </summary>
        public decimal? TotalDistance { get; set; } = null;

        /// <summary>
        /// Total ascent in metres, or null if not known.
        /// </summary>
        public decimal? TotalAscent { get; set; } = null;

        /// <summary>
        /// Total descent in metres, or null if not known.
        /// </summary>
        public decimal? TotalDescent { get; set; } = null;

        /// <summary>
        /// Estimated walk time from Naismith's rule, or null if not known.
        /// </summary>
        public TimeSpan? TotalWalkTime { get; set; } = null;

        /// <summary>
        /// Leg properties.
        /// Number of elements will be one less than the number of waypoints, or zero if there are fewer than two waypoints.
        /// </summary>
        public IEnumerable<LegProperties> LegProperties { get; private set; } = Array.Empty<LegProperties>();


        /// <summary>
        /// Calculate route properties from a list of locations.
        /// Distance and walk time are null if there are fewer than two locations. Ascent and descent are null unless
        /// the route climbs or descends between locations that have altitudes.
        /// </summary>
        /// <param name="locations">The route's locations, in order</param>
        /// <returns>Route properties</returns>
        public static RouteProperties CreateFrom(Location[] locations)
        {
            var routeProps = new RouteProperties();

            if (locations.Length >= 2)
            {
                routeProps.TotalDistance = 0M;
                double? prevAltitude = null;

                var legProperties = new LegProperties[locations.Length - 1];
                for(int iLocation = 0; iLocation < locations.Length; iLocation ++)
                {
                    if (iLocation < locations.Length - 1)
                    {
                        // Distance
                        var distance = Utils.HaversineKm(locations[iLocation], locations[iLocation + 1]);
                        routeProps.TotalDistance += (decimal)distance;

                        legProperties[iLocation] = new LegProperties();
                        legProperties[iLocation].Distance = distance;
                    }
                    
                    // Altitude
                    if (locations[iLocation].Altitude is double altitude)
                    {
                        // Current location has an altitude
                        if (prevAltitude.HasValue)
                        {
                            // We also have a previous altitude.
                            // Calculate the difference and accumulate into TotalAscent or TotalDescent.
                            var delta = (decimal)(altitude - prevAltitude.Value);
                            if (delta > 0M)
                            {
                                routeProps.TotalAscent ??= 0M; // initialise to zero if null
                                routeProps.TotalAscent += delta;
                            }
                            else if (delta < 0M)
                            {
                                routeProps.TotalDescent ??= 0M; // initialise to zero if null
                                routeProps.TotalDescent += Math.Abs(delta);
                            }
                        }

                        prevAltitude = altitude;
                    }
                }
                routeProps.LegProperties = legProperties;

                // Time to walk route
                // Use naismiths rule (https://en.m.wikipedia.org/wiki/Naismith%27s_rule):
                // Allow one hour for every 3 miles (5 km) forward, plus an additional hour for every 2,000 feet (600 m) of ascent
                var walkTime = TimeSpan.Zero.Add(TimeSpan.FromHours((double)(routeProps.TotalDistance.Value / 5M)));
                if (routeProps.TotalAscent.HasValue && routeProps.TotalAscent > 0M)
                {
                    var ascentHours = routeProps.TotalAscent.Value / 600M;
                    walkTime += TimeSpan.FromHours((double)ascentHours);
                }
                routeProps.TotalWalkTime = TimeSpan.FromSeconds((int)walkTime.TotalSeconds);  // Truncate to seconds
            }

            return routeProps;
        }
    }


    /// <summary>
    /// Properties of a leg of a route.
    /// </summary>
    public class LegProperties
    {
        /// <summary>
        /// Distance in kilometres
        /// </summary>
        public double Distance { get; set; } = 0;
    }
}
