using ASSTMS_STKC.Data;
using ASSTMS_STKC.SharedModels;
using ASSTMS_STKC.SharedModels.Models;

namespace ASSTMS_STKC.Services
{
    public class StubCommandService
    {
        private readonly HttpClient _httpClient;

        public StubCommandService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<bool> SendStopCommandAsync(string stockerId)
        {
            var request = new OperationInstructionsReq(
                    true,
                    new Job(
                    "JOB1001",
                    "STOP",
                    "CST-1001",
                    "IN_PORT",
                    "SHELF_A1"
                    )
                    );

            HttpResponseMessage response =
                //await _httpClient.PostAsJsonAsync($"http://172.16.7.19:5029/",request);
                await _httpClient.PostAsJsonAsync($"http://localhost:5029/", request);

            return response.IsSuccessStatusCode;
        }
    }
}
