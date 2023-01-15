using System;

namespace DependencyInjection
{
    public sealed class ConstructorNotFoundException : Exception
    {
        public ConstructorNotFoundException(Type type)
            : base($"Suitable constructor has not been found for type {type.FullName}")
        {
        }
    }
}