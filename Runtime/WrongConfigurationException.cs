namespace DependencyInjection
{
    using System;

    internal class WrongConfigurationException : Exception
    {
        public WrongConfigurationException(Type type)
            : base($"Unable to find configuration for type {type.Name}")
        {
        }
    }
}