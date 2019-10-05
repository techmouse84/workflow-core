using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ObjectPool;
using System;

namespace WorkflowCore.Services
{
    internal class InjectedObjectPoolPolicy<T> : IPooledObjectPolicy<T>
    {
        private readonly IServiceProvider _provider;

        public InjectedObjectPoolPolicy(IServiceProvider provider)
        {
            _provider = provider;
        }

        public T Create()
        {
            return _provider.GetService<T>();
        }

        public bool Return(T obj)
        {
            return true;
        }
    }
}
