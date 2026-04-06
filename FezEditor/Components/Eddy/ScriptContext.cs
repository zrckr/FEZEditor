using FEZRepacker.Core.Definitions.Game.Level;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class ScriptContext : BaseContext
{
    public ScriptContext(Game game, Level level, IEddyEditor eddy) : base(game, level, eddy)
    {
    }

    protected override bool IsContextAllowed(EddyContext context)
    {
        return context == EddyContext.Script;
    }
}
