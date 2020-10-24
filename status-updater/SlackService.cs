using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace status_updater
{
    public class SlackOptions
    {
        public string AccessToken { get; set; }
        public string DefaultEmoji { get; set; }
    }

    public class SlackService
    {
        private readonly SlackOptions _options;
        public HttpClient Client { get; set; }

        public SlackService(HttpClient client, IOptions<SlackOptions> options)
        {
            _options = options.Value;
            client.BaseAddress = new Uri("https://slack.com/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
            Client = client;
        }

        public async Task<bool> SetUserStatusAsync(string emoji, string status)
        {
            var path = "api/users.profile.set";

            var data = new
            {
                profile = new { 
                    status_text = status,
                    status_emoji = emoji ?? _options.DefaultEmoji,
                    status_expiration = 0
                }
            };

            var response = await Client.PostAsync(path, new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
    }
}
