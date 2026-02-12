namespace FezEditor.Structure;

public readonly record struct Dirty<T>(T Value, bool IsDirty = false) where T : notnull
{
    public Dirty<T> Marked() => IsDirty ? this : this with { IsDirty = true };
    
    public Dirty<T> Clean() => this with { IsDirty = false };
}