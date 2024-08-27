using Meijer.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MeijerApp
{
    internal class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly string ChaosEndpointDefault = "https://chaosfunctionapplication.azurewebsites.net/api/ChaosData?code=WtY6mo-5zFF3B6uWCzSybYpwo1ypMbl7vdM4SqyaQSBLAzFuiL4h6w%3D%3D";
        protected static object MergeLock = new object();

        static void Main(string[] args)
        {
            ConcurrentDictionary<HttpStatusCode, int> statusCodes = new ConcurrentDictionary<HttpStatusCode, int>();
            string apiResponse = string.Empty;
            List<Task> callTasks = new List<Task>();
            JObject aggregate = new();


            //point config to appsettings.json file
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            //build up service collection
            var serviceProvider = new ServiceCollection()
                .AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information))
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            //services
            var chaosClientLogger = serviceProvider.GetRequiredService<ILogger<ChaosClient>>();
            var chaosClient = new ChaosClient(httpClient, chaosClientLogger);
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            //retrieve config values
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var chaosUrl = configuration.GetValue<string>("ChaosEndpoint") ?? ChaosEndpointDefault;
            var numApiCalls = configuration.GetValue<int>("APICalls");
            if (numApiCalls == 0)
                numApiCalls = 299; //default to 299

            //begin retrieval run
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            Console.WriteLine("Starting run");
            Console.WriteLine("Hitting Chaos endpoint {0} times", numApiCalls);

            var callThrottleWatch = new Stopwatch();
            callThrottleWatch.Start();
            for (int i = 0; i < numApiCalls; i++)
            {
                callTasks.Add(Task.Run(async () => {
                    try
                    {
                        //retrieve data
                        var responseMsg = await chaosClient.FetchChaos(chaosUrl);
                        //update the status code count
                        statusCodes.AddOrUpdate(responseMsg.StatusCode, 1, (key, oldValue) => oldValue + 1);

                        //format data as string
                        var responseContent = await responseMsg.Content.ReadAsStringAsync();
                        //Console.WriteLine("Status Code: {0} \tContent received: {1}", responseMsg.StatusCode, responseContent);
                        
                        //update the aggregate data
                        JObject update = JObject.Parse(responseContent);
                        lock(MergeLock)
                        {
                            aggregate.Merge(update);
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        logger.LogError("Exception encountered: {exc}", ex);
                        throw;
                    }
                }));

                //throttle to no more than 299 calls per second
                if((i % 299 == 0) && (callThrottleWatch.ElapsedMilliseconds < 1000))
                {
                    var delay = 1000 - callThrottleWatch.ElapsedMilliseconds;
                    Thread.Sleep((int)delay);
                    callThrottleWatch.Restart();
                }
                else if(callThrottleWatch.ElapsedMilliseconds >= 1000)
                {
                    callThrottleWatch.Restart();
                }
            }

            Task t = Task.WhenAll(callTasks);
            try
            {
                t.Wait();
            }
            catch { }

            stopWatch.Stop();
            Console.WriteLine("Run finished in {0}ms", stopWatch.ElapsedMilliseconds);

            Console.WriteLine("\nSummary of status codes received and their count:");
            foreach (var statusCode in statusCodes)
            {
                Console.WriteLine("Code: \"{0,-20}\" \tCount: {1,-10}", statusCode.Key, statusCode.Value);
            }

            Console.WriteLine("\nAggregate contents:");
            Console.WriteLine("{0}", aggregate.ToString());
        }
    }
}
