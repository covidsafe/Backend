﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CovidSafe.DAL.Helpers;
using CovidSafe.DAL.Repositories.Cosmos.Client;
using CovidSafe.DAL.Repositories.Cosmos.Records;
using CovidSafe.Entities.Geospatial;
using CovidSafe.Entities.Messages;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Azure.Cosmos.Spatial;

namespace CovidSafe.DAL.Repositories.Cosmos
{
    /// <summary>
    /// CosmosDB implementation of <see cref="IMessageContainerRepository"/>
    /// </summary>
    public class CosmosMessageContainerRepository : CosmosRepository, IMessageContainerRepository
    {
        /// <summary>
        /// Creates a new <see cref="CosmosMessageContainerRepository"/> instance
        /// </summary>
        /// <param name="dbContext"><see cref="CosmosContext"/> instance</param>
        public CosmosMessageContainerRepository(CosmosContext dbContext) : base(dbContext)
        {
            // Create container reference
            this.Container = this.Context.GetContainer(
                this.Context.SchemaOptions.MessageContainerName
            );
        }

        /// <summary>
        /// Returns the most restrictive timestamp filter, based on the application 
        /// configuration and the one provided by the user
        /// </summary>
        /// <param name="timestampFilter">Original timestamp filter applied to query</param>
        /// <returns>Timestamp filter value, in ms since UNIX epoch</returns>
        private long _getTimestampFilter(long timestampFilter)
        {
            // Get the default timestamp filter value
            long defaultFilter = DateTimeOffset.UtcNow
                .AddDays(-(this.Context.SchemaOptions.MaxDataAgeToReturnDays))
                .ToUnixTimeMilliseconds();

            // If a timestamp filter was provided for the query, see if that one is more restrictive than ours
            if(timestampFilter > 0)
            {
                // Take most restrictive timestamp filter
                return Math.Max(defaultFilter, timestampFilter);
            }
            else
            {
                // Use our filter by default, if none was provided already for the query
                return defaultFilter;
            }
        }

        /// <inheritdoc/>
        public async Task<MessageContainer> GetAsync(string messageId, CancellationToken cancellationToken = default)
        {
            // Create LINQ query
            var queryable = this.Container
                .GetItemLinqQueryable<MessageContainerRecord>();

            // Execute query
            var iterator = queryable
                .Where(r =>
                    r.Id == messageId
                    && r.Version == MessageContainerRecord.CURRENT_RECORD_VERSION
                )
                .Select(r => r.Value)
                .ToFeedIterator();

            List<MessageContainer> results = new List<MessageContainer>();

            while (iterator.HasMoreResults)
            {
                results.AddRange(await iterator.ReadNextAsync());
            }

            return results.FirstOrDefault();
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<MessageContainerMetadata>> GetLatestAsync(Region region, long lastTimestamp, CancellationToken cancellationToken = default)
        {
            // Create LINQ query
            var queryable = this.Container
                .GetItemLinqQueryable<MessageContainerRecord>();

            long timeStampFilter = this._getTimestampFilter(lastTimestamp);

            // Execute query
            var iterator = queryable
                .Where(r =>
                    r.Timestamp > timeStampFilter
                    && r.Region.LatitudePrefix == region.LatitudePrefix
                    && r.Region.LongitudePrefix == region.LongitudePrefix
                    && r.Region.Precision == region.Precision
                    && r.Version == MessageContainerRecord.CURRENT_RECORD_VERSION
                )
                .Select(r => new MessageContainerMetadata
                {
                    Id = r.Id,
                    Timestamp = r.Timestamp
                })
                .ToFeedIterator();

            List<MessageContainerMetadata> results = new List<MessageContainerMetadata>();

            while(iterator.HasMoreResults)
            {
                results.AddRange(await iterator.ReadNextAsync());
            }

            return results;
        }

        /// <inheritdoc/>
        public async Task<long> GetLatestRegionSizeAsync(Region region, long lastTimestamp, CancellationToken cancellationToken = default)
        {
            // Create LINQ query
            var queryable = this.Container
                .GetItemLinqQueryable<MessageContainerRecord>();

            long timeStampFilter = this._getTimestampFilter(lastTimestamp);

            // Execute query
            var size = await queryable
                .Where(r =>
                    r.Timestamp > timeStampFilter
                    && r.Region.LatitudePrefix == region.LatitudePrefix
                    && r.Region.LongitudePrefix == region.LongitudePrefix
                    && r.Region.Precision == region.Precision
                    && r.Version == MessageContainerRecord.CURRENT_RECORD_VERSION
                )
                .Select(r => r.Size)
                .SumAsync(cancellationToken)
                .ConfigureAwait(false);

            return size;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<MessageContainer>> GetRangeAsync(IEnumerable<string> ids, CancellationToken cancellationToken)
        {
            // Create LINQ query
            var queryable = this.Container
                .GetItemLinqQueryable<MessageContainerRecord>();

            // Execute query
            var iterator = queryable
                .Where(r =>
                    ids.Contains(r.Id)
                    && r.Version == MessageContainerRecord.CURRENT_RECORD_VERSION
                )
                .Select(r => r.Value)
                .ToFeedIterator();

            List<MessageContainer> results = new List<MessageContainer>();

            while (iterator.HasMoreResults)
            {
                results.AddRange(await iterator.ReadNextAsync());
            }

            return results;
        }

        /// <inheritdoc/>
        public async Task<string> InsertAsync(MessageContainer report, Region region, CancellationToken cancellationToken = default)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }
            if (region == null)
            {
                throw new ArgumentNullException(nameof(region));
            }

            region = RegionHelper.AdjustToPrecision(region);

            // Get allowed region boundary
            RegionBoundary boundary = RegionHelper.GetRegionBoundary(region);

            var record = new MessageContainerRecord(report)
            {
                RegionBoundary = new RegionBoundary(boundary),
                Region = region,
                PartitionKey = MessageContainerRecord.GetPartitionKey(region)
            };

            ItemResponse<MessageContainerRecord> response = await this.Container
                .CreateItemAsync<MessageContainerRecord>(
                    record,
                    new PartitionKey(record.PartitionKey),
                    cancellationToken: cancellationToken
                );

            return response.Resource.Id;
        }

        public async Task InsertAsync(MessageContainer report, IEnumerable<Region> regions, CancellationToken cancellationToken = default)
        {
            // Validate inputs
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            // Prepare records to insert (grouped by partition key)
            var recordGroups = regions.Select(
                r => new MessageContainerRecord(report)
                {
                    RegionBoundary = new RegionBoundary(
                        RegionHelper.GetRegionBoundary(r)
                    ),
                    Region = RegionHelper.AdjustToPrecision(r),
                    PartitionKey = MessageContainerRecord.GetPartitionKey(r)
                }).GroupBy(r => r.PartitionKey);

            // Begin batch operation
            // All MatchMessageRecords will have same PartitionID in this batch
            var batches = recordGroups.Select(g => g.Aggregate(
                this.Container.CreateTransactionalBatch(new PartitionKey(g.Key)),
                (result, item) => result.CreateItem<MessageContainerRecord>(item)));

            // Execute transactions
            // TODO: make a single transaction. 
            var responses = await Task.WhenAll(batches.Select(b => b.ExecuteAsync(cancellationToken)));

            var failed = responses.Where(r => !r.IsSuccessStatusCode);
            if (failed.Any())
            {
                throw new Exception(
                    String.Format(
                        "{0} out of {1} insertions failed. Cosmos bulk insert failed with HTTP Status Code {2}.",
                        responses.Count(),
                        failed.Count(),
                        failed.First().StatusCode.ToString()
                )
                );
            }
        }
    }
}
