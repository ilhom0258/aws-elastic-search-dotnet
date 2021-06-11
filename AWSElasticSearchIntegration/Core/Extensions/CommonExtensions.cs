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
        public static string GetDisplayName<T>(this T obj)
        {
            var displayName = typeof(T)
                .GetCustomAttributes(typeof(DisplayNameAttribute), true)
                .FirstOrDefault() as DisplayNameAttribute;

            if (displayName != null)
                return displayName.DisplayName;

            return "";
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