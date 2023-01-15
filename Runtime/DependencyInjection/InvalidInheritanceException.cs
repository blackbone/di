using System;

namespace DependencyInjection
{
    internal class InvalidInheritanceException : Exception
    {
        public InvalidInheritanceException(Type api, Type impl)
            : base($"Abstraction inheritance is invalide for api: {api.Name} and impl: {impl.Name}")
        {
        }
    }
}