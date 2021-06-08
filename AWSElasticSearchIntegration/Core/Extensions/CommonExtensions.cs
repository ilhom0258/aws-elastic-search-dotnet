using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace AWSElasticSearchIntegration.Core.Extensions
{
    
    /// <summary>
    /// Extension methods for different types
    /// </summary>
    public static class CommonExtensions
    {
        public static string GetName(this object obj)
        {
            return obj?.GetType()
                .GetMember(obj.ToString() ?? string.Empty)
                .FirstOrDefault()
                ?.GetCustomAttributes<DisplayNameAttribute>()
                .GetName();
        }
        
        
        public static string GetDescription(this Enum value)
        {
            return value?.GetType()
                .GetMember(value.ToString())
                .FirstOrDefault()
                ?.GetCustomAttribute<DescriptionAttribute>()
                ?.Description;
        }
        
    }
}