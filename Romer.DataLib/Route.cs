using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using SQLite;

namespace Romer.DataLib
{
    /// <summary>
    /// A route: a named, ordered list of waypoints.
    /// </summary>
    [Table("Routes")]
    public class Route
    {
        /// <summary>
        /// Database row ID, or <see cref="RouteManager.IdNull"/> if the route has not been stored.
        /// </summary>
        [PrimaryKey, AutoIncrement, Column("_id")]
        [JsonIgnore]
        public int Id { get; set; } = RouteManager.IdNull;

        /// <summary>
        /// Route creation time (UTC). Local times are converted to UTC.
        /// </summary>
        public DateTime CreationTime
        {
            get { return _creationTime; }
            set { _creationTime = DateTimeUtc.From(value); }
        }
        private DateTime _creationTime;

        /// <summary>
        /// Route label/name
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Route notes/description
        /// </summary>
        public string Notes { get; set; } = string.Empty;


        /// <summary>
        /// The route's waypoints, in order.
        /// Setting this property replaces the existing waypoints.
        /// </summary>
        [SQLite.Ignore]
        public IEnumerable<Waypoint> Waypoints
        {
            get
            {
                return _waypoints;
            }
            set
            {
                _waypoints = value.ToList();
            }
        }
        private List<Waypoint> _waypoints = new List<Waypoint>();


        /// <summary>
        /// Add a waypoint to the end of the route
        /// </summary>
        /// <param name="waypoint">Waypoint to add</param>
        /// <returns>The route</returns>
        public Route Add(Waypoint waypoint)
        {
            this._waypoints.Add(waypoint);
            return this;
        }


        /// <summary>
        /// Reverse the order of the waypoints.
        /// Call <see cref="RouteManager.UpdateRoute"/> to persist the change.
        /// </summary>
        /// <returns>The route</returns>
        public Route ReverseWaypoints()
        {
            this._waypoints.Reverse();
            return this;
        }


        /// <summary>
        /// Create a route from a JSON string made by <see cref="ToJson"/>.
        /// Route and waypoint IDs are not serialised, so they will be <see cref="RouteManager.IdNull"/>.
        /// </summary>
        /// <param name="json">JSON string representation</param>
        /// <returns>Route object, or null if the JSON represents a null value</returns>
        /// <exception cref="JsonException">The string isn't valid JSON for a route</exception>
        public static Route? FromJson(string json)
        {
            return JsonSerializer.Deserialize<Route>(json);
        }


        /// <summary>
        /// Serialise this route, including its waypoints, to a JSON string.
        /// Route and waypoint IDs are not serialised.
        /// </summary>
        /// <returns>Route serialised to JSON</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
