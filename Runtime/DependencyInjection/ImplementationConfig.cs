using System;

namespace DependencyInjection
{
    public class ImplementationConfig
    {
        public Type Impl { get; }
        public bool IsSingleton { get; }

        public ImplementationConfig(Type impl, bool isSingleton)
        {
            Impl = impl;
            IsSingleton = isSingleton;
        }
    }
}