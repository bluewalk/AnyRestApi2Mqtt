using Flurl;
using Flurl.Http;
using Net.Bluewalk.AnyRestApi2Mqtt.Models;
using Net.Bluewalk.AnyRestApi2Mqtt.Models.Endpoint;

namespace Net.Bluewalk.AnyRestApi2Mqtt.Extensions.Flurl
{
    public class FlurlApiRequest : FlurlRequest, IFlurlApiRequest
    {
        public Api Api { get; set; }
        public Endpoint Endpoint { get; set; }

        public FlurlApiRequest(Api api, Endpoint endpoint, Url url = null) : base(url)
        {
            Api = api;
            Endpoint = endpoint;
        }
    }
}