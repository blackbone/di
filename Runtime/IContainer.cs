namespace DependencyInjection
{
    using System;
    using System.Collections.Generic;

    /// <inheritdoc />
    /// <summary>
    /// The interface declaration for the IoC container
    /// </summary>
    public interface IContainer : IDisposable
    {
        /// <summary>
        /// Returns all constructed singleton instances that met generic restriction.
        /// </summary>
        /// <typeparam name="T">Generic restriction argument.</typeparam>
        /// <exception cref="InvalidOperationException">In case when container not yet started.</exception>
        /// <returns>Enumeration of matching instances.</returns>
        IEnumerable<T> GetAllInstances<T>();
        
        /// <summary>
        /// Allow to register the implementation instance using typed API parameter.
        /// </summary>
        /// <param name="isSingleton">Is resolved instance will be stored as singleton.</param>
        void Register<T>(bool isSingleton = true);
        
        /// <summary>
        /// Allow to register the implementation instance using typed API parameter.
        /// </summary>
        /// <param name="api">The API typed parameter.</param>
        /// <param name="instance">The implementation instance.</param>
        void Register<T>(T instance);
        
        /// <summary>
        /// Allow to register interface/implementation pair in the IoC container.
        /// </summary>
        /// <typeparam name="TApi">The API type can be non interface, but must be assignable from implementation.</typeparam>
        /// <typeparam name="TType">The implementation type.</typeparam>
        /// <param name="isSingleton">Is resolved instance will be stored as singleton</param>
        void Register<TApi, TType>(bool isSingleton = true) where TType : TApi;
        
        /// <summary>
        /// Allow to check is the given API type is registered in the IoC container
        /// </summary>
        /// <param name="api">The typed API parameter</param>
        /// <returns>Returns true if the give API type is registered in the IoC container otherwise returns false</returns>
        bool IsRegistered(Type api);

        /// <summary>
        /// Allow to resolve the API using the given type parameter 
        /// </summary>
        /// <returns>Returns resolved API implementation instance</returns>
        T Resolve<T>();

        /// <summary>
        /// Allow to resolve the API using the given type parameter 
        /// </summary>
        /// <param name="type"></param>
        /// <returns>Returns resolved API implementation instance</returns>
        object Resolve(Type type);

        /// <summary>
        /// Injects dependencies to target object.
        /// </summary>
        /// <param name="target">target for injection.</param>
        void Inject(object target);

        /// <summary>
        /// Creates new empty container.
        /// </summary>
        static IContainer Create() => new Container();

        /// <summary>
        /// Resolves all static registrations calling corresponding constructors and so on.
        /// </summary>
        void Run();
    }
}