using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client.Utils;
using Microsoft.Xrm.Sdk;
using Polly;
using Polly.Retry;
using System;
using System.Linq;
using System.Net.Sockets;
using System.ServiceModel;

namespace DotNetDevOps.Extensions.PowerPlatform.DataVerse
{
    public static class CDSPolly
    {
        public static RetryPolicy RetryPolicy = Policy
              .Handle<DataverseConnectionException>(ShouldHandle)
               .Or<FaultException<OrganizationServiceFault>>(ShouldHandle)
               .OrInner<TimeoutException>()
               .OrInner<SocketException>()
              .WaitAndRetryForever(BackoffTimeProvider, (ex, count, time, context) =>
              {
                  LogRetryAttempt(ex, count, time, context);

              });

        private static bool ShouldHandle(FaultException<OrganizationServiceFault> arg)
        {
            if (arg.Detail.ErrorCode == -2147088227)
            {
                Console.WriteLine(arg.Detail.Message);
                Console.WriteLine(arg.Detail.GetType().Name);
            }
            switch (arg.Detail.ErrorCode)
            {
                case -2147088227:   //Name: MultipleRecordsFoundByEntityKey
                                    //Message: More than one record exists for entity {0} with entity key involving attributes { 1}  // concurrent transaction issue,More than one record exists for entity {} with entity key involving attributes {}

                case -2147204295:   //CouldNotObtainLockOnResource
                                    //Database resource lock could not be obtained.For more information, see http://docs.microsoft.com/dynamics365/customer-engagement/customize/best-practices-workflow-processes#limit-the-number-of-workflows-that-update-the-same-entity
                    return true;

            }
            return false;

        }

        private static void LogRetryAttempt(Exception ex, int count, TimeSpan time, Context context)
        {
            if (context.ContainsKey("logger") && context["logger"] is ILogger logger)
            {
                logger.LogInformation("Retrying attempt:{count} with backoff time:{time}, exception was {ex}", count, time, ex);
            }
            //   Console.WriteLine($"Retrying attempt:{count} with backoff time:{time}");
        }

        private static TimeSpan BackoffTimeProvider(int i, Exception ex, Context context)
        {
            if (ex.InnerException is AggregateException aggreex
                && aggreex.InnerException is FaultException<OrganizationServiceFault> serviceex)
            {

                switch (serviceex.Detail.ErrorCode)
                {
                    case -2147015902: //Number of requests exceeded the limit of 6000 over time window of 300 seconds.
                    case -2147015903:  //Combined execution time of incoming requests exceeded limit of 1,200,000 milliseconds over time window of 300 seconds. Decrease number of concurrent requests or reduce the duration of requests and try again later.
                    case -2147015898: //Number of concurrent requests exceeded the limit of 52.
                        return (TimeSpan)serviceex.Detail.ErrorDetails["Retry-After"];

                }


            }

            return TimeSpan.FromSeconds(Math.Pow(2, i));
        }

        private static bool ShouldHandle(DataverseConnectionException ex)
        {
            if (ex.InnerException is AggregateException aggreex)
            {


                if (aggreex.InnerException is FaultException<OrganizationServiceFault> serviceex)
                {
                    switch (serviceex.Detail.ErrorCode)
                    {
                        case -2147015902: //Number of requests exceeded the limit of 6000 over time window of 300 seconds.
                        case -2147015903:  //Combined execution time of incoming requests exceeded limit of 1,200,000 milliseconds over time window of 300 seconds. Decrease number of concurrent requests or reduce the duration of requests and try again later.
                        case -2147015898: //Number of concurrent requests exceeded the limit of 52.

                            return true;

                    }

                    Console.WriteLine($"FaultException<OrganizationServiceFault>\n{serviceex.Message}\n{serviceex.Detail.ErrorCode}");
                    Console.WriteLine(string.Join("\n", serviceex.Detail.ErrorDetails.Select(kv => $"{kv.Key}={kv.Value}")));

                }
                else if (aggreex.InnerException is CommunicationException comex)
                {
                    if (aggreex.InnerException.HResult == -2146233088) //The HTTP request was forbidden with client authentication scheme 'Anonymous'. == The client credentials doesn't have access to CRM. 
                    {
                        return false;
                    }
                    Console.WriteLine("Failed to communicate: " + aggreex.InnerException.Message); //Network issue
                    return true;
                }
                else if (aggreex.InnerException is TimeoutException timeout)
                {
                    Console.WriteLine("Failed to communicate: " + aggreex.InnerException.Message); //Network issue
                    return true;
                }


            }

            return false;
        }
    }
}
