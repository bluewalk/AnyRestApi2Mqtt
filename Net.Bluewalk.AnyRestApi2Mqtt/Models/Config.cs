using System.Collections.Generic;

namespace Net.Bluewalk.AnyRestApi2Mqtt.Models
{
    public class Config
    {
        public Mqtt Mqtt { get; set; }
        public List<Api> Apis { get; set; }
    }
}