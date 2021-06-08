using System.ComponentModel;

namespace AWSElasticSearchIntegration.Core.Models
{
    [DisplayName("properties")]
    public class Property
    {
        public int PropertyId { get; set; }
        public string Name { get; set; }
        public string FormerName { get; set;}
        public string StreetAddress { get; set; }
        public string City { get; set; }
        public string Market { get; set; }
        public string State { get; set; }
        public string Lat { get; set;}
        public string Lng { get; set; }
    }
}