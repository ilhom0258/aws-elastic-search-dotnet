using AWSElasticSearchIntegration.Core.Configs;
using AWSElasticSearchIntegration.Core.Enums;
using AWSElasticSearchIntegration.Core.Models;
using AWSElasticSearchIntegration.Infrastructure.ElasticSearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nest;

namespace AWSElasticSearchIntegration.Core.Extensions
{
    public static class ServiceCollectionExtension
    {
        public static void AddElasticClient(this IServiceCollection services, IConfiguration conf)
        {
            var awsConfig = conf.GetSection("AwsConfig").Get<AwsConfig>();
            var connSetting = new ConnectionSettings(awsConfig.Uri);
            if (!string.IsNullOrEmpty(awsConfig.Login))
            {
                connSetting.BasicAuthentication(awsConfig.Login, awsConfig.Password);
            }
            connSetting.DefaultMappingFor<Property>(x => x.IndexName(Indexes.Properties.GetDescription()))
                .DefaultMappingFor<Mgmt>(x => x.IndexName(Indexes.Management.GetDescription()));
            services.AddSingleton<IElasticClient>(new ElasticClient(connSetting));

            services.AddTransient(typeof(IElasticSearchService<>),typeof(ElasticSearchService<>));
        }
    }
}