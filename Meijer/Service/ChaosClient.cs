using Meijer.Interface;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Meijer.Service
{
    public class ChaosClient : IChaosClient
    {
        private readonly ILogger<ChaosClient> _logger;
        private readonly HttpClient _httpClient;

        public ChaosClient(HttpClient httpClient, ILogger<ChaosClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<HttpResponseMessage> FetchChaos(string url)
        {
            _logger.LogDebug("Fetching Chaos endpoint");
            HttpResponseMessage respMessage = await _httpClient.GetAsync(url);

            return respMessage;
        }
    }
}
