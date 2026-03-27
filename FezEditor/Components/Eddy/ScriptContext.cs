using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class ScriptContext : EddyContext
{
    public override bool Pick(Ray ray)
    {
        return false;
    }

    public override void Update()
    {
    }

    public override void Revisualize(bool partial = false)
    {
    }

    public override void Dispose()
    {
    }
}
