using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StereoKit;

namespace GroqApiLibrary
{
    public class GroqApiClient
    {
        private HttpClient client = new(); // Ensure non-null

        public GroqApiClient(string apiKey)
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }

        public async Task<JObject> CreateChatCompletionAsync(JObject request)
        {

            StringContent httpContent = new StringContent(request.ToString(), Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", httpContent);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();

                try {
                    JObject result = JObject.Parse(responseBody);
                    return result;
                }
                catch (JsonReaderException e)
                {
                    Log.Err($"Error parsing JSON: {e.Message}");
                    return new JObject();
                }
            }
            catch (HttpRequestException e)
            {
                Log.Err($"Error sending request: {e.Message}");
                return new JObject();
            }
        }
    }
}