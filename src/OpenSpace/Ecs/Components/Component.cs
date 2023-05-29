using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OpenSpace.Ecs.Components;

public abstract class Component
{
    public Entity? Entity;

    public event Action<Component> ComponentChanged;

    protected bool SetValue<T>(ref T field, T value, [CallerMemberName]string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        NotifyPropertyChanged(propertyName);
        return true;
    }

    private void NotifyPropertyChanged(string? propertyName)
    {
        var componentChanged = ComponentChanged;
        componentChanged?.Invoke(this);
    }
}