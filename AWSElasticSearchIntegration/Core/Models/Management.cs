using System.ComponentModel;
using Nest;

namespace AWSElasticSearchIntegration.Core.Models
{
    [DisplayName("managements")]
    [ElasticsearchType(RelationName = "managements")]
    public class Mgmt
    {
        public int MgmtId { get; set;}
        public string Name { get; set; }
        public string Market { get; set; }
        public string State { get; set; }     
    }
}