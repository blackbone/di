using System;
using System.Collections.Generic;

namespace DependencyInjection
{
    /// <inheritdoc />
    /// <summary>
    ///     The interface declaration for the IoC container
    /// </summary>
    public interface IContainer : IDisposable
    {
        /// <summary>
        ///     Returns all constructed singleton instances that met generic restriction.
        /// </summary>
        /// <typeparam name="T">Generic restriction argument.</typeparam>
        /// <exception cref="InvalidOperationException">In case when container not yet started.</exception>
        /// <returns>Enumeration of matching instances.</returns>
        IEnumerable<T> GetAllInstances<T>();

        /// <summary>
        ///     Allow to register the implementation instance using typed API parameter.
        /// </summary>
        /// <param name="isSingleton">Is resolved instance will be stored as singleton.</param>
        void Register<T>(in bool isSingleton = true);

        /// <summary>
        ///     Allow to register the implementation instance using typed API parameter.
        /// </summary>
        /// <typeparam name="TApi">The API type can be non interface, but must be assignable from implementation.</typeparam>
        /// <param name="instance">The implementation instance.</param>
        void Register<TApi>(in TApi instance);

        /// <summary>
        ///     Allow to register the implementation instance using typed API parameter.
        /// </summary>
        /// <param name="api">The API type can be non interface, but must be assignable from implementation.</param>
        /// <param name="instance">The implementation instance.</param>
        void Register(in Type api, in object instance);

        /// <summary>
        ///     Allow to register interface/implementation pair in the IoC container.
        /// </summary>
        /// <typeparam name="TApi">The API type can be non interface, but must be assignable from implementation.</typeparam>
        /// <typeparam name="TType">The implementation type.</typeparam>
        /// <param name="isSingleton">Is resolved instance will be stored as singleton</param>
        void Register<TApi, TType>(in bool isSingleton = true) where TType : TApi;

        /// <summary>
        ///     Allow to check is the given API type is registered in the IoC container
        /// </summary>
        /// <param name="api">The typed API parameter</param>
        /// <returns>Returns true if the give API type is registered in the IoC container otherwise returns false</returns>
        bool IsRegistered(in Type api);

        /// <summary>
        ///     Allow to resolve the API using the given type parameter
        /// </summary>
        /// <param name="value"></param>
        /// <param name="arguments"></param>
        /// <returns>Returns resolved API implementation instance</returns>
        bool Resolve<T>(out T value, params object[] arguments);

        /// <summary>
        ///     Allow to resolve the API using the given type parameter
        /// </summary>
        /// <param name="type"></param>
        /// <param name="arguments"></param>
        /// <returns>Returns resolved API implementation instance</returns>
        object Resolve(in Type type, params object[] arguments);

        /// <summary>
        ///     Injects dependencies to target object.
        /// </summary>
        /// <param name="target">target for injection.</param>
        void Inject(in object target);

        /// <summary>
        ///     Creates new empty container.
        /// </summary>
        static IContainer Create()
        {
            return new Container();
        }

        /// <summary>
        ///     Resolves all static registrations calling corresponding constructors and so on.
        /// </summary>
        void Run();

        /// <summary>
        ///     Register temporary object in container.
        ///     Value will be available through <see cref="Transiency.ITransientAccess" />.
        /// </summary>
        /// <param name="key"> Transient object identifier. </param>
        /// <param name="value"> Exact object. </param>
        bool RegisterTransientObject(in object key, in object value);
        
        /// <summary>
        ///     Unregister temporary object.
        ///     Value will not be available through <see cref="Transiency.ITransientAccess" /> even if it was resolved when it was.
        /// </summary>
        /// <param name="key"> Transient object identifier. </param>
        bool UnregisterTransientObject(in object key);
        
        /// <summary>
        ///     Unregister temporary object.
        ///     Value will not be available through <see cref="Transiency.ITransientAccess" /> even if it was resolved when it was.
        /// </summary>
        /// <param name="key"> Transient object identifier. </param>
        /// <param name="value"> Unregistered transient object. Default if key is not presented or type mismatch. </param>
        bool UnregisterTransientObject<T>(in object key, out T value);

        /// <summary>
        ///     Tries to get transient object from container by it's key.
        /// </summary>
        /// <param name="key"> Transient object identifier. </param>
        /// <param name="value"> Resulting transient object. Default if key is not presented or type mismatch. </param>
        /// <typeparam name="T"> Type of access to transient object. </typeparam>
        /// <returns></returns>
        bool TryGetTransientObject<T>(in object key, out T value);
    }
}