using System.Collections.Generic;
using System.Text.RegularExpressions;
using Net.Bluewalk.AnyRestApi2Mqtt.Enums;

namespace Net.Bluewalk.AnyRestApi2Mqtt.Models.Endpoint
{
    public class Endpoint
    {
        public string Path { get; set; }
        public RequestMethod Method { get; set; }
        public BodyEncoding Encoding { get; set; }
        public Mqtt Mqtt { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public string Selector { get; set; }
    }
}