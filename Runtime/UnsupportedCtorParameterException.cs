namespace DependencyInjection
{
    using System;
    using System.Reflection;

    internal class UnsupportedCtorParameterException : Exception
    {
        public UnsupportedCtorParameterException(Type type, ConstructorInfo ctor)
            : base($"Constructor parameter type {type.Name} is not supported in ctor {ctor.ReflectedType}")
        {
        }
    }
}