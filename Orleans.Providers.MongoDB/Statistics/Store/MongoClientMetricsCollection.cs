﻿using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using Orleans.Providers.MongoDB.Utils;
using Orleans.Runtime;

namespace Orleans.Providers.MongoDB.Statistics.Store
{
    public class MongoClientMetricsCollection : CollectionBase<MongoClientMetricsDocument>
    {
        private static readonly UpdateOptions UpsertNoValidation = new UpdateOptions { BypassDocumentValidation = true, IsUpsert = true };
        private readonly TimeSpan expireAfter;
        private readonly string collectionPrefix;

        public MongoClientMetricsCollection(string connectionString, string databaseName, TimeSpan expireAfter, string collectionPrefix)
            : base(connectionString, databaseName)
        {
            this.expireAfter = expireAfter;
            this.collectionPrefix = collectionPrefix;
        }

        protected override string CollectionName()
        {
            return collectionPrefix + "OrleansClientMetricsTable";
        }

        protected override void SetupCollection(IMongoCollection<MongoClientMetricsDocument> collection)
        {
            if (expireAfter != TimeSpan.Zero)
            {
                collection.Indexes.CreateOne(Index.Ascending(x => x.Timestamp), new CreateIndexOptions { ExpireAfter = expireAfter });
            }
        }

        public virtual async Task UpsertReportClientMetricsAsync(
            string deploymentId,
            string clientId,
            string address,
            string hostName,
            IClientPerformanceMetrics clientMetrics)
        {
            var id = ReturnId(deploymentId, clientId, expireAfter != TimeSpan.Zero);

            var document = new MongoClientMetricsDocument
            {
                Id = id,
                Address = address,
                ClientId = clientId,
                ConnectedGateWayCount = clientMetrics.ConnectedGatewayCount,
                CpuUsage = clientMetrics.CpuUsage,
                DeploymentId = deploymentId,
                HostName = hostName,
                MemoryUsage = clientMetrics.MemoryUsage,
                ReceivedMessages = clientMetrics.ReceivedMessages,
                SendQueueLength = clientMetrics.SendQueueLength,
                SentMessages = clientMetrics.SentMessages,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                if (expireAfter != TimeSpan.Zero)
                {
                    await Collection.InsertOneAsync(document);
                }
                else
                {

                    await Collection.ReplaceOneAsync(x => x.Id == id, document, UpsertNoValidation);
                }
            }
            catch (MongoWriteException ex)
            {
                if (ex.WriteError.Category != ServerErrorCategory.DuplicateKey)
                {
                    throw;
                }
            }
        }

        private static string ReturnId(string deploymentId, string clientId, bool multiple)
        {
            var id =  $"{deploymentId}:{clientId}";

            if (multiple)
            {
                id += Guid.NewGuid();
            }

            return id;
        }
    }
}
