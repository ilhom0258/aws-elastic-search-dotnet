﻿using System.Collections.Generic;
using AWSElasticSearchIntegration.Core.Models;

namespace AWSElasticSearchIntegration.Core.DTO
{
    public class PropertySearchDto
    {
        public long PropertyCount { get; set; }
        public long ManagementCount { get; set; }
        public IList<Property> Properties{ get; set; }
        public IList<Mgmt> Managements { get; set; }
    }
}