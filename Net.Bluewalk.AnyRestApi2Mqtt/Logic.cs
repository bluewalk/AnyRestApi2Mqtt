using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Flurl.Http;
using Flurl.Util;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client.Options;
using MQTTnet.Extensions.ManagedClient;
using Net.Bluewalk.AnyRestApi2Mqtt.Enums;
using Net.Bluewalk.AnyRestApi2Mqtt.Extensions.Flurl;
using Net.Bluewalk.AnyRestApi2Mqtt.Models;
using Net.Bluewalk.AnyRestApi2Mqtt.Models.Endpoint;
using Net.Bluewalk.DotNetUtils.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Net.Bluewalk.AnyRestApi2Mqtt
{
    public class Logic : IHostedService
    {
        private readonly Config _config;
        private readonly IManagedMqttClient _mqttClient;
        private readonly ILogger _logger;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly List<MqttSubscription> _mqttSubscriptions;
        private readonly AsyncPolicy _requestPolicy;

        public Logic(ILogger<Logic> logger, IHostEnvironment hostEnvironment)
        {
            _logger = logger;
            _hostEnvironment = hostEnvironment;
            
            var fileName = Path.Combine(_hostEnvironment.ContentRootPath, "config.yml");

            if (!File.Exists(fileName))
                throw new FileNotFoundException("Config file not found", fileName);

            var yaml = File.ReadAllText(fileName);

            using (var input = new StringReader(yaml))
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                _config = deserializer.Deserialize<Config>(input);
            }

            _mqttSubscriptions = new List<MqttSubscription>();

            _mqttClient = new MqttFactory().CreateManagedMqttClient();
            _mqttClient.UseApplicationMessageReceivedHandler(MqttClientOnApplicationMessageReceived);
            _mqttClient.UseConnectedHandler(e => { _logger.LogInformation("Connected to MQTT server"); });
            _mqttClient.UseDisconnectedHandler(e =>
            {
                if (e.Exception != null && !e.ClientWasConnected)
                    _logger.LogError(e.Exception, "Unable to connect to MQTT server");

                if (e.Exception != null && e.ClientWasConnected)
                    _logger.LogError("Disconnected from connect to MQTT server with error", e.Exception);

                if (e.Exception == null && e.ClientWasConnected)
                    _logger.LogInformation("Disconnected from MQTT server");
            });

            _requestPolicy = Policy
                .Handle<FlurlHttpException>(r => r.Call.HttpStatus == HttpStatusCode.Unauthorized)
                .RetryAsync(1, async (exception, i) => 
                {
                    var call = (exception as FlurlHttpException)?.Call;
                    if (!(call?.FlurlRequest is FlurlApiRequest req)) return;

                    _logger.LogError("Access forbidden", exception);
                    _logger.LogInformation($"Authenticating for api {req.Api.Name}");

                    await ExecuteAuthentication(req.Api);
                });
        }

        /// <summary>
        /// Start logic
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting logic");

            var clientOptions = new MqttClientOptionsBuilder()
                .WithClientId($"AnyWebApi2Mqtt-{Environment.MachineName}-{Environment.UserName}")
                .WithTcpServer(_config.Mqtt.Host, _config.Mqtt.Port);

            if (!string.IsNullOrEmpty(_config.Mqtt.Username))
                clientOptions = clientOptions.WithCredentials(_config.Mqtt.Username,
                    _config.Mqtt.Password);

            var managedOptions = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(clientOptions);

            _logger.LogInformation($"Connecting to MQTT ({_config.Mqtt.Host}:{_config.Mqtt.Port})");
            await _mqttClient.StartAsync(managedOptions.Build());

            // Initializing topics etc
            _config.Apis.ToList()
                .ForEach(async a =>
                {
                    a.LoadAuthToken();
                    
                    a.Endpoints.Values.ToList()
                        .Where(e => !string.IsNullOrEmpty(e.Mqtt.Topic) && e.Mqtt.Action == MqttAction.Subscribe)
                        .ToList()
                        .ForEach(e => Subscribe(a, e));

                    a.Endpoints.ToList()
                        .Where(e => !string.IsNullOrEmpty(e.Value.Mqtt.Topic) &&
                                    e.Value.Mqtt.Action == MqttAction.Publish && a.OnStart.Contains(e.Key))
                        .ToList()
                        .ForEach(e => _requestPolicy.ExecuteAsync(() => ExecutePublishEndpoint(a, e.Value)));

                    await _mqttClient.SubscribeAsync(new TopicFilterBuilder().WithTopic($"{a.BaseTopic}/perform")
                        .Build());
                });
        }

        /// <summary>
        /// Stop logic
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Disconnecting from MQTT server");
            await _mqttClient?.StopAsync();
        }

        private async Task MqttClientOnApplicationMessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            _logger.LogInformation("Received MQTT message on topic {0}", e.ApplicationMessage.Topic);

            var messageHandled = false;

            // Handle perform topics
            _config.Apis.ToList()
                .ForEach(async a =>
                {
                    if (!e.ApplicationMessage.Topic.Equals($"{a.BaseTopic}/perform")) return;

                    await _requestPolicy.ExecuteAsync(() => ExecutePublishEndpoint(a, a.Endpoints[e.ApplicationMessage.ConvertPayloadToString()]));
                    messageHandled = true;
                });

            if (messageHandled) return;

            // Handle API Endpoint topics
            var subscription = _mqttSubscriptions.FirstOrDefault(m =>
                Regex.IsMatch(e.ApplicationMessage.Topic, $"{m.Api.BaseTopic}/{m.Endpoint.Mqtt.TopicRegex}"));
            if (subscription == null) return;

            _logger.LogInformation("Found subscription info");
            var parameters = Regex.Match(e.ApplicationMessage.Topic,
                    $"{subscription.Api.BaseTopic}/{subscription.Endpoint.Mqtt.TopicRegex}").Groups
                .Cast<Group>()
                .Skip(1)
                .ToDictionary(pair => pair.Name, pair => pair.Value);

            _logger.LogInformation("Found the following parameters: {0}",
                string.Join(",", parameters.Select(x => x.Key + "=" + x.Value).ToArray()));

            _logger.LogInformation("Set to execute action");
            
            await _requestPolicy.ExecuteAsync(() =>
                ExecuteEndpoint(subscription.Api, subscription.Endpoint, parameters, e.ApplicationMessage.Payload));
        }

        private async Task<HttpResponseMessage> ExecuteEndpoint(Api api, Endpoint endpoint,
            Dictionary<string, string> parameters = null, byte[] body = null)
        {
            var path = endpoint.Path;

            parameters?.ToList().ForEach(p => path = path.Replace($"%{p.Key}%", p.Value));

            var request = $"{api.BaseUrl}/{path}"
                .WithApiEndpoint(api, endpoint)
                .WithHeaders(api.Headers)
                .WithHeaders(endpoint.Headers);

            switch (api.Authentication.Type)
            {
                case AuthenticationType.Basic:
                    request = request.WithBasicAuth(api.Authentication.BasicUsername, api.Authentication.BasicPassword);
                    break;
                case AuthenticationType.Bearer:
                    request = request.WithOAuthBearerToken(api.Authentication.Token);
                    break;
                case AuthenticationType.Header:
                    request = request.WithHeader(api.Authentication.HeaderName, api.Authentication.Token);
                    break;
            }

            _logger.LogInformation("Request path: {0}", request.Url);
            _logger.LogDebug("Body {0}", body);

            switch (endpoint.Method)
            {
                case RequestMethod.Get:
                    return await request.GetAsync();
                case RequestMethod.Post:
                    return await request.PostAsync(new ByteArrayContent(body));
                case RequestMethod.Put:
                    return await request.PutAsync(new ByteArrayContent(body));
                case RequestMethod.Delete:
                    return await request.DeleteAsync();
                case RequestMethod.Patch:
                    return await request.PatchAsync(new ByteArrayContent(body));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async Task ExecutePublishEndpoint(Api api, Endpoint endpoint)
        {
            var response = await _requestPolicy.ExecuteAsync(() => ExecuteEndpoint(api, endpoint));
            var responseBody = await response.Content.ReadAsStringAsync();

//            var vars = Regex.Matches(endpoint.Mqtt.Topic, @"%(.*?)%")
//                .Select(r => r.Groups[1].Value)
//                .ToList();

            switch (endpoint.Encoding)
            {
                case BodyEncoding.Json:
                    var json = JToken.Parse(responseBody);
                    var tokens = json.SelectTokens(endpoint.Selector);

                    tokens.ToList()
                        .ForEach(async t =>
                        {
//                            var subscribeTopic = endpoint.Mqtt.Topic;
//
//                            vars.ForEach(v =>
//                            {
//                                subscribeTopic = subscribeTopic.Replace($"%{v}%",
//                                    t.SelectToken(v).ToString());
//                            });
//
//                            Subscribe(subscribeTopic, api, endpoint);
                            var msg = new MqttApplicationMessageBuilder()
                                .WithTopic($"{api.BaseTopic}/{endpoint.Mqtt.Topic}")
                                .WithPayload(JsonConvert.SerializeObject(t))
                                .WithExactlyOnceQoS()
                                .Build();

                            await _mqttClient.PublishAsync(msg);
                        });
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private async Task ExecuteAuthentication(Api api)
        {
            _logger.LogInformation("Performing authentication");
            byte[] body;
            api.Authentication.Token = string.Empty;

            switch (api.Authentication.BodyEncoding)
            {
                case BodyEncoding.Json:
                    body = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(api.Authentication.Body));
                    break;

                case BodyEncoding.Yaml:
                    var serializer = new SerializerBuilder().Build();
                    var yaml = serializer.Serialize(api.Authentication.Body);
                    body = System.Text.Encoding.UTF8.GetBytes(yaml);
                    break;

                case BodyEncoding.Xml:
                    var xmlSerializer = new XmlSerializer(api.Authentication.Body.GetType());

                    await using (var textWriter = new StringWriter())
                    {
                        xmlSerializer.Serialize(textWriter, api.Authentication.Body);
                        body = System.Text.Encoding.UTF8.GetBytes(textWriter.ToString());
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }

            var response = await ExecuteEndpoint(api, api.Authentication, null, body);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Authentication failed");
                return;
            }

            _logger.LogInformation("Authentication succeeded, fetching token");
            switch (api.Authentication.BodyEncoding)
            {
                case BodyEncoding.Json:
                    var responseJson = await response.Content.ReadAsStringAsync();
                    var json = JToken.Parse(responseJson);

                    api.SetAuthToken(json.SelectToken(api.Authentication.TokenPath).ToString());
                    break;

                case BodyEncoding.Yaml:
                    using (var input = new StringReader(await response.Content.ReadAsStringAsync()))
                    {
                        var deserializer = new DeserializerBuilder()
                            .WithNamingConvention(CamelCaseNamingConvention.Instance)
                            .IgnoreUnmatchedProperties()
                            .Build();
                        var yaml = deserializer.Deserialize(input)?.ToKeyValuePairs().ToList();

                        api.SetAuthToken(yaml?.FirstOrDefault(y => y.Key.Equals(api.Authentication.TokenPath)).ToString());
                    }

                    break;

                case BodyEncoding.Xml:
                    var responseXml = await response.Content.ReadAsStringAsync();
                    var xml = new XmlDocument();
                    if (string.IsNullOrEmpty(responseXml) || !xml.TryLoadXml(responseXml.RemoveAllNamespaces())) return;

                    api.SetAuthToken(xml.SelectSingleNode(api.Authentication.TokenPath)?.Value);
                    break;
            }

            _logger.LogInformation("Authentication done");
        }

        private async void Subscribe(Api api, Endpoint endpoint)
        {
            _logger.LogInformation("Subscribing to {0}/{1}", api.BaseTopic, endpoint.Mqtt.TopicSubscribe);

            await _mqttClient.SubscribeAsync(new TopicFilterBuilder()
                .WithTopic($"{api.BaseTopic}/{endpoint.Mqtt.TopicSubscribe}")
                .Build());

            _mqttSubscriptions.Add(new MqttSubscription
            {
                Api = api,
                Endpoint = endpoint
            });
        }
    }
}