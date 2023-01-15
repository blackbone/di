using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using Transiency;

namespace DependencyInjection
{
    internal sealed class Container : IContainer
    {
        private readonly Dictionary<Type, ImplementationConfig> configurations = new();
        private readonly ConstructorsComparer constructorsComparer = new();
        private readonly Type injectAttributeType = typeof(InjectAttribute);
        private readonly Dictionary<Type, object> instances = new();
        private readonly Type iTransientAccessType = typeof(ITransientAccess);

        private readonly Dictionary<object, object> transientObjects = new();
        private readonly HashSet<Type> used = new();
        private ConstructorInfo currentCtor;
        private bool started;
        
        private ConstructorInfo unsupportedCtor;
        private Type unsupportedType;

        // to have ability bypass container to consumers
        public Container()
        {
            Register<IContainer>(this);
        }

        /// <inheritdoc cref="Dispose" />
        public void Dispose()
        {
            if (!started)
                throw new Exception("Container is not started yet!");

            using var instancesEnumerator = instances.GetEnumerator();

            while (instancesEnumerator.MoveNext())
            {
                var instance = instancesEnumerator.Current.Value;

                // do not dispose container itself, it's already disposing
                if (instance is IContainer)
                    continue;

                if (instance is IDisposable disposable)
                    disposable.Dispose();
            }

            transientObjects.Clear();
            instances.Clear();
            configurations.Clear();
            used.Clear();
            started = false;
        }

        public IEnumerable<T> GetAllInstances<T>()
        {
            if (!started)
                throw new InvalidOperationException("Container is not running state, instances not initialized yet.");

            foreach (var instance in instances.Values)
                if (instance is T tInstance)
                    yield return tInstance;
        }

        /// <inheritdoc cref="IsRegistered" />
        public bool IsRegistered(in Type api)
        {
            return configurations.ContainsKey(api);
        }

        /// <inheritdoc cref="Resolve{T}" />
        public bool Resolve<T>(out T value, params object[] arguments)
        {
            if (!started)
                throw new Exception("Container is not started yet!");

            try
            {
                value = (T)Resolve(typeof(T), arguments);
                return true;
            }
            catch
            {
                value = default;
                return false;
            }
        }

        /// <inheritdoc cref="Resolve" />
        public object Resolve(in Type type, params object[] arguments)
        {
            if (instances.TryGetValue(type, out var instance))
                return instance;

            var config = ValidateTypeConfiguration(type);
            var validType = ValidateCycleDependency(type, config);
            var constructorInfos = validType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(constructorInfos, constructorsComparer);

            var len = constructorInfos.Length;
            for (var i = 0; i < len; ++i)
            {
                var constructorInfo = constructorInfos[i];
                currentCtor = constructorInfo;
                var constructorParameterInfos = currentCtor.GetParameters();
                if (!ValidateCtorParametersTypes(constructorParameterInfos, arguments, out unsupportedType, out var injectionMask))
                {
                    unsupportedCtor = currentCtor;
                    continue;
                }

                var constructorParametersCount = constructorParameterInfos.Length;
                var constructorParameters = new object[constructorParameterInfos.Length];
                var argIndex = 0;
                for (var j = 0; j < constructorParametersCount; ++j)
                {
                    constructorParameters[j] = (1L << j & injectionMask) == 0
                        ? arguments[argIndex++]
                        : Resolve(constructorParameterInfos[j].ParameterType);
                }

                return CreateInstance(type, validType, constructorInfo, constructorParameters, config);
            }

            if (unsupportedCtor != null)
                throw new UnsupportedCtorParameterException(unsupportedType, unsupportedCtor);

            throw new ConstructorNotFoundException(type);
        }
        
        /// <inheritdoc cref="Inject" />
        public void Inject(in object target)
        {
            if (!started)
                throw new Exception("Container is not started yet!");

            if (target == null)
                return;

            InjectDependencies(target.GetType(), target);
        }

        /// <inheritdoc cref="Run" />
        public void Run()
        {
            foreach (var (api, config) in configurations)
                if (config.IsSingleton)
                    Resolve(api);

            started = true;
        }

