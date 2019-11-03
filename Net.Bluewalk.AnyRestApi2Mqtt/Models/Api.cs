using System.Collections.Generic;
using System.IO;
using Net.Bluewalk.AnyRestApi2Mqtt.Models.Endpoint;
using YamlDotNet.Serialization;

namespace Net.Bluewalk.AnyRestApi2Mqtt.Models
{
    public class Api
    {
        public string Name { get; set; }
        [YamlMember(Alias = "base-url", ApplyNamingConventions = false)]
        public string BaseUrl { get; set; }
        [YamlMember(Alias = "base-topic", ApplyNamingConventions = false)]
        public string BaseTopic { get; set; }
        public Authentication Authentication { get; set; }
        public Dictionary<string, Endpoint.Endpoint> Endpoints { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        [YamlMember(Alias = "on-start", ApplyNamingConventions = false)]
        public List<string> OnStart { get; set; }

        [YamlIgnore]
        public string AuthFile => Path.Combine(Directory.GetCurrentDirectory(), $"{Name}.auth");
        
        public void SetAuthToken(string token)
        {
            Authentication.Token = token;
            
            File.WriteAllText(AuthFile,
                Authentication.Token);
        }

        public void LoadAuthToken()
        {
            if (File.Exists(AuthFile))
                Authentication.Token = File.ReadAllText(AuthFile);
        }
    }
}