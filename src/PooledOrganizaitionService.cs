using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Xml.Linq;

namespace DotNetDevOps.Extensions.PowerPlatform.DataVerse
{

    public class PooledOrganizaitionService : IDisposable, IOrganizationService
    {
        public bool UseWebApi { get; set; } = false;
        private readonly ServiceClient service;
        private readonly ConcurrentQueue<PooledOrganizaitionService> queue;

        public ILogger Logger { get; set; }

        public PooledOrganizaitionService(ServiceClient service, ConcurrentQueue<PooledOrganizaitionService> queue)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));

        }

        public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            this.service.Associate(entityName, entityId, relationship, relatedEntities);
        }

        public Guid Create(Entity entity)
        {
            using (Logger.BeginScope(new Dictionary<string, string> { { "CreateOperationId", Guid.NewGuid().ToString() } }))
            {
                Logger.LogInformation("Creating Entity<{EntityLogicalName}>", entity.LogicalName);
                var createMessage = new CreateRequest
                {
                    Target = entity,
                };



                var createResponse = Execute<CreateResponse>(new CreateRequest
                {
                     Target = entity
                });
                entity.Id = createResponse.id;

                Logger.LogInformation("Created Entity<{EntityLogicalName}> with {Id}", entity.LogicalName, entity.Id);
                return entity.Id;

            }


        }
         
      
        


        public void Delete(string entityName, Guid id)
        {
            var response = Execute<DeleteResponse>(new DeleteRequest
            {
                Target = new EntityReference(entityName, id)
            });

        }

        public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            service.Disassociate(entityName, entityId, relationship, relatedEntities);
        }

      
        public void Dispose()
        {

            queue.Enqueue(this);

        }
        object lockobj = new object();
        public OrganizationResponse Execute(OrganizationRequest request)
        {
            lock (lockobj)
            {
                using (Logger.BeginScope(new Dictionary<string, string> { { "ExecuteOperationId", Guid.NewGuid().ToString() } }))
                {


                    if (request is ExecuteMultipleRequest multipleRequest)
                    {
                        Logger.LogInformation("Executing Operation<{RequestName}> with {RequestCount}", request.RequestName, multipleRequest.Requests.Count);
                    }
                    else
                    {
                        Logger.LogInformation("Executing Operation<{RequestName}>", request.RequestName);
                    }

                    var response = service.ExecuteOrganizationRequest(request, useWebAPI: UseWebApi);

                    if (response == null)
                    {
                        Logger.LogInformation(service.LastException, "Execution Failed for Operation<{RequestName}>", request.RequestName);
                        throw service.LastException;
                    }



                    Logger.LogInformation("Executed Operation<{RequestName}>", request.RequestName);


                    return response;
                }
            }
        }
        public T Execute<T>(OrganizationRequest request) where T : OrganizationResponse
        {
            return Execute(request) as T;
        }

        public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
        {

            var response = Execute<RetrieveResponse>(new RetrieveRequest
            {
                ColumnSet = columnSet,
                Target = new EntityReference(entityName, id)
            });

            return response.Entity;
        }

        public EntityCollection RetrieveMultiple(QueryBase query)
        {
            var a = Execute<RetrieveMultipleResponse>(new RetrieveMultipleRequest
            {
                Query = query
            });
            return a.EntityCollection;
        }

        public void Update(Entity entity)
        {
            var response = Execute<UpdateResponse>(new UpdateRequest
            {
                Target = entity
            });

        }
    }
}
