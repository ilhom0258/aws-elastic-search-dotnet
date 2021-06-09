using System.Collections.Generic;
using AWSElasticSearchIntegration.Core.Models;

namespace AWSElasticSearchIntegration.Core.DTO
{
    public class PropertySearchDto
    {
        public long PropertyCount { get; set; }
        public long ManagementCount { get; set; }
        public IEnumerable<Property> Properties{ get; set; }
        public IEnumerable<Mgmt> Managements { get; set; }
    }
}