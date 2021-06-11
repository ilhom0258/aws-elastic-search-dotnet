using AWSElasticSearchIntegration.Core.Enums;

namespace AWSElasticSearchIntegration.Core
{
    /// <summary>
    /// Common response for interacting between microservices
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Response<T> where T : class
    {
        public Errors Code { get; set; }
        public T Payload { get; set; }
        public string Message { get; set; }
        public string State { get; set; }
    }
}