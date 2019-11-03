using System;
using Net.Bluewalk.AnyRestApi2Mqtt.Enums;

namespace Net.Bluewalk.AnyRestApi2Mqtt.Models.Endpoint
{
    public class AuthenticationToken
    {
        public AuthenticationType Type { get; set; }
        public string Token { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}