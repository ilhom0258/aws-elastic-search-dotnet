using System.Collections.Generic;
using AWSElasticSearchIntegration.Core.Models;

namespace AWSElasticSearchIntegration.Core.DTO
{
    public class SearchDto
    {
        public long PropertyCount { get; set; }
        public long ManagementCount { get; set; }
        public long TimeSpent { get; set; } // in milliseconds
        public IEnumerable<Property> Properties{ get; set; }
        public IEnumerable<Mgmt> Managements { get; set; }
    }
}