        /// <inheritdoc cref="Register(System.Type,object)" />
        public void Register(in Type api, in object instance)
        {
            var impl = instance.GetType();

            if (!ValidateInheritance(api, impl)) throw new InvalidInheritanceException(api, impl);

            configurations[api] = new ImplementationConfig(impl, true);
            instances[api] = instance;
        }
        
        /// <inheritdoc cref="Register(System.Type,System.Type,bool)" />
        public void Register(in Type api, in Type impl, in bool isSingleton = false)
        {
            if (!ValidateInheritance(api, impl)) throw new InvalidInheritanceException(api, impl);

            configurations[api] = new ImplementationConfig(impl, isSingleton);
        }

        /// <inheritdoc cref="Register(System.Type,bool)" />
        public void Register(in Type impl, in bool isSingleton)
        {
            // register self
            Register(impl, impl, isSingleton);
        }

        /// <inheritdoc cref="Register{T}(in bool)"/>
        public void Register<T>(in bool isSingleton = true)
        {
            Register(typeof(T), isSingleton);
        }

        /// <inheritdoc cref="Register{T}(in T)"/>
        public void Register<T>(in T impl)
        {
            Register(typeof(T), impl);
        }

        /// <inheritdoc cref="Register{TApi,TType}(in bool)"/>
        public void Register<TApi, TType>(in bool isSingleton = true) where TType : TApi
        {
            Register(typeof(TApi), typeof(TType), isSingleton);
        }

        /// <inheritdoc cref="Register{TApi,TType}(in TType)" />
        public void Register<TApi, TType>(in TType impl) where TType : TApi
        {
            Register(typeof(TApi), impl);
        }
        
        /// <inheritdoc cref="RegisterTransientObject" />
        public bool RegisterTransientObject(in object key, in object obj)
        {
            return transientObjects.TryAdd(key, obj);
        }

        /// <inheritdoc cref="UnregisterTransientObject" />
        public bool UnregisterTransientObject(in object key)
        {
            return transientObjects.Remove(key);
        }
        
        /// <inheritdoc cref="UnregisterTransientObject{T}" />
        public bool UnregisterTransientObject<T>(in object key, out T value)
        {
            if (!transientObjects.Remove(key, out var typelessValue))
            {
                value = default;
                return false;
            }
            
            try
            {
                value = (T)typelessValue;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                value = default;
                return false;
            }
        }

        /// <inheritdoc cref="TryGetTransientObject{T}" />
        public bool TryGetTransientObject<T>(in object key, out T value)
        {
            if (!transientObjects.TryGetValue(key, out var typelessValue))
            {
                value = default;
                return false;
            }

            // in this case we got the result but it can be different value
            try
            {
                value = (T)typelessValue;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                value = default;
                return false;
            }
        }

#region Ctors comparer implementation

        private sealed class ConstructorsComparer : IComparer<ConstructorInfo>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Compare(ConstructorInfo x, ConstructorInfo y)
            {
                if (x == null || y == null)
                    return -1;

                var xc = x.GetParameters().Length;
                var yc = y.GetParameters().Length;

                if (xc > yc)
                    return -1;

