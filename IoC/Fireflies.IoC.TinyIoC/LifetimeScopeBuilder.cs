using Fireflies.IoC.Abstractions;

namespace Fireflies.IoC.TinyIoC;

public class LifetimeScopeBuilder : ILifetimeScopeBuilder {
    private readonly TinyIoCContainer _containerBuilder;

    public LifetimeScopeBuilder(TinyIoCContainer containerBuilder) {
        _containerBuilder = containerBuilder;
    }

    public void RegisterType<T>() where T : class {
        _containerBuilder.Register<T>();
    }

    public void RegisterType(Type type) {
        _containerBuilder.Register(type);
    }

    public void RegisterInstance<T>(T instance) where T : class {
        _containerBuilder.Register(instance);
    }

    public void RegisterTypeAsSingleInstance<T>() where T : class {
        _containerBuilder.Register<T>().AsSingleton();
    }
}