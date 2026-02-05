using System;
using System.Collections.Generic;

public static class GameEventBus
{
    // Dictionary mapping event types to delegate handlers
    private static readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public static void Subscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (!_handlers.ContainsKey(type))
        {
            _handlers[type] = new List<Delegate>();
        }
        _handlers[type].Add(handler);
    }

    public static void Unsubscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (_handlers.ContainsKey(type))
        {
            _handlers[type].Remove(handler);
        }
    }

    public static void Publish<T>(T eventMessage)
    {
        var type = typeof(T);
        if (_handlers.TryGetValue(type, out var handlers))
        {
            // Iterate backwards to allow Unsubscribing inside handler safely
            for (int i = handlers.Count - 1; i >= 0; i--)
            {
                var action = handlers[i] as Action<T>;
                action?.Invoke(eventMessage);
            }
        }
    }
}

// === Common Event Definitions (Example) ===
public struct UnitDamagedEvent
{
    public BattleUnit Target;
    public int Amount;
    public bool IsCrit;
    
    public UnitDamagedEvent(BattleUnit target, int amount, bool isCrit)
    {
        Target = target;
        Amount = amount;
        IsCrit = isCrit;
    }
}
