using System;
using System.Reflection;

namespace DependencyInjection
{
    internal sealed class CycleDependencyException : Exception
    {
        public CycleDependencyException(Type type, ConstructorInfo ctor)
            : base($"Detected cycle dependency for type {type} in ctor {ctor.ReflectedType}")
        {
        }
    }
}