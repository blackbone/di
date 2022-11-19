using System;
using System.Collections.Generic;
using System.Reflection;

namespace DependencyInjection
{
    internal sealed class Container : IContainer
    {
        private readonly Dictionary<Type, ImplementationConfig> configurations = new();
        private readonly ConstructorsComparer constructorsComparer = new();
        private readonly Type injectAttributeType = typeof(InjectAttribute);
        private readonly Dictionary<Type, object> instances = new();
        private readonly HashSet<Type> used = new();
        private ConstructorInfo currentCtor;
        private FieldInfo currentField;
        private PropertyInfo currentProperty;
        private bool started;
        private ConstructorInfo unsupportedCtor;

        private Type unsupportedType;

        // to have ability bypass container to consumers
        public Container() => Register<IContainer>(this);

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
        }

        public IEnumerable<T> GetAllInstances<T>()
        {
            if (!started)
                throw new InvalidOperationException("Container is not running state, instances not initialized yet.");

            foreach (var instance in instances.Values)
                if (instance is T tInstance)
                    yield return tInstance;
        }

        public void Register<T>(bool isSingleton = true)
        {
            Register(typeof(T), isSingleton);
        }

        public void Register<T>(T impl)
        {
            Register(typeof(T), impl);
        }

        public void Register<TApi, TType>(bool isSingleton = true) where TType : TApi
        {
            Register(typeof(TApi), typeof(TType), isSingleton);
        }

        /// <inheritdoc cref="IsRegistered" />
        public bool IsRegistered(Type api)
        {
            return configurations.ContainsKey(api);
        }

        /// <inheritdoc cref="Resolve{T}" />
        public T Resolve<T>()
        {
            if (!started)
                throw new Exception("Container is not started yet!");

            return (T)Resolve(typeof(T));
        }

        /// <inheritdoc cref="Inject" />
        public void Inject(object target)
        {
            if (!started)
                throw new Exception("Container is not started yet!");

            if (target == null)
                return;

            InjectDependencies(target.GetType(), target);
        }

        public void Run()
        {
            foreach (var (api, config) in configurations)
                if (config.IsSingleton)
                    Resolve(api);

            started = true;
        }

        /// <inheritdoc cref="Resolve"/>
        public object Resolve(Type type)
        {
            if (instances.TryGetValue(type, out var instance))
                return instance;

            var config = ValidateTypeConfiguration(type);
            var validType = ValidateCycleDependency(type, config);
            var constructorInfos =
                validType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(constructorInfos, constructorsComparer);

            var len = constructorInfos.Length;
            for (var i = 0; i < len; ++i)
            {
                var constructorInfo = constructorInfos[i];
                currentCtor = constructorInfo;
                var constructorParameterInfos = currentCtor.GetParameters();
                if (!ValidateCtorParametersTypes(constructorParameterInfos, out unsupportedType))
                {
                    unsupportedCtor = currentCtor;
                    continue;
                }

                var constructorParametersCount = constructorParameterInfos.Length;
                var resolvedParameterValues = new object[constructorParameterInfos.Length];
                for (var j = 0; j < constructorParametersCount; ++j)
                    resolvedParameterValues[j] = Resolve(constructorParameterInfos[j].ParameterType);

                return CreateInstance(type, validType, constructorInfo, resolvedParameterValues, config);
            }

            if (unsupportedCtor != null)
                throw new UnsupportedCtorParameterException(unsupportedType, unsupportedCtor);

            return null;
        }

        /// <inheritdoc cref="Register(System.Type,System.Type,bool)" />
        public void Register(Type api, Type impl, bool isSingleton = false)
        {
            if (!ValidateInheritance(api, impl)) throw new InvalidInheritanceException(api, impl);

            configurations[api] = new ImplementationConfig(impl, isSingleton);
        }
        
