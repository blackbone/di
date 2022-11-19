namespace DependencyInjection
{
    using System;

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class InjectAttribute : Attribute
    {
    }
}