using System.Text;

using Android.Gms.Wearable;

using Romer.DataLib;


namespace Romer.CommonLib
{
    /// <summary>
    /// The Wear OS Data Layer contract between the mobile and wearable apps: the capability they advertise and the
    /// message paths they use.
    /// A request path is a verb followed by a noun, e.g. <see cref="Put"/> + <see cref="Data_Route"/>.
    /// A reply uses the noun alone.
    /// </summary>
    public static class WearMessage
    {
        /// <summary>
        /// Capability that both apps advertise (see Resources/values/wear.xml), used to find each other.
        /// </summary>
        public const string RouteManagerCapability = "Romer.App.RouteManager";

        // Verbs

        /// <summary>Verb: request the noun's data. The reply's path is the noun alone, and its payload is the data.</summary>
        public const string Get = "/get";

        /// <summary>Verb: send the noun's data as the message's payload.</summary>
        public const string Put = "/put";

        // Nouns

        /// <summary>Noun: a route, as JSON made by <see cref="Route.ToJson"/>.</summary>
        public const string Data_Route = "/data/route";

        /// <summary>Noun: a device's diagnostic information, as JSON made by <see cref="DiagnosticInfo.ToJson"/>.</summary>
        public const string Config_Diagnostics = "/config/diagnostics";

        /// <summary>
        /// How long to wait for a reply to a request.
        /// </summary>
        public static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(20);


        /// <summary>
        /// Read the route from a <see cref="Put"/> + <see cref="Data_Route"/> message.
        /// </summary>
        /// <param name="messageEvent">Received message</param>
        /// <returns>The route. Its ID and its waypoints' IDs are <see cref="RouteManager.IdNull"/>.</returns>
        /// <exception cref="InvalidDataException">The message doesn't contain a route</exception>
        /// <exception cref="System.Text.Json.JsonException">The message's payload isn't valid JSON for a route</exception>
        public static Route ReadRoute(IMessageEvent messageEvent)
        {
            var json = Encoding.UTF8.GetString(messageEvent.GetData() ?? Array.Empty<byte>());
            return Route.FromJson(json) ?? throw new InvalidDataException("Route message is empty");
        }
    }
}
