using ASSTMS_STKC.Data;
using ASSTMS_STKC.Data.Repositories;
using ASSTMS_STKC.SharedModels;
using ASSTMS_STKC.SharedModels.Models;
using System.Net;

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
                await _httpClient.PostAsJsonAsync($"http://172.16.7.19:5029/",request);
            //await _httpClient.PostAsJsonAsync($"http://localhost:5029/", request);

            return response;
        }
    }
}
