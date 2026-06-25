using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace stocker.Client
{
    public class ApiClient
    {
        private readonly HttpClient _httpClient;

        public ApiClient()
        {
            _httpClient = new HttpClient();

            // 共通タイムアウト
            //_httpClient.Timeout = TimeSpan.FromSeconds(30);

            // サーバーURL
            _httpClient.BaseAddress = new Uri("http://172.16.7.6:5028/");
        }

        public async Task<HttpResponseMessage> PostAsync<T>(string url, T request)
        {
            string json = JsonSerializer.Serialize(request);

            //// エラーの確認用
            //Console.WriteLine($"Request URL : {url}");
            //Console.WriteLine($"Request JSON: {json}");

            StringContent? content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

            return await _httpClient.PostAsync(url, content);
        }

        public async Task<TResponse?> PostAsync<TRequest, TResponse>(
            string url,
            TRequest request)
        {
            var json = JsonSerializer.Serialize(request);

            //// エラーの確認用
            Console.WriteLine($"Request URL : {url}");
            Console.WriteLine($"Request JSON: {json}");

            StringContent? content = new(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync(url, content);

            var responseBody = await response.Content.ReadAsStringAsync();

            //// エラーの確認用
            Console.WriteLine($"StatusCode : {(int)response.StatusCode}");
            Console.WriteLine($"Response   : {responseBody}");

            response.EnsureSuccessStatusCode();

            var option = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return JsonSerializer.Deserialize<TResponse>(
                responseBody, option);
        }
    }
}
