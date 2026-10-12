using System;
using System.Text.Json.Serialization;

using SQLite;

namespace Romer.DataLib
{
    /// <summary>
    /// A waypoint in a route.
    /// Waypoints within a route are ordered by their database ID.
    /// </summary>
    [Table("Waypoints")]
    public class Waypoint
    {
        /// <summary>
        /// Database row ID, or <see cref="RouteManager.IdNull"/> if the waypoint has not been stored.
        /// </summary>
        [PrimaryKey, AutoIncrement, Column("_id")]
        [JsonIgnore]
        public int Id { get; set; } = RouteManager.IdNull;

        /// <summary>
        /// ID of the route that contains this waypoint.
        /// </summary>
        [Indexed]
        [JsonIgnore]
        public int RouteId { get; set; } = RouteManager.IdNull;

        /// <summary>
        /// Waypoint creation time (UTC). Local times are converted to UTC.
        /// </summary>
        public DateTime CreationTime
        {
            get { return _creationTime; }
            set { _creationTime = DateTimeUtc.From(value); }
        }
        private DateTime _creationTime;

        /// <summary>WGS84 latitude in decimal degrees.</summary>
        public double Latitude { get; set; }

        /// <summary>WGS84 longitude in decimal degrees.</summary>
        public double Longitude { get; set; }

        /// <summary>Altitude in metres, or null if not known.</summary>
        public double? Altitude { get; set; } = null;

        /// <summary>Waypoint label/name.</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Waypoint notes/description.</summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>True if the waypoint has been visited.</summary>
        public bool Ticked { get; set; } = false;
    }
}
