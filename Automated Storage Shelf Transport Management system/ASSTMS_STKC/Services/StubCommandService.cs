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

        //public async Task<bool> SendStopCommandAsync(string stockerId)
        //{

        //}
    }
}
