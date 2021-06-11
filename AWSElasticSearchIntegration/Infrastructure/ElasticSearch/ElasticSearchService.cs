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
        private readonly ILogger _logger;

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
                .Index(Indexes.Properties.GetDescription())
                .TrackTotalHits();
            var qContainers = new List<QueryContainer>();
            var qd = new QueryContainerDescriptor<Property>();

            try
            {
                var q1 = qd.Match(m => m
                    .Fuzziness(Fuzziness.Auto)
                    .Field(f => f.Name)
                    .Field(f => f.FormerName)
                    .Query(filterDto.SearchPhrase)
                );
                qContainers.Add(q1);

                if (!string.IsNullOrEmpty(filterDto.City))
                {
                    var q2 = qd.Match(m => m
                        .Fuzziness(Fuzziness.Auto)
                        .Field(f => f.City)
                        .Query(filterDto.City)
                    );
                    qContainers.Add(q2);
                }

                if (!string.IsNullOrEmpty(filterDto.State))
                {
                    var q2 = qd.Match(m => m
                        .Fuzziness(Fuzziness.Auto)
                        .Field(f => f.State)
                        .Query(filterDto.State)
                    );
                    qContainers.Add(q2);
                }

                if (!string.IsNullOrEmpty(filterDto.StreetAddress))
                {
                    var q2 = qd.Match(m => m
                        .Fuzziness(Fuzziness.Auto)
                        .Field(f => f.StreetAddress)
                        .Query(filterDto.StreetAddress)
                    );
                    qContainers.Add(q2);
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
                var searchResult = await _client.SearchAsync<Property>(sd);
                payload.PropertyCount = searchResult.Total;
                payload.Properties = searchResult.Hits.Select(h => h.Source);
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
        
        public async Task<Response<IndexDto>> Index(T model)
        {
            var result = new Response<IndexDto>();
            try
            {
                var index = model.GetDisplayName();
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