namespace Fireflies.IoC.Core;

public interface ILifetimeScopeBuilder {
    void RegisterType<T>() where T : notnull;
    void RegisterType(Type type);
    void RegisterInstance<T>(T instance) where T : class;
}