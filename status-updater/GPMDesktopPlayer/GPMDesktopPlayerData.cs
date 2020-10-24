using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace status_updater.GPMDesktopPlayer
{

    [JsonConverter(typeof(GPMDesktopPlayerDataConverter))]
    internal class GPMDesktopPlayerData
    {
        public string Channel { get; set; }
        public object Payload { get; set; }
    }

    internal class TrackPayload
    {
        public string Title { get; set; }
        public string Artist { get; set; }
    }

    internal class GPMDesktopPlayerDataConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jObject = JToken.ReadFrom(reader);

            var result = new GPMDesktopPlayerData
            {
                Payload = (string) jObject["channel"] switch {
                    "track" => new TrackPayload(),
                    "playState" => new bool(),
                    _ => new object()
                    }
            };

            serializer.Populate(jObject.CreateReader(), result);

            return result;
        }

        public override bool CanConvert(Type objectType)
        {
            throw new NotImplementedException();
        }

    }
}