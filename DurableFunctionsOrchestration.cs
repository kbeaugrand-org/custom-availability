
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace Company.Function
{
    public static class DurableFunctionsOrchestration
    {
        [FunctionName("TimerOrchestrator")]
        public static async Task TimerOrchestrator([TimerTrigger("0 */1 * * * *")] TimerInfo timer, [DurableClient] IDurableOrchestrationClient starter)
        {
            await starter.StartNewAsync("RunOrchestrator", null);
        }

        [FunctionName("RunOrchestrator")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            List<Task> activities = new List<Task>();

            activities.Add(context.CallActivityAsync("TestUri", "https://www.bing.com/"));
            activities.Add(context.CallActivityAsync("TestUri", "https://www.google.com/"));

            await Task.WhenAll(activities);
        }

        [FunctionName("TestUri")]
        public static async Task TestUri([ActivityTrigger] Uri test, ILogger log)
        {
            var telemetryClient = new TelemetryClient(new TelemetryConfiguration(Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY")));
            var telemetry = new AvailabilityTelemetry();
            telemetry.Name = test.Host;
            var watch = new Stopwatch();

            try
            {
                HttpClient client = HttpClientFactory.Create();

                watch.Start();
                var responseMessage = await client.GetAsync(test);

                responseMessage.EnsureSuccessStatusCode();
                telemetry.Success = true;
            }
            catch (Exception e)
            {
                telemetry.Message = e.ToString();
            }
            finally
            {
                watch.Stop();
                telemetry.Duration = watch.Elapsed;
                telemetryClient.TrackAvailability(telemetry);
                telemetryClient.Flush();
            }
        }
    }
}