using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Mahkias.Core.Helpers
{
    public static class PropertyInfoExtensions
    {
        public static IEnumerable<T> GetAttributes<T>(this PropertyInfo propertyInfo) where T : Attribute
        {
            return propertyInfo.GetCustomAttributes(typeof(T), true).Cast<T>();
        }

        public static bool IsMarkedWith<T>(this PropertyInfo propertyInfo) where T : Attribute
        {
            return propertyInfo.GetAttributes<T>().Any();
        }
    }
}
