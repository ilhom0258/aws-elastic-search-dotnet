using System.Collections.Generic;
using System.Threading.Tasks;
using AWSElasticSearchIntegration.Core;
using AWSElasticSearchIntegration.Core.DTO;
using AWSElasticSearchIntegration.Core.Models;
using AWSElasticSearchIntegration.Infrastructure.ElasticSearch;
using Microsoft.AspNetCore.Mvc;

namespace AWSElasticSearchIntegration.Controllers
{
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    [ApiController]
    public class PropertyController : ControllerBase
    {
        private IElasticSearchService<Property> _elasticService;

        public PropertyController(IElasticSearchService<Property> elasticService)
        {
            _elasticService = elasticService;
        }

        [HttpPost("Post")]
        public async Task<Response<IndexDto>> Post([FromBody] Property property)
        {
            return await _elasticService.Index(property);
        }

        [HttpPost("Post/Bulk")]
        public async Task<Response<string>> PostBulk([FromBody] IList<Property> properties)
        {
            return await _elasticService.IndexBulk(properties);
        }

        [HttpPost("Find")]
        public async Task<Response<SearchDto>> Find([FromBody] FilterDto filterDto)
        {
            return await _elasticService.Search(filterDto);
        }

        [HttpDelete("DeleteAll")]
        public async Task<Response<string>> DeleteAllAsync()
        {
            return await _elasticService.DeleteIndexAsync();
        } 
    }
}