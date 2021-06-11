using System.ComponentModel;
using Nest;

namespace AWSElasticSearchIntegration.Core.Models
{
    [DisplayName("managements")]
    [ElasticsearchType(RelationName = "managements")]
    public class Mgmt
    {
        public int MgmtId { get; set;}
        // [Text(Analyzer = "full_text")]
        public string Name { get; set; }
        // [Text(Analyzer = "full_text")]
        public string Market { get; set; }
        // [Text(Analyzer = "full_text")]
        public string State { get; set; }     
    }
}