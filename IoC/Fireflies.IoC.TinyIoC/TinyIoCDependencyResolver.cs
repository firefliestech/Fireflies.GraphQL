using Fireflies.IoC.Abstractions;

namespace Fireflies.IoC.TinyIoC;

public class TinyIoCDependencyResolver : IDependencyResolver {
    private readonly TinyIoCContainer _rootContainer;

    public TinyIoCDependencyResolver() {
        _rootContainer = new TinyIoCContainer();
    }

    public TinyIoCDependencyResolver(TinyIoCContainer rootContainer) {
        _rootContainer = rootContainer;
    }

    public IDependencyResolver BeginLifetimeScope(Action<ILifetimeScopeBuilder> builder) {
        return new TinyIoCDependencyResolver(_rootContainer.GetChildContainer());
    }

    public T Resolve<T>() where T : class {
        return _rootContainer.Resolve<T>();
    }

    public object Resolve(Type type) {
        return _rootContainer.Resolve(type);
    }

    public bool TryResolve<T>(out T? instance) where T : class {
        return _rootContainer.TryResolve<T>(out instance);
    }

    public void Dispose() {
        _rootContainer.Dispose();
    }
}