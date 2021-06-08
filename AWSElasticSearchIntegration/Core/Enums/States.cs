using System.ComponentModel;

namespace AWSElasticSearchIntegration.Core.Enums
{
    public enum States
    {
        [Description("Success")]
        Success,
        [Description("Failed")]
        Failed,
        [Description("Pending")]
        Pending
    }
}