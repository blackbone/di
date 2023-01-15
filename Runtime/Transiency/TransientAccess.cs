using System;
using UnityEngine;

namespace Transiency
{
    internal delegate bool TransientAccessDelegate(object key, out object value);
    internal delegate bool TransientCheckDelegate(object key);

    internal interface ITransientAccess
    {
    }

    public readonly struct TransientAccess<T> : ITransientAccess
    {
        private readonly object _key; 
        internal readonly TransientAccessDelegate Access;
        internal readonly TransientCheckDelegate Check;

        public bool HasValue
        {
            get
            {
                if (Access == null)
                    throw new Exception("Transient access not initialized. Possibly not injected.");

                return Check(_key);
            }
        }
        
        public T Value
        {
            get
            {
                if (Access == null)
                    throw new Exception("Transient access not initialized. Possibly not injected.");

                if (!Access(_key, out var value))
                    return default;

                try
                {
                    return (T)value;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return default;
                }
            }
        }

        public TransientAccess(object key)
        {
            _key = key;
            Access = null;
            Check = null;
        }
    }
}