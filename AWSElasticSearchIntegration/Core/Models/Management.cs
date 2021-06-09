using System.ComponentModel;
using Nest;

namespace AWSElasticSearchIntegration.Core.Models
{
    [DisplayName("managements")]
    [ElasticsearchType(RelationName = "properties")]
    public class Mgmt
    {
        public int MgmtId { get; set;}
        [Text(Analyzer = "partial_text")]
        public string Name { get; set; }
        [Text(Analyzer = "partial_text")]
        public string Market { get; set; }
        [Text(Analyzer = "partial_text")]
        public string State { get; set; }     
    }
}