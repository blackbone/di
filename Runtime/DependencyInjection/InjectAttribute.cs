using System;

namespace DependencyInjection
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class InjectAttribute : Attribute
    {
    }
}