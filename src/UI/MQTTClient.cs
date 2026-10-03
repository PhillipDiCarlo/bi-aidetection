using MQTTnet;
//using MQTTnet.Client.Connecting;
//using MQTTnet.Client.Options;
//using MQTTnet.Client.Publishing;
//using MQTTnet.Client.Subscribing;
using MQTTnet.Protocol;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static AITool.AITOOL;

namespace AITool
{
    //Handler signature for a subscribed topic.  Payload is the raw bytes received - callers can decode as UTF8/JSON themselves.
    public delegate Task MqttMessageHandler(string Topic, byte[] Payload);

    public class MQTTClient : IDisposable, IAsyncDisposable
    {
        public bool IsSubscribed = false;
        public bool IsConnected = false;

        int portint = 0;
        string server = "";
        string port = "";
        string LastTopic = "";
        string LastPayload = "";
        bool LastRetain = false;

        //topics we are subscribed (or want to be subscribed) to, and the handler to call when a message for that topic arrives.
        //Kept so we can re-subscribe automatically after a reconnect (see ConnectedAsync below).
        private readonly ConcurrentDictionary<string, MqttMessageHandler> SubscribedHandlers = new ConcurrentDictionary<string, MqttMessageHandler>(StringComparer.OrdinalIgnoreCase);

        MqttClientFactory factory = null;
        IMqttClient mqttClient = null;
        MqttClientOptions options = null;
        MqttClientConnectResult cres = null;

        public void Dispose()
        {
            // Synchronous fallback; prefer DisposeAsync so the broker receives a clean disconnect.
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        public async ValueTask DisposeAsync()
        {

            if (mqttClient != null)
            {

                if (mqttClient.IsConnected)
                {
                    Log($"Debug: MQTT: Disconnecting from server.");
                    try
                    {
                        await mqttClient.DisconnectAsync();
                    }
                    catch (Exception ex)
                    {
                        //dont throw ERROR in the log if fail to disconnect
                        Log($"Debug: MQTT: Could not disconnect from server, got: {ex.Msg()}");
                    }
                }
                else
                {
                    Log($"Debug: MQTT: Already disconnected from server, no need to disconnect.");
                }


                IsConnected = false;

                mqttClient.Dispose();

            }



        }

        public MQTTClient()
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            try
            {
                this.factory = new MqttClientFactory();
                this.mqttClient = factory.CreateMqttClient();

            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Msg()}");
            }
        }

        private string LastServerPortUser = "";
        public async Task<bool> Connect()
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            bool ret = false;
            try
            {
                if (this.mqttClient.IsConnected)
                    await this.mqttClient.DisconnectAsync();

                this.server = AppSettings.Settings.mqtt_serverandport.GetWord("", ":");
                this.port = AppSettings.Settings.mqtt_serverandport.GetWord(":", "/");
                this.portint = 0;

                if (!string.IsNullOrEmpty(this.port))
                    this.portint = Convert.ToInt32(this.port);
                if (this.portint == 0 && AppSettings.Settings.mqtt_UseTLS)
                {
                    this.portint = 8883;
                }
                else if (this.portint == 0)
                {
                    this.portint = 1883;
                }

                bool IsWebSocket = (AppSettings.Settings.mqtt_serverandport.IndexOf("ws://", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    AppSettings.Settings.mqtt_serverandport.IndexOf("/mqtt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    AppSettings.Settings.mqtt_serverandport.IndexOf("wss://", StringComparison.OrdinalIgnoreCase) >= 0);

                bool UseTLS = (AppSettings.Settings.mqtt_UseTLS || portint == 8883 || AppSettings.Settings.mqtt_serverandport.IndexOf("wss://", StringComparison.OrdinalIgnoreCase) >= 0);

                //bool UseCreds = (!string.IsNullOrWhiteSpace(AppSettings.Settings.mqtt_username));



                //=====================================================================
                //Seems like there should be a better way here with this Options builder...
                //I dont see an obvious way to directly modify options without the builder
                //and I cant seem to put IF statements around each part of the option builder
                //parameters.
                //=====================================================================

                var lw = new MqttApplicationMessage()
                {
                    Topic = AppSettings.Settings.mqtt_LastWillTopic,
                    PayloadSegment = Encoding.UTF8.GetBytes(AppSettings.Settings.mqtt_LastWillPayload),
                    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
                    Retain = true
                };

                if (UseTLS)
                {
                    if (IsWebSocket)
                    {

                        options = new MqttClientOptionsBuilder()
                            .WithClientId(AppSettings.Settings.mqtt_clientid)
                            .WithWebSocketServer(o => o.WithUri(AppSettings.Settings.mqtt_serverandport))
                            .WithCredentials(AppSettings.Settings.mqtt_username, AppSettings.Settings.mqtt_password)
                            .WithTlsOptions(o => o.UseTls())
                            .WithWillTopic(AppSettings.Settings.mqtt_LastWillTopic)
                            .WithWillPayload(Encoding.UTF8.GetBytes(AppSettings.Settings.mqtt_LastWillPayload))
                            .WithWillRetain(true)
                            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                            .WithCleanSession()
                            .Build();

                    }
                    else
                    {
                        options = new MqttClientOptionsBuilder()
                            .WithClientId(AppSettings.Settings.mqtt_clientid)
                            .WithTcpServer(server, portint)
                            .WithCredentials(AppSettings.Settings.mqtt_username, AppSettings.Settings.mqtt_password)
                            .WithTlsOptions(o => o.UseTls())
                            .WithWillTopic(AppSettings.Settings.mqtt_LastWillTopic)
                            .WithWillPayload(Encoding.UTF8.GetBytes(AppSettings.Settings.mqtt_LastWillPayload))
                            .WithWillRetain(true)
                            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                            .WithCleanSession()
                            .Build();
                    }


                }
                else
                {
                    if (IsWebSocket)
                    {
                        options = new MqttClientOptionsBuilder()
                        .WithClientId(AppSettings.Settings.mqtt_clientid)
                        .WithWebSocketServer(o => o.WithUri(AppSettings.Settings.mqtt_serverandport))
                        .WithCredentials(AppSettings.Settings.mqtt_username, AppSettings.Settings.mqtt_password)
                            .WithWillTopic(AppSettings.Settings.mqtt_LastWillTopic)
                            .WithWillPayload(Encoding.UTF8.GetBytes(AppSettings.Settings.mqtt_LastWillPayload))
                            .WithWillRetain(true)
                            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                        .WithCleanSession()
                        .Build();
                    }
                    else
                    {
                        options = new MqttClientOptionsBuilder()
                        .WithClientId(AppSettings.Settings.mqtt_clientid)
                        .WithTcpServer(server, portint)
                        .WithCredentials(AppSettings.Settings.mqtt_username, AppSettings.Settings.mqtt_password)
                            .WithWillTopic(AppSettings.Settings.mqtt_LastWillTopic)
                            .WithWillPayload(Encoding.UTF8.GetBytes(AppSettings.Settings.mqtt_LastWillPayload))
                            .WithWillRetain(true)
                            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                        .WithCleanSession()
                        .Build();

                    }



                }

                //mqttClient.UseDisconnectedHandler(async e =>

                mqttClient.DisconnectedAsync += async e =>
                {
                    IsConnected = false;
                    string excp = "";
                    if (e.Exception != null)
                    {
                        excp = e.Exception.Message;
                    }
                    Log($"Debug: MQTT: ### DISCONNECTED FROM SERVER ### - Reason: {e.Reason}, ClientWasDisconnected: {e.ClientWasConnected}, {excp}");

                    //reconnect here if needed?
                };


                //mqttClient.UseApplicationMessageReceivedHandler(async e =>
                mqttClient.ApplicationMessageReceivedAsync += async e =>
                {
                    Log($"Debug: MQTT: ### RECEIVED APPLICATION MESSAGE ###");
                    Log($"Debug: MQTT: + Topic = {e.ApplicationMessage.Topic}");
                    Log($"Debug: MQTT: + Payload = {Encoding.UTF8.GetString(e.ApplicationMessage.Payload).Truncate()}");
                    Log($"Debug: MQTT: + QoS = {e.ApplicationMessage.QualityOfServiceLevel}");
                    Log($"Debug: MQTT: + Retain = {e.ApplicationMessage.Retain}");
                    Log("");

                    //route to any handler registered for this topic (exact match, or MQTT wildcard match against the filter it subscribed with)
                    foreach (var kv in this.SubscribedHandlers)
                    {
                        if (TopicMatchesFilter(kv.Key, e.ApplicationMessage.Topic))
                        {
                            try
                            {
                                await kv.Value(e.ApplicationMessage.Topic, e.ApplicationMessage.Payload.ToArray());
                            }
                            catch (Exception ex)
                            {
                                Log($"Error: MQTT: Subscribed handler for topic filter '{kv.Key}' threw: {ex.Msg()}");
                            }
                        }
                    }

                };


                //mqttClient.UseConnectedHandler(async e =>
                mqttClient.ConnectedAsync += async e =>
                {
                    IsConnected = true;
                    Log($"Debug: MQTT: ### CONNECTED WITH SERVER '{AppSettings.Settings.mqtt_serverandport}' ### - Result: {e.ConnectResult.ResultCode}, '{e.ConnectResult.ReasonString}'");


                    MqttApplicationMessage ma = new MqttApplicationMessageBuilder()
                                                    .WithTopic(AppSettings.Settings.mqtt_LastWillTopic)
                                                    .WithPayload(AppSettings.Settings.mqtt_OnlinePayload)
                                                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                                                    .WithRetainFlag(true)
                                                    .Build();

                    Log($"Debug: MQTT: Sending '{AppSettings.Settings.mqtt_OnlinePayload}' message...");
                    MqttClientPublishResult res = await mqttClient.PublishAsync(ma, CancellationToken.None);

                    //re-subscribe to anything we were subscribed to - this fires after every successful connect, including reconnects
                    foreach (string topic in this.SubscribedHandlers.Keys)
                    {
                        try
                        {
                            await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).WithAtLeastOnceQoS().Build());
                            IsSubscribed = true;
                            Log($"Debug: MQTT: ### SUBSCRIBED to topic '{topic}' ###");
                        }
                        catch (Exception ex)
                        {
                            Log($"Error: MQTT: Could not subscribe to topic '{topic}': {ex.Msg()}");
                        }
                    }
                };

                Log($"Debug: MQTT: Connecting to server '{this.server}:{this.portint}' with ClientID '{AppSettings.Settings.mqtt_clientid}', Username '{AppSettings.Settings.mqtt_username}', Password '{AppSettings.Settings.mqtt_username.ReplaceChars('*')}'...");


                cres = await mqttClient.ConnectAsync(options, CancellationToken.None);

            }
            catch (Exception ex)
            {

                Log($"Error: {ex.Msg()}");
            }
            return ret;
        }

        public async Task<MqttClientPublishResult> PublishAsync(string topic, string payload, bool retain, ClsImageQueueItem CurImg)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            MqttClientPublishResult res = null;
            Stopwatch sw = Stopwatch.StartNew();

            this.LastRetain = retain;
            this.LastPayload = payload;
            this.LastTopic = topic;
            if (string.IsNullOrWhiteSpace(topic))
            {
                this.LastTopic = Guid.NewGuid().ToString();
            }

            if (!this.IsConnected || this.LastServerPortUser != AppSettings.Settings.mqtt_serverandport + AppSettings.Settings.mqtt_password + AppSettings.Settings.mqtt_username + AppSettings.Settings.mqtt_clientid)
            {
                await this.Connect();
                this.LastServerPortUser = AppSettings.Settings.mqtt_serverandport + AppSettings.Settings.mqtt_password + AppSettings.Settings.mqtt_username + AppSettings.Settings.mqtt_clientid;
            }

            if (!this.IsConnected)
                return res;

            await Task.Run(async () =>
                        {


                            try
                            {


                                Log($"Debug: MQTT: Sending topic '{this.LastTopic}' with payload '{this.LastPayload}' to server '{this.server}:{this.portint}'...");

                                if (cres != null && mqttClient.IsConnected && cres.ResultCode == MqttClientConnectResultCode.Success)
                                {

                                    IsConnected = true;

                                    MqttApplicationMessage ma;

                                    if (CurImg != null)
                                    {

                                        ma = new MqttApplicationMessageBuilder()
                                             .WithTopic(this.LastTopic)
                                             .WithPayload(CurImg.ToMemStream())
                                             .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                                             .WithRetainFlag(this.LastRetain)
                                             .Build();

                                        res = await mqttClient.PublishAsync(ma, CancellationToken.None);


                                        if (res.ReasonCode == MqttClientPublishReasonCode.Success)
                                        {
                                            Log($"Debug: MQTT: ...Sent image in {sw.ElapsedMilliseconds}ms, Reason: '{res.ReasonCode}' ({Convert.ToInt32(res.ReasonCode)} - '{res.ReasonString}')");
                                        }
                                        else
                                        {
                                            Log($"Error: MQTT: sending image: ({sw.ElapsedMilliseconds}ms) Reason: '{res.ReasonCode}' ({Convert.ToInt32(res.ReasonCode)} - '{res.ReasonString}')");
                                        }

                                    }
                                    else
                                    {
                                        ma = new MqttApplicationMessageBuilder()
                                                .WithTopic(this.LastTopic)
                                                .WithPayload(this.LastPayload)
                                                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                                                .WithRetainFlag(this.LastRetain)
                                                .Build();

                                        res = await mqttClient.PublishAsync(ma, CancellationToken.None);

                                        //Success = 0,
                                        //        NoMatchingSubscribers = 0x10,
                                        //        UnspecifiedError = 0x80,
                                        //        ImplementationSpecificError = 0x83,
                                        //        NotAuthorized = 0x87,
                                        //        TopicNameInvalid = 0x90,
                                        //        PacketIdentifierInUse = 0x91,
                                        //        QuotaExceeded = 0x97,
                                        //        PayloadFormatInvalid = 0x99

                                        if (res.ReasonCode == MqttClientPublishReasonCode.Success)
                                        {
                                            Log($"Debug: MQTT: ...Sent in {sw.ElapsedMilliseconds}ms, Reason: '{res.ReasonCode}' ({Convert.ToInt32(res.ReasonCode)} - '{res.ReasonString}')");
                                        }
                                        else
                                        {
                                            Log($"Error: MQTT: sending: ({sw.ElapsedMilliseconds}ms) Reason: '{res.ReasonCode}' ({Convert.ToInt32(res.ReasonCode)} - '{res.ReasonString}')");
                                        }

                                    }

                                }
                                else if (cres != null)
                                {
                                    IsConnected = false;
                                    Log($"Error: MQTT: connecting: ({sw.ElapsedMilliseconds}ms) Result: '{cres.ResultCode}' - '{cres.ReasonString}'");
                                }
                                else
                                {
                                    IsConnected = false;
                                    Log($"Error: MQTT: Error connecting: ({sw.ElapsedMilliseconds}ms) cres=null");
                                }





                            }
                            catch (Exception ex)
                            {

                                Log($"Error: MQTT: Unexpected Problem: Topic '{this.LastTopic}' Payload '{this.LastPayload}': " + ex.Msg());
                            }
                            finally
                            {

                            }

                        });


            return res;


        }

        //Subscribe to a topic (which may contain MQTT wildcards + or #) and route any matching message to Handler.
        //Connects first if needed.  The topic/handler is remembered so ConnectedAsync (above) can re-subscribe automatically after a reconnect.
        public async Task<bool> SubscribeAsync(string topic, MqttMessageHandler Handler)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            bool ret = false;

            if (string.IsNullOrWhiteSpace(topic) || Handler == null)
                return ret;

            this.SubscribedHandlers[topic] = Handler;

            try
            {
                if (!this.IsConnected || !this.mqttClient.IsConnected)
                {
                    await this.Connect();
                }

                if (this.IsConnected && this.mqttClient.IsConnected)
                {
                    await this.mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).WithAtLeastOnceQoS().Build());
                    IsSubscribed = true;
                    Log($"Debug: MQTT: ### SUBSCRIBED to topic '{topic}' ###");
                    ret = true;
                }
            }
            catch (Exception ex)
            {
                Log($"Error: MQTT: Could not subscribe to topic '{topic}': {ex.Msg()}");
            }

            return ret;
        }

        //Stop routing messages for a previously-subscribed topic.
        public async Task UnsubscribeAsync(string topic)
        {
            using var Trace = new Trace();  //This c# 8.0 using feature will auto dispose when the function is done.

            this.SubscribedHandlers.TryRemove(topic, out _);

            try
            {
                if (this.mqttClient != null && this.mqttClient.IsConnected)
                {
                    await this.mqttClient.UnsubscribeAsync(topic);
                }
            }
            catch (Exception ex)
            {
                Log($"Debug: MQTT: Could not unsubscribe from topic '{topic}': {ex.Msg()}");
            }
        }

        //Basic MQTT topic filter matching (supports the + single-level and # multi-level wildcards).  Frigate topics are static
        //(e.g. "frigate/events") so this is normally just a plain equality check, but wildcards are supported for completeness.
        private static bool TopicMatchesFilter(string Filter, string Topic)
        {
            if (string.Equals(Filter, Topic, StringComparison.OrdinalIgnoreCase))
                return true;

            if (Filter.IndexOf('+') < 0 && Filter.IndexOf('#') < 0)
                return false;

            string[] filterParts = Filter.Split('/');
            string[] topicParts = Topic.Split('/');

            for (int i = 0; i < filterParts.Length; i++)
            {
                if (filterParts[i] == "#")
                    return true; //matches this level and everything below

                if (i >= topicParts.Length)
                    return false;

                if (filterParts[i] != "+" && !string.Equals(filterParts[i], topicParts[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return filterParts.Length == topicParts.Length;
        }

    }
}
