using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace stocker.Client
{
    /// <summary>
    /// サーバーとのHTTP通信を行う共通クラス。
    /// POSTリクエストの送信およびレスポンスの受信を担当する。
    /// </summary>
    public class ApiClient
    {
        // HTTP通信を行うクライアント
        private readonly HttpClient _httpClient;

        public ApiClient()
        {
            // HttpClient生成
            _httpClient = new HttpClient();

            // 共通タイムアウト
            //_httpClient.Timeout = TimeSpan.FromSeconds(30);

            // サーバーのベースURL設定
            _httpClient.BaseAddress = new Uri("http://172.16.7.6:5028/");
        }


        /// <summary>
        /// POSTリクエストを送信する。
        /// レスポンスボディは取得しない。
        /// </summary>
        /// <typeparam name="T">リクエスト型</typeparam>
        /// <param name="url">送信先URL</param>
        /// <param name="request">送信データ</param>
        /// <returns>HTTPレスポンス</returns>
        public async Task<HttpResponseMessage> PostAsync<T>(string url, T request)
        {
            // リクエストをJSONへ変換
            string json = JsonSerializer.Serialize(request);

            // HTTP送信用コンテンツ生成
            StringContent? content = new(
                json,
                Encoding.UTF8,
                "application/json");

            // POSTリクエスト送信
            return await _httpClient.PostAsync(url, content);
        }

        /// <summary>
        /// POSTリクエストを送信し、レスポンスを指定した型へ変換して返す。
        /// </summary>
        /// <typeparam name="TRequest">リクエスト型</typeparam>
        /// <typeparam name="TResponse">レスポンス型</typeparam>
        /// <param name="url">送信先URL</param>
        /// <param name="request">送信データ</param>
        /// <returns>デシリアライズしたレスポンス</returns>
        public async Task<TResponse?> PostAsync<TRequest, TResponse>(
            string url,
            TRequest request)
        {
            // リクエストをJSONへ変換
            var json = JsonSerializer.Serialize(request);

            // HTTP送信用コンテンツ生成
            StringContent? content = new(json, Encoding.UTF8, "application/json");
            
            // POSTリクエスト送信
            var response = await _httpClient.PostAsync(url, content);

            // レスポンスボディ取得
            var responseBody = await response.Content.ReadAsStringAsync();

            // HTTPエラーの場合は例外を送出
            response.EnsureSuccessStatusCode();

            // プロパティ名の大文字・小文字を区別せずデシリアライズする
            var option = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // JSONをレスポンス型へ変換
            return JsonSerializer.Deserialize<TResponse>(
                responseBody, option);
        }
    }
}
