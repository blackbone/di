using System;
using System.Collections.Generic;
using System.Linq;

namespace DependencyInjection
{
    public static class TypeExtensions
    {
        public static IEnumerable<Type> GetInterfaces(this Type type, bool includeInherited = false)
        {
            if (includeInherited || type.BaseType == null)
                return type.GetInterfaces();

            return type.GetInterfaces().Except(type.BaseType.GetInterfaces());
        }
    }
}