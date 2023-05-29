using System;

namespace OpenSpace.Engine.Graphics;

[Flags]
public enum StorageAllocationFlags
{
    None = 0,
    Dynamic = 1,
    Client = 2
}