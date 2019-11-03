using System.Collections.Generic;
using System.IO;
using Net.Bluewalk.AnyRestApi2Mqtt.Enums;
using YamlDotNet.Serialization;

namespace Net.Bluewalk.AnyRestApi2Mqtt.Models.Endpoint
{
    public class Authentication : Endpoint
    {
        public AuthenticationType Type { get; set; }
        
        public Dictionary<string, string> Body { get; set; }
        
        [YamlMember(Alias = "body-encoding", ApplyNamingConventions = false)]
        public BodyEncoding BodyEncoding { get; set; }

        [YamlMember(Alias = "token-path", ApplyNamingConventions = false)]
        public string TokenPath { get; set; }

        [YamlMember(Alias = "basic-username", ApplyNamingConventions = false)]
        public string BasicUsername { get; set; }
        
        [YamlMember(Alias = "basic-password", ApplyNamingConventions = false)]
        public string BasicPassword { get; set; }
        
        [YamlMember(Alias = "header-name", ApplyNamingConventions = false)]
        public string HeaderName { get; set; }
        
        [YamlIgnore]
        public string Token { get; set; }
    }

}