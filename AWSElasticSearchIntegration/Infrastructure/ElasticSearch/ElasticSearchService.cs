using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AWSElasticSearchIntegration.Core;
using AWSElasticSearchIntegration.Core.DTO;
using AWSElasticSearchIntegration.Core.Enums;
using AWSElasticSearchIntegration.Core.Extensions;
using AWSElasticSearchIntegration.Core.Models;
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

        public async Task<Response<PropertySearchDto>> Search(FilterDto filterDto)
        {
            var resp = new Response<PropertySearchDto>();
            var payload = new PropertySearchDto();
            var sd = new SearchDescriptor<Property>()
                .From(filterDto.From)
                .Size(filterDto.Size == 0 ? 25 : filterDto.Size)
                .Index(Indexes.Properties.GetDescription()).TrackTotalHits();
            var qContainers = new List<QueryContainer>();
            var qd = new QueryContainerDescriptor<Property>();

            try
            {
                var q1 = qd.Fuzzy(m => m
                    .Fuzziness(Fuzziness.Auto)
                    .Transpositions(true)
                    .Field(f => f.Name)
                    .Field(f => f.FormerName)
                    .Field(f => f.City)
                    .Field(f => f.State)
                    .Field(f => f.StreetAddress)
                    .Value(filterDto.SearchPhrase)
                );
                qContainers.Add(q1);
                if (filterDto.Markets != null && filterDto.Markets.Count > 0)
                {
                    var q2 = GenerateMultiSearch(filterDto.Markets);
                    var marketResp = await _client.SearchAsync<Mgmt>(s => s
                        .Query(q => q
                            .Bool(b => b
                                .Must(q2))));
                    payload.ManagementCount = marketResp.Total;
                    payload.Managements = marketResp.Documents.Select(t => t);
                    qContainers.Add(q2);
                }

                sd.Query(q => q
                    .Bool(b => b
                        .Must(qContainers.ToArray())
                    )
                );
                var searchResult = await _client.SearchAsync<Property>(sd);
                payload.PropertyCount = searchResult.Total;
                payload.Properties = searchResult.Hits.Select(h => h.Source);
                resp.Payload = payload;
                if (payload.PropertyCount <= 0)
                {
                    resp.Code = Errors.NotFound;
                    resp.State = States.Failed.GetDescription();
                    resp.Message = Errors.NotFound.GetDescription();
                    return resp;
                }

                resp.Code = Errors.Success;
                resp.Message = Errors.Success.GetDescription();
                resp.State = States.Success.GetDescription();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in while searching in {MethodBase.GetCurrentMethod()?.Name} Exception : {ex}");
            }
            return resp;
        }
        
        public async Task<Response<IndexDto>> Index(T model)
        {
            var result = new Response<IndexDto>();
            try
            {
                var index = model.GetDisplayName();
                await CreateIndexAsync(index);
                var indexResp = await _client.IndexAsync(model, x => x.Index(index));
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
                var index = properties.FirstOrDefault().GetDisplayName();
                var isIndexExists = await CreateIndexAsync(index);
                if (!isIndexExists)
                {
                    result.Code = Errors.Failed;
                    result.Message = Errors.Failed.GetDescription();
                    result.State = States.Failed.GetDescription();
                    return result;
                }
                var waitHandle = new CountdownEvent(1);
                var bulkResp = _client.BulkAll(properties, b => b
                    .Index(index)
                    .BackOffRetries(23)
                    .BackOffTime("30s")
                    .RefreshOnCompleted(true)
                    .MaxDegreeOfParallelism(4));
                bulkResp.Subscribe(new BulkAllObserver(
                        response =>
                        {
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
                            _logger.LogInformation($"Bulk insert for {index} completed");
                            waitHandle.Signal();
                        }
                ));
                waitHandle.Wait();
            }
            catch (Exception ex)
            {
                result.Code = Errors.InternalError;
                result.Message = Errors.InternalError.GetDescription();
                result.State = States.Failed.GetDescription();
                _logger.LogError("Elastic Search service: Bulk index (internal error) \n Exception : {@ex} ", ex);
            }

            return result;
        }

        /// <summary>
        /// method for creating custom index over the provided models
        /// </summary>
        /// <param name="index">doc name in order to create</param>
        /// <returns>nothing to return</returns>
        private async Task<bool> CreateIndexAsync(string index)
        {
            try
            {
                var indexExistsResponse = await _client.Indices.ExistsAsync(index);
                if (!indexExistsResponse.Exists)
                {
                    var createIndexResponse = await _client.Indices.CreateAsync(index,
                        c => c
                            .Settings(s => s
                                .Analysis(a => a
                                    .Analyzers(ad => ad
                                        .Standard("standard_english", sa => sa
                                            .StopWords("_english_") 
                                        )
                                        .Custom("partial_text", ca => ca
                                            .Filters("lowercase", "edge_ngrams")
                                            .Tokenizer("standard"))
                                        // give the custom analyzer a name
                                        .Custom("full_text", ca => ca
                                            .Tokenizer("standard")
                                            .Filters("lowercase", "stop", "standard", "snowball")
                                        )
                                    )
                                )
                            )
                            .Map<T>(d => d
                                .AutoMap()
                            )
                    );
                    if (createIndexResponse.IsValid && createIndexResponse.OriginalException == null)
                    {
                        return true;
                    }

                    _logger.LogError("Error while creating index \nResponse = {@resp}", createIndexResponse);
                    return false;
                }

                return true; 
            }
            catch (Exception ex)
            {
                _logger.LogError("Error while creating index \nException : {@ex}", ex);
                return false;
            }
        }
        private static QueryContainer GenerateMultiSearch(IList<string> markets)
        {
            return new QueryContainerDescriptor<Property>().Bool(
                b => b.Should(
                    GenerateDescription(markets)
                )
            );
        }
        private static QueryContainer[] GenerateDescription(IEnumerable<string> markets)
        {
            return markets.Select(item => new MatchPhraseQuery {Field = "market", Query = item})
                .Select(orQuery => (QueryContainer) orQuery).ToArray();
        }
    }
}