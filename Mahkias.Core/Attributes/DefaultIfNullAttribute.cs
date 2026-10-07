using System;

namespace Mahkias.Core.Attributes
{
    public class DefaultIfNullAttribute : Attribute
    {
        public DefaultIfNullAttribute()
        {
        }

        public DefaultIfNullAttribute(object defaultValue)
        {
            DefaultValue = defaultValue;
        }

        public object DefaultValue { get; }
    }
}