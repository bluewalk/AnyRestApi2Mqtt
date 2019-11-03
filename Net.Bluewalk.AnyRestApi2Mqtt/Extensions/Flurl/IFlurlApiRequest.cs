using Flurl.Http;
using Net.Bluewalk.AnyRestApi2Mqtt.Models;
using Net.Bluewalk.AnyRestApi2Mqtt.Models.Endpoint;

namespace Net.Bluewalk.AnyRestApi2Mqtt.Extensions.Flurl
{
    public interface IFlurlApiRequest : IFlurlRequest
    {
        Api Api { get; set; }
        Endpoint Endpoint { get; set; }
    }
}