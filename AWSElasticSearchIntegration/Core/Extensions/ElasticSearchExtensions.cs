using System;
using System.Linq.Expressions;
using Nest;

namespace AWSElasticSearchIntegration.Core.Extensions
{
    public static class ElasticSearchExtensions
    {
        public static ISearchResponse<T> SearchWithMatch<T>(this IElasticClient client, string index, string query, Expression<Func<T, object>> field)
        where T : class =>
            client.Search<T>(s => s
                .From(0)
                .Size(25)
                .Index("index")
                .Query(q => q
                    .Match(m => m
                        .Field(field)
                        .Query(query)
                    )
                )
            );
    }
}