using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        private readonly ILogger _logger;
        
        private string _index = (typeof(T)
        .GetCustomAttributes(typeof(DisplayNameAttribute), true)
        .FirstOrDefault() as DisplayNameAttribute)?.DisplayName;

        public ElasticSearchService(IElasticClient client, ILoggerFactory loggerFactory)
        {
            _client = client;
            _logger = loggerFactory.CreateLogger("ElasticSearchService");
        }

        /// <summary>
        /// Searches for properties based on filter
        /// </summary>
        /// <param name="filterDto"></param>
        /// <returns>Search Dto that has data for what we searched</returns>
        public async Task<Response<SearchDto>> Search(FilterDto filterDto) 
        {
            var resp = new Response<SearchDto>();
            var payload = new SearchDto();
            var sd = new SearchDescriptor<T>()
                .From(filterDto.From)
                .Size(filterDto.Size == 0 ? 25 : filterDto.Size)
                .TrackTotalHits();
            var qContainers = new List<QueryContainer>();
            var qd = new QueryContainerDescriptor<T>();

            try
            {
                var q1 = qd.MultiMatch(m => m
                    .Fuzziness(Fuzziness.Auto)
                    .Fields(fs => fs
                        .Field("name")
                        .Field("formerName")
                        .Field("streetAddress"))
                    .Operator(Operator.Or)
                    .Query(filterDto.SearchPhrase)
                );
                qContainers.Add(q1);

                if (!string.IsNullOrEmpty(filterDto.City))
                {
                    var q = qd.Match(m => m
                        .Fuzziness(Fuzziness.Auto)
                        .Field("city")
                        .Operator(Operator.Or)
                        .Query(filterDto.City)
                    );
                    qContainers.Add(q);
                }

                if (!string.IsNullOrEmpty(filterDto.State))
                {
                    var q = qd.Match(m => m
                        .Fuzziness(Fuzziness.Auto)
                        .Field("state")
                        .Query(filterDto.State)
                    );
                    qContainers.Add(q);
                }

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
                
                var searchResult = await _client.SearchAsync<T>(sd);
                payload.PropertyCount = searchResult.Total;
                if (typeof(T) == typeof(Property))
                {
                    payload.PropertyCount = searchResult.Total;
                    payload.Properties = searchResult.Hits.Select(h => h.Source as Property);
                }
                else
                {
                    payload.ManagementCount = searchResult.Total;
                    payload.Managements = searchResult.Hits.Select(h => h.Source as Mgmt);
                }
                payload.TimeSpent = searchResult.Took;
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
        
        /// <summary>
        /// Adds a model into the specific document
        /// </summary>
        /// <param name="model">generic model</param>
        /// <returns></returns>
        public async Task<Response<IndexDto>> Index(T model)
        {
            var result = new Response<IndexDto>();
            try
            {
                var indexCreateResp = await CreateIndexAsync(_index);
                if (!indexCreateResp)
                {
                    result.Code = Errors.Failed;
                    result.Message = Errors.Failed.GetDescription();
                    result.State = States.Failed.GetDescription();
                    return result;
                }
                var indexResp = await _client.IndexAsync(model, x => x.Index(_index));
                if (indexResp.IsValid && !string.IsNullOrEmpty(indexResp.Id))
                {
                    result.Code = Errors.Success;
                    result.Message = Errors.Success.GetDescription();
                    result.State = States.Success.GetDescription();
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

        /// <summary>
        /// Performs generic bulk addition for the list of objects
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        public async Task<Response<string>> IndexBulk(IList<T> properties)
        {
            var result = new Response<string>();
            try
            {
                var indexResp = await CreateIndexAsync(_index);
                if (!indexResp)
                {
                    result.Code = Errors.Failed;
                    result.Message = Errors.Failed.GetDescription();
                    result.State = States.Failed.GetDescription();
                    return result;
                }
                var waitHandle = new CountdownEvent(1);
                var bulkResp = _client.BulkAll(properties, b => b
                    .Index(_index)
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
                            _logger.LogInformation($"Bulk insert for {_index} completed");
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
        /// Deleting index
        /// </summary>
        /// <returns>Generic response indicating the state of request</returns>
        public async Task<Response<string>> DeleteIndexAsync()
        {
            var result = new Response<string>()
            {
                Code = Errors.InternalError,
                Message = Errors.InternalError.GetDescription(),
                State = States.Failed.GetDescription()
            };
            try
            {
                var deleteIndexResp = await _client.Indices.DeleteAsync(_index);
                if (deleteIndexResp.IsValid)
                {
                    result.Code = Errors.Success;
                    result.Message = Errors.Success.GetDescription();
                    result.State = States.Success.GetDescription();
                    return result;
                }

                result.Code = Errors.Failed;
                result.Message = Errors.Failed.GetDescription();
                result.State = States.Failed.GetDescription();
            }
            catch (Exception ex)
            {
                _logger.LogError("Error while deleting index : {@ex}", ex);
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
                                            .Filters("lowercase", "edge_ngram")
                                            .Tokenizer("standard"))
                                        // give the custom analyzer a name
                                        .Custom("full_text", ca => ca
                                            .Tokenizer("standard")
                                            .Filters("lowercase", "stop", "snowball")
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