        /// <inheritdoc cref="Register(System.Type,bool)" />
        public void Register(Type impl, bool isSingleton)
        {
            // register self
            Register(impl, impl, isSingleton);

            // TODO [Dmitrii Osipov] figure out if needed to register interfaces
            // register apis
            // foreach (var api in impl.GetInterfaces(false))
            //     Register(api, impl, isSingleton);
        }

        public void Register<TApi, TType>(TType impl) where TType : TApi
        {
            Register(typeof(TApi), impl);
        }

        /// <inheritdoc cref="Register(System.Type,object)" />
        private void Register(Type api, object instance)
        {
            var impl = instance.GetType();

            if (!ValidateInheritance(api, impl)) throw new InvalidInheritanceException(api, impl);

            configurations[api] = new ImplementationConfig(impl, true);
            instances[api] = instance;
        }


#region Ctors comparer implementation

        private sealed class ConstructorsComparer : IComparer<ConstructorInfo>
        {
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

        private object CreateInstance(Type type, Type validType, ConstructorInfo constructorInfo,
            object[] constructorParameters, ImplementationConfig config)
        {
            used.Remove(validType);

            var instance = InjectDependencies(validType, constructorInfo.Invoke(constructorParameters));
            if (config != null && config.IsSingleton)
                instances[type] = instance;

            return instance;
        }

        private Type ValidateCycleDependency(Type type, ImplementationConfig config)
        {
            if (config == null || type.IsPrimitive || type.IsEnum)
                return type;

            if (used.Contains(config.Impl))
                throw new CycleDependencyException(type, currentCtor);
            used.Add(config.Impl);

            return config.Impl;
        }

        private ImplementationConfig ValidateTypeConfiguration(Type type, bool throwException = true)
        {
            if (!configurations.TryGetValue(type, out var config) && throwException && type.IsInterface)
                throw new WrongConfigurationException(type);

            return config;
        }

        private bool ValidateCtorParametersTypes(IEnumerable<ParameterInfo> parameters, out Type unsupported)
        {
            foreach (var parameter in parameters)
            {
                var type = parameter.ParameterType;

                if (!type.IsPrimitive && (!type.IsInterface || configurations.ContainsKey(type)))
                    continue;

                unsupported = type;
                return false;
            }

            unsupported = null;
            return true;
        }

        private object InjectDependencies(IReflect type, object instance)
        {
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var member in members)
            {
                if (member.GetCustomAttributes(injectAttributeType, false).Length > 0)
                    continue;

                switch (member.MemberType)
                {
                    case MemberTypes.Property:
                        currentProperty = (PropertyInfo)member;
                        InjectProperty(instance, currentProperty);
                        currentProperty = null;
                        break;
                    case MemberTypes.Field:
                        currentField = (FieldInfo)member;
                        InjectField(instance, currentField);
                        currentField = null;
                        break;
                }
            }

            return instance;
        }

        private void InjectProperty(object instance, PropertyInfo propertyInfo)
        {
            var config = ValidateTypeConfiguration(propertyInfo.PropertyType, false);
            if (config == null)
                return;

            if (!propertyInfo.CanWrite || propertyInfo.PropertyType.IsArray ||
                propertyInfo.GetIndexParameters().Length > 0 || propertyInfo.GetValue(instance, null) != null)
                return;

            propertyInfo.SetValue(instance, Resolve(propertyInfo.PropertyType), null);
        }

        private void InjectField(object instance, FieldInfo fieldInfo)
        {
            var config = ValidateTypeConfiguration(fieldInfo.FieldType, false);
            if (config == null)
                return;

            if (fieldInfo.FieldType.IsArray || fieldInfo.GetValue(instance) != null)
                return;

            fieldInfo.SetValue(instance, Resolve(fieldInfo.FieldType));
        }

        private static bool ValidateInheritance(Type api, Type impl)
        {
            if (api.IsClass) return api.IsAssignableFrom(impl);

            if (api != impl)
                return impl.GetInterface(api.Name) != null;

            return true;
        }

#endregion
    }
}