                return xc < yc ? 1 : 0;
            }
        }

#endregion

#region Private methods implementation

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private object CreateInstance(in Type type, in Type validType, in ConstructorInfo constructorInfo, in object[] constructorParameters, in ImplementationConfig config)
        {
            used.Remove(validType);

            var instance = InjectDependencies(validType, constructorInfo.Invoke(constructorParameters));
            if (instance != null && config is { IsSingleton: true })
                instances[type] = instance;

            return instance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Type ValidateCycleDependency(in Type type, in ImplementationConfig config)
        {
            if (config == null || type.IsPrimitive || type.IsEnum)
                return type;

            if (!used.Add(config.Impl))
                throw new CycleDependencyException(type, currentCtor);

            return config.Impl;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ImplementationConfig ValidateTypeConfiguration(in Type type, in bool throwException = true)
        {
            if (!configurations.TryGetValue(type, out var config) && throwException && type.IsInterface)
                throw new WrongConfigurationException(type);

            return config;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ValidateCtorParametersTypes(in IReadOnlyList<ParameterInfo> parameters, in IReadOnlyList<object> arguments, out Type unsupported, out long injectionMask)
        {
            injectionMask = 0;
            var argIndex = 0;
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                var type = parameter.ParameterType;
                if (instances.ContainsKey(type))
                {
                    injectionMask |= 1L << i;
                    continue;
                }

                argIndex++;
                if (argIndex >= arguments.Count)
                {
                    unsupported = type;
                    return false;
                }
                
                if (type.IsInstanceOfType(arguments[argIndex]))
                    continue;
                    
                unsupported = type;
                return false;
            }
            
            unsupported = null;
            return true;
        }

        private object InjectDependencies(in Type type, in object instance)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            foreach (var fieldInfo in fields)
            {
                if (iTransientAccessType.IsAssignableFrom(fieldInfo.FieldType))
                    InjectTransientAccessField(instance, fieldInfo);
                else if (fieldInfo.GetCustomAttributes(injectAttributeType, false).Length > 0)
                    InjectField(instance, fieldInfo);
            }
            
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            foreach (var propertyInfo in properties)
            {
                if (iTransientAccessType.IsAssignableFrom(propertyInfo.PropertyType))
                    InjectTransientAccessProperty(instance, propertyInfo);
                else if (propertyInfo.GetCustomAttributes(injectAttributeType, false).Length > 0)
                    InjectProperty(instance, propertyInfo);
            }

            return instance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InjectProperty(in object instance, in PropertyInfo propertyInfo)
        {
            var config = ValidateTypeConfiguration(propertyInfo.PropertyType, false);
            if (config == null)
                return;

            if (!propertyInfo.CanWrite || propertyInfo.PropertyType.IsArray ||
                propertyInfo.GetIndexParameters().Length > 0 || propertyInfo.GetValue(instance, null) != null)
                return;

            propertyInfo.SetValue(instance, Resolve(propertyInfo.PropertyType), null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InjectField(in object instance, in FieldInfo fieldInfo)
        {
            var config = ValidateTypeConfiguration(fieldInfo.FieldType, false);
            if (config == null)
                return;

            if (fieldInfo.FieldType.IsArray || fieldInfo.GetValue(instance) != null)
                return;

            fieldInfo.SetValue(instance, Resolve(fieldInfo.FieldType));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InjectTransientAccessProperty(in object instance, in PropertyInfo propertyInfo)
        {
            var value = propertyInfo.GetValue(instance);
            InjectTransiency(ref value);
            propertyInfo.SetValue(instance, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InjectTransientAccessField(in object instance, in FieldInfo fieldInfo)
        {
            var value = fieldInfo.GetValue(instance);
            InjectTransiency(ref value);
            fieldInfo.SetValue(instance, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InjectTransiency(ref object value)
        {
            // set check delegate
            var fieldInfoCheck = value.GetType().GetField(nameof(TransientAccess<object>.Check), BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldInfoCheck == null)
                throw new FieldAccessException($"Can not find field name with name {nameof(TransientAccess<object>.Check)} in {value} but suppose to!");
            fieldInfoCheck.SetValue(value, (TransientCheckDelegate)transientObjects.ContainsKey);

            // set access delegate
            var fieldInfoAccess = value.GetType().GetField(nameof(TransientAccess<object>.Access), BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldInfoAccess == null)
                throw new FieldAccessException($"Can not find field name with name {nameof(TransientAccess<object>.Access)} in {value} but suppose to!");
            fieldInfoAccess.SetValue(value, (TransientAccessDelegate)transientObjects.TryGetValue);
        }

        private static bool ValidateInheritance(in Type api, in Type impl)
        {
            if (api.IsClass) return api.IsAssignableFrom(impl);

            if (api != impl)
                return impl.GetInterface(api.Name) != null;

            return true;
        }

#endregion
    }
}