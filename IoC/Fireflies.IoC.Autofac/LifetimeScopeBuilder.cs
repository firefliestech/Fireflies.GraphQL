using Autofac;
using Fireflies.IoC.Abstractions;

namespace Fireflies.IoC.Autofac;

public class LifetimeScopeBuilder : ILifetimeScopeBuilder {
    private readonly ContainerBuilder _containerBuilder;

    public LifetimeScopeBuilder(ContainerBuilder containerBuilder) {
        _containerBuilder = containerBuilder;
    }

    public void RegisterType<T>() where T : class {
        _containerBuilder.RegisterType<T>();
    }

    public void RegisterType(Type type) {
        _containerBuilder.RegisterType(type);
    }

    public void RegisterInstance<T>(T instance) where T : class {
        _containerBuilder.RegisterInstance(instance);
    }

    public void RegisterTypeAsSingleInstance<T>() where T : class {
        _containerBuilder.RegisterType<T>().SingleInstance();
    }
}