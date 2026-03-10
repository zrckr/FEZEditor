using System.Reflection;
using Microsoft.Xna.Framework;
using Serilog;

namespace FezEditor.Tools;

public static class GameExtensions
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", nameof(FezEditor));

    private static readonly Lock Lock = new();

    private static readonly List<object> Services = new();

    #region Service Creation with DI

    public static T CreateService<T>(this Game game) where T : class
    {
        var service = (T)CreateInstance(game, typeof(T));
        game.AddService(service);
        return service;
    }

    #endregion

    #region Instance Creation

    private static object CreateInstance(Game game, Type type)
    {
        var constructors = type.GetConstructors();
        foreach (var ctor in constructors.OrderByDescending(c => c.GetParameters().Length))
        {
            if (TryResolveConstructor(game, ctor, out var args))
            {
                return ctor.Invoke(args);
            }
        }

        throw new InvalidOperationException(
            $"Cannot create {type.Name}: no constructor with resolvable dependencies");
    }

    private static bool TryResolveConstructor(Game game, ConstructorInfo ctor, out object?[] args)
    {
        var parameters = ctor.GetParameters();
        args = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            if (TryResolveParameter(game, paramType, out var value))
            {
                args[i] = value;
            }
            else if (parameters[i].HasDefaultValue)
            {
                args[i] = parameters[i].DefaultValue;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryResolveParameter(Game game, Type paramType, out object? value)
    {
        // Special case: inject Game itself
        if (paramType == typeof(Game) || paramType.IsInstanceOfType(game))
        {
            value = game;
            return true;
        }

        // Try to get existing service
        var service = game.Services.GetService(paramType);
        if (service != null)
        {
            value = service;
            return true;
        }

        value = null;
        return false;
    }

    #endregion

    #region Service Management

    private static void AddService<T>(this Game game, T service)
    {
        if (service is IUpdateable || service is IDrawable || service is IGameComponent || service is IComparable)
        {
            throw new ArgumentException("Only a service with or without IDisposable interface can be added!");
        }

        game.Services.AddService(typeof(T), service);
        Services.Add(service!);
        Logger.Debug("Added {0}", service!.GetType().Name);
    }

    public static T GetService<T>(this Game game) where T : class
    {
        var @object = game.Services.GetService(typeof(T));
        if (@object is not T service)
        {
            throw new InvalidCastException($"Could not find or cast service {typeof(T).FullName}");
        }

        return service;
    }

    public static void RemoveServices(this Game game)
    {
        foreach (var service in Services)
        {
            game.Services.RemoveService(service.GetType());
            Logger.Debug("Removed {0}", service.GetType().Name);
            if (service is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        Services.Clear();
    }

    #endregion

    #region Component Management

    public static void AddComponent<T>(this Game game, T component) where T : IGameComponent
    {
        lock (Lock)
        {
            game.Components.Add(component);
        }
    }

    public static T GetComponent<T>(this Game game) where T : IGameComponent
    {
        lock (Lock)
        {
            return game.Components.OfType<T>().First();
        }
    }

    public static void RemoveComponent<T>(this Game game, T component) where T : IGameComponent
    {
        if (component is IDisposable disposable)
        {
            disposable.Dispose();
        }

        lock (Lock)
        {
            game.Components.Remove(component);
        }
    }

    #endregion
}