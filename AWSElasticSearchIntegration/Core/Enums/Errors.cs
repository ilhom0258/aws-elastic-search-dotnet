using System.ComponentModel;

namespace AWSElasticSearchIntegration.Core.Enums
{
    /// <summary>
    /// Common error codes for interacting between services
    /// </summary>
    public enum Errors
    {
        [Description("Success")]
        Success = 1000,
        [Description("Failed")]
        Failed = 1001,
        [Description("Bad request")]
        BadRequest = 1003,
        [Description("Something bad happened")]
        InternalError = 1004,
        [Description("Timeout of service")]
        Timeout = 1005,
        [Description("Upstream service unavailable")]
        ServiceUnavailable = 1006
    }
}