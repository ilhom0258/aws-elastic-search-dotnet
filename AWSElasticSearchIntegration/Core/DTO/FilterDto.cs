using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AWSElasticSearchIntegration.Core.DTO
{
    public class FilterDto
    {
        [Required]
        public string SearchPhrase { get; set; }

        public string City { get; set; }

        public string StreetAddress { get; set; }
        public string State { get; set; }

        public IList<string> Markets { get; set; }
        // limit and offset configuration
        public int From { get; set; }
        public int Size { get; set; }
    }
}