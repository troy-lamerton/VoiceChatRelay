using System;
using System.Diagnostics;
using System.Timers;
using NLog.Common;

/// <summary>
/// Action that has a cap on how often it can execute.
/// If it is invoking during the cooldown period,
/// when the cooldown period ends only the most recent call is executed.
/// </summary>
public class ThrottledAction<T> : IDisposable where T : class {
    private readonly Action<T> action;
    private readonly Timer timer;
    private bool canExecuteImmediately = true;
    
    public ThrottledAction(Action<T> action, int cooldownPeriod) {
        this.action = action;
        timer = new Timer {Interval = cooldownPeriod};
        timer.Elapsed += OnInterval;
        timer.Start();
    }

    private T nextValue;

    public void Invoke(T value) {
        if (canExecuteImmediately) {
            canExecuteImmediately = false;
            action.Invoke(value);
            return;
        }
        nextValue = value;
    }

    private void OnInterval(object source, ElapsedEventArgs e) {
        if (nextValue == null) {
            canExecuteImmediately = true;
            return;
        }
        action.Invoke(nextValue);
        nextValue = null;
    }

    public void Dispose() {
        timer?.Dispose();
    }
}
