using System.Collections.Generic;
using System.Threading.Tasks;
using AWSElasticSearchIntegration.Core;
using AWSElasticSearchIntegration.Core.DTO;

namespace AWSElasticSearchIntegration.Infrastructure.ElasticSearch
{
    public interface IElasticSearchService<T> : IBaseElasticSearch

    {
    Task<Response<IndexDto>> Index(T model);
    Task<Response<string>> IndexBulk(IList<T> properties);
    }

    public interface IBaseElasticSearch
    {
        Task<Response<PropertySearchDto>> Search(FilterDto filterDto);
    }
}