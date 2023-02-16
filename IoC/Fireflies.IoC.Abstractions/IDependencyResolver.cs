namespace Fireflies.IoC.Abstractions;

public interface IDependencyResolver : IDisposable {
    IDependencyResolver BeginLifetimeScope(Action<ILifetimeScopeBuilder> builder);
    T Resolve<T>() where T : class;
    object Resolve(Type type);
    bool TryResolve<T>(out T? instance) where T : class;
}