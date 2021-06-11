using System.ComponentModel;
using Nest;

namespace AWSElasticSearchIntegration.Core.Models
{
    [DisplayName("properties")]
    [ElasticsearchType(RelationName = "properties")]
    public class Property
    {
        public int PropertyId { get; set; }
        // [Text(Analyzer = "full_text", SearchAnalyzer = "full_text")] 
        public string Name { get; set; }

        // [Text(Analyzer = "full_text", SearchAnalyzer = "full_text")] 
        public string FormerName { get; set; }

        // [Text(Analyzer = "full_text", SearchAnalyzer = "full_text")] 
        public string StreetAddress { get; set; }

        // [Text(Analyzer = "full_text", SearchAnalyzer = "full_text")] 
        public string City { get; set; }
        
        // [Text(Analyzer = "full_text", SearchAnalyzer = "full_text")] 
        
        public string Market { get; set; }

        // [Text(Analyzer = "full_text", SearchAnalyzer = "full_text")] 
        public string State { get; set; }
        public float Lat { get; set; }
        public float Lng { get; set; }
    }
}