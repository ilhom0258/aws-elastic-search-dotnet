using System;
using System.Collections.Generic;

namespace AWSElasticSearchIntegration.Core.Configs
{
    public class AwsConfig
    {
        public string Login { get; set; }
        public string Password { get; set; }
        public string Url { get; set; }
        public Uri Uri => new Uri(Url);
    }
}