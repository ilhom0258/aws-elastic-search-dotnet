using System.Collections.Generic;
using System.Threading.Tasks;
using AWSElasticSearchIntegration.Core;
using AWSElasticSearchIntegration.Core.DTO;

namespace AWSElasticSearchIntegration.Infrastructure.ElasticSearch
{
    public interface IElasticSearchService<T> : IBaseElasticSearch where T : class

    {
        Task<Response<IndexDto>> Index(T model);
        Task<Response<string>> IndexBulk(IList<T> properties);
    }

    public interface IBaseElasticSearch
    {
        Task<Response<SearchDto>> Search(FilterDto filterDto);
    }
}