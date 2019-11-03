namespace Net.Bluewalk.AnyRestApi2Mqtt.Models
{
    public class MqttSubscription
    {
        public string Topic { get; set; }
        public Api Api { get; set; }
        public Endpoint.Endpoint Endpoint { get; set; }
    }
}