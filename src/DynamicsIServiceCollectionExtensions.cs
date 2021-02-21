using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Cds.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Polly;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.ServiceModel;

namespace DotNetDevOps.Extensions.PowerPlatform.DataVerse
{
    public static class DynamicsIServiceCollectionExtensions
    {
     
        
        public static EntityReference ToEntityReferenceWithKeys(this Entity entity)
        {
            if (entity.KeyAttributes.Any())
            {
                return new EntityReference(entity.LogicalName, entity.KeyAttributes);
            }
            return entity.ToEntityReference();
        }

       
        public static T Retrieve<T>(this EntityReference reference, IOrganizationService service, ColumnSet columnSet) where T : Entity
        {
            try
            {
                var resp = service.Execute<RetrieveResponse>(new RetrieveRequest
                {
                    Target = reference,
                    ColumnSet = columnSet
                });

                return resp.Entity.ToEntity<T>();
            }
            catch (FaultException<OrganizationServiceFault> ex)
            {
                if (ex.Detail.ErrorCode == -2147220969)//
                {
                    return null;
                }
                throw;
            }

        }
         
        public static T Execute<T>(this IOrganizationService client, OrganizationRequest message)

            where T : OrganizationResponse
        {
            return client.Execute(message) as T;

        }
        public static UpsertResponse Upsert(this IOrganizationService service, Entity entity, ILogger log)
        {
            var req = new UpsertRequest() { Target = entity };
            var resp = service.Execute(req) as UpsertResponse;


            entity.Id = resp.Target?.Id ?? entity.Id;
            log.LogInformation("{EntityLogicalName}|{Id} was {CreatedOrUpdated}", entity.LogicalName, entity.Id, resp.RecordCreated ? "Created" : "Updated");
            return resp;
        }

        private static ConcurrentQueue<PooledOrganizaitionService> queue = new ConcurrentQueue<PooledOrganizaitionService>();
        public static IServiceCollection AddPowerPlatform(this IServiceCollection services, bool cache = false)
        {
            services.AddSingleton<TokenService>();
            services.AddScoped((sp) =>
            {
                if (queue.Any() && queue.TryDequeue(out var result))
                {
                    result.Logger = sp.GetRequiredService<ILogger<CdsServiceClient>>(); //Would be cool to get the loger for the function scope
                    return result;
                }

                var configuration = sp.GetRequiredService<IConfiguration>();
                var uri =
                    new Uri(configuration.GetValue<string>("CDSEnvironment"));

                CdsServiceClient.MaxConnectionTimeout = TimeSpan.FromMinutes(5);
                CdsServiceClient service = CDSPolly.RetryPolicy.Execute((context) => new CdsServiceClient(uri,
                sp.GetRequiredService<TokenService>().GetTokenAsync)
                { }
                , new Context
                {
                    ["logger"] = sp.GetRequiredService<ILogger<CdsServiceClient>>(),
                });
                service.DisableCrossThreadSafeties = true;
                //if (cache)
                //    return new PooledOrganizaitonService( new CachingOrganizationService(sp.GetRequiredService<ILogger<CachingOrganizationService>>(), service, Microsoft.Azure.Cosmos.Table.CloudStorageAccount.Parse(configuration.GetValue<string>("CDSClientCacheStorageAccount"))) as IOrganizationService,queue);
                return new PooledOrganizaitionService(service, queue)
                {
                    Logger = sp.GetRequiredService<ILogger<CdsServiceClient>>()
                };


            });
            services.AddScoped(sp =>
            {
                return sp.GetRequiredService<PooledOrganizaitionService>() as IOrganizationService;




            });


            return services;
        }
    }
}
