using System.Text.RegularExpressions;
using Net.Bluewalk.AnyRestApi2Mqtt.Enums;

namespace Net.Bluewalk.AnyRestApi2Mqtt.Models.Endpoint
{
    public class Mqtt
    {
        public string Topic { get; set; }
        public string TopicRegex => Regex.Replace(Topic, "%(.*?)%", "(?<$1>([a-zA-Z0-9-_]+))")
            .Replace("/", "\\/");
        public string TopicSubscribe => Regex.Replace(Topic, "%(.*?)%", "+");
        public MqttAction Action { get; set; }
    }
}