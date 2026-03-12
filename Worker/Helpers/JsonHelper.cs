using System.Text.Json;
using System.Text.Json.Serialization;
using Worker.Converters;

namespace Worker.Helpers
{
    public static class JsonHelper
    {
        public static JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        static JsonHelper()
        {
            //Options.Converters.Add(new NullableDateTimeConverter());
        }
    }
}
