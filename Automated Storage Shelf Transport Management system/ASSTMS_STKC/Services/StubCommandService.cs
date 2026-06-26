using ASSTMS_STKC.SharedModels;

namespace ASSTMS_STKC.Services
{
    public class StubCommandService
    {
        private readonly HttpClient _httpClient;

        public StubCommandService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<HttpResponseMessage> SendStopCommandAsync(OperationInstructionsReq request)
        {
            HttpResponseMessage response =
                await _httpClient.PostAsJsonAsync($"https://172.16.7.19:5029/",request);

            return response;
        }
    }
}
