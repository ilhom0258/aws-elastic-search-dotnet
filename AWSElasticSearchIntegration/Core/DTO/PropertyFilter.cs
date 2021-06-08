using System.Collections.Generic;

namespace AWSElasticSearchIntegration.Core.DTO
{
    public class PropertyFilter
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Address { get; set; }
        public IList<string> Markets { get; set; }
        // limit and offset configuration
        public int From { get; set; }
        public int Size { get; set; }
    }
}