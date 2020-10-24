using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace status_updater.GPMDesktopPlayer
{
    public class GPMDesktopPlayerOptions
    {
        public string AuthCode { get; set; }
    }

    /// <summary>
    /// Wrapper for managing WebSocket communication with GPM Desktop Player.
    /// </summary>
    internal class GPMDesktopPlayerSocketHandler
    {
        public delegate Task GPMDesktopPlayerSocketEventHandler(GPMDesktopPlayerSocketHandler source, GPMDesktopPlayerData e);

        internal ClientWebSocket Socket { get; }

        /// <summary>
        /// Occurs when an Event is received from GPM Desktop Player.
        /// </summary>
        public event GPMDesktopPlayerSocketEventHandler EventOccurredAsync;

        /// <summary>
        /// Wrapper for managing WebSocket communication GPM Desktop Player.
        /// </summary>
        /// <param name="uri">The URI to connect to the websocket on.</param>
        /// <param name="authCode">Authcode for the GPM Desktop Player</param>
        public GPMDesktopPlayerSocketHandler(Uri uri, string authCode)
        {
            _uri = uri;
            _authCode = authCode;

            Socket = new ClientWebSocket();
            _serialiserSettings = new JsonSerializerSettings {ContractResolver = new CamelCasePropertyNamesContractResolver()};
            _serialiser = JsonSerializer.Create(_serialiserSettings);
        }

        /// <summary>
        /// Initiate connection with the Stream Deck.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to be used for async requests.</param>
        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            await Socket.ConnectAsync(_uri, cancellationToken);
            await Socket.SendAsync(GetConnectionBytes(), WebSocketMessageType.Text, true, CancellationToken.None);
            _connected = true;
        }

        /// <summary>
        /// Continually listens to the WebSocket and raises EventsOccurrences when messages are received.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token used to halt the function.</param>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            if (!_connected) throw new Exception("Not connected to socket");
            var pipe = new Pipe();
            Task.WaitAll(FillPipeAsync(pipe.Writer, cancellationToken), ReadPipe(pipe.Reader, cancellationToken));
        }

        private readonly Uri _uri;
        private readonly string _authCode;
        private bool _connected;
        private readonly JsonSerializerSettings _serialiserSettings;
        private readonly JsonSerializer _serialiser;

        private Task OnEventOccurredAsync(GPMDesktopPlayerData data)
        {
            return EventOccurredAsync?.Invoke(this, data);
        }

        private ArraySegment<byte> GetConnectionBytes()
        {
            var registration = new GPMDesktopPlayerPayload
            {
                Namespace = "connect",
                Method = "connect",
                Arguments = new []{"SlackStatusUpdater", _authCode}
            };

            var outString = JsonConvert.SerializeObject(registration, _serialiserSettings);
            var outBytes = Encoding.UTF8.GetBytes(outString);
            return outBytes;
        }

        private async Task FillPipeAsync(PipeWriter writer, CancellationToken cancellationToken)
        {
            if (!_connected) throw new Exception("Not connected to socket");

            while (!cancellationToken.IsCancellationRequested)
            {
                var memory = writer.GetMemory();
                try
                {
                    var socketResult = await Socket.ReceiveAsync(memory, cancellationToken);
                    if (socketResult.Count == 0) continue;
                    writer.Advance(socketResult.Count);
                    if (socketResult.EndOfMessage) await writer.FlushAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    // Stay in loop
                }
                Thread.Sleep(TimeSpan.FromSeconds(0.1));
            }

            writer.Complete();
        }

        private async Task ReadPipe(PipeReader reader, CancellationToken cancellationToken)
        {
            if (!_connected) throw new Exception("Not connected to socket");

            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(cancellationToken);

                var jsonString = Encoding.UTF8.GetString(result.Buffer.ToArray());

                reader.AdvanceTo(result.Buffer.End);

                if (string.IsNullOrEmpty(jsonString)) continue;
                using var sr = new StringReader(jsonString);
                using var jsonReader = new JsonTextReader(sr) {SupportMultipleContent = true};

                while (jsonReader.Read())
                    if (jsonReader.TokenType == JsonToken.StartObject)
                    {
                        var payload = _serialiser.Deserialize<GPMDesktopPlayerData>(jsonReader);
                        if(payload.Channel != "time")
                            await OnEventOccurredAsync(payload);
                    }

                Thread.Sleep(TimeSpan.FromSeconds(0.1));

            }
        }
    }
}