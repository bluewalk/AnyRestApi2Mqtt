using Flurl;
using Flurl.Http;
using Net.Bluewalk.AnyRestApi2Mqtt.Models;
using Net.Bluewalk.AnyRestApi2Mqtt.Models.Endpoint;

namespace Net.Bluewalk.AnyRestApi2Mqtt.Extensions.Flurl
{
    public static class Extensions
    {
        public static IFlurlApiRequest WithApiEndpoint(this IFlurlApiRequest req)
        {
            return req
                .WithHeader("User-Agent", "Bluewalk AnyRestApi2Mqtt");
        }

        public static IFlurlRequest WithApiEndpoint(this Url url, Api api, Endpoint endpoint) => new FlurlApiRequest(api, endpoint, url).WithApiEndpoint();

        public static IFlurlRequest WithApiEndpoint(this string url, Api api, Endpoint endpoint) => new FlurlApiRequest(api, endpoint, url).WithApiEndpoint();
    }
}