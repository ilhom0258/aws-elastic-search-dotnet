using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AWSElasticSearchIntegration.Core;
using AWSElasticSearchIntegration.Core.DTO;
using AWSElasticSearchIntegration.Core.Enums;
using AWSElasticSearchIntegration.Core.Extensions;
using AWSElasticSearchIntegration.Core.Models;
using Elasticsearch.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Nest;

namespace AWSElasticSearchIntegration.Infrastructure.ElasticSearch
{
    public class ElasticSearchService<T>: IElasticSearchService<T> where T:class
    {
        private readonly IElasticClient _client;
        private ILogger _logger;

        public ElasticSearchService(IElasticClient client, ILoggerFactory loggerFactory)
        {
            _client = client;
            _logger = loggerFactory.CreateLogger("ElasticSearchService");
        }
        
        public async Task<Response<PropertySearchDto>> Search(PropertyFilter filter)
        {
            var resp = new Response<PropertySearchDto>();
            var payload = new PropertySearchDto();
            var sd = new SearchDescriptor<Property>()
                .From(filter.From)
                .Size(filter.Size == 0 ? 25 : filter.Size)
                .Index(Indexes.Properties.GetDescription());
            var qContainers = new List<QueryContainer>();
            var qd = new QueryContainerDescriptor<Property>();

            if (!string.IsNullOrEmpty(filter.City))
            {
                var q = qd.Match(m => m
                    .Field(f => f.City)
                    .Query(filter.City));
                qContainers.Add(q);
            }

            if (!string.IsNullOrEmpty(filter.Name))
            {
                var q = qd.Match(m => m
                    .Field(f => f.Name)
                    .Field(f => f.FormerName)
                    .Query(filter.Name)
                );
                qContainers.Add(q);
            }

            if (!string.IsNullOrEmpty(filter.State))
            {
                var q = qd.Match(m => m
                    .Field(f => f.State)
                    .Query(filter.State)
                );
                qContainers.Add(q);
            }

            if (!string.IsNullOrEmpty(filter.Address))
            {
                var q = qd.Match(m => m
                    .Field(f => f.StreetAddress)
                    .Query(filter.Address)
                );
                qContainers.Add(q);
            }

            if (filter.Markets != null && filter.Markets.Any())
            {
                var q = qd.Terms(t => t
                    .Field(f => f.Market)
                    .Terms(filter.Markets.ToArray())
                );
                qContainers.Add(q);
            }

            sd.Query(q => q
                .Bool(b => b
                    .Must(qContainers.ToArray())));
            var searchResult = await _client.SearchAsync<Property>();
            payload.PropertyCount = searchResult.Total;
            payload.Properties = searchResult.Hits.Select(h => h.Source).ToList();
            resp.Payload = payload;
            return resp;
        }

        public async Task<Response<IndexDto>> Index(T model)
        {
            var result = new Response<IndexDto>();
            try
            {
                var indexResp = await _client.IndexAsync(model, x => x.Index(model.GetName()));
                if (indexResp.IsValid && !string.IsNullOrEmpty(indexResp.Id))
                {
                    result.Code = Errors.Success;
                    result.Message = Errors.Success.GetDescription();
                    result.Payload = new IndexDto() {Id = indexResp.Id};
                    return result;
                }
                result.Code = Errors.Failed;
                result.Message = Errors.Failed.GetDescription();    
                _logger.LogError("Elastic Search Service : Index (not indexed) \n Request : {@model} \n Response \n{@indexResp}", model, indexResp);
            }
            catch (Exception ex)
            {
                result.Code = Errors.InternalError;
                result.Message = Errors.InternalError.GetDescription();
                _logger.LogError("Elastic Search Service : Index (not indexed) \n Request : {@model}, \n Exception : {@ex}", model, ex);
            }
            
            return result;
        }

        public async Task<Response<string>> IndexBulk(IList<T> properties)
        {
            var result = new Response<string>();
            try
            {
                var waitHandle = new CountdownEvent(1);
                var bulkResp = _client.BulkAll(properties, b => b
                    .Index(properties.FirstOrDefault().GetName())
                    .BackOffRetries(2)
                    .BackOffTime("30s")
                    .RefreshOnCompleted(true)
                    .MaxDegreeOfParallelism(4));
                bulkResp.Subscribe(new BulkAllObserver(
                        response =>
                        {
                            _logger.LogInformation($"Indexed {response.Items} with {response.Retries} retries");
                        },
                        ex =>
                        {
                            _logger.LogError("BulkAll Error : {0}", ex);
                            waitHandle.Signal();
                        },
                        () =>
                        {
                            result.Code = Errors.Success;
                            result.Message = Errors.Success.GetDescription();
                            result.State = States.Success.GetDescription();
                            _logger.LogInformation("Bulk insert of ");
                            waitHandle.Signal();
                        }
                ));
                waitHandle.Wait();
            }
            catch (Exception ex)
            {
                _logger.LogError("Elastic Search service: Bulk index (internal error) \n Exception : {@ex} ", ex);
            }

            return result;
        }
    }
}