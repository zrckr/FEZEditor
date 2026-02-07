using FezEditor.Services;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

[UsedImplicitly]
public class StatusBar : DrawableGameComponent
{
    private readonly IStateService _stateService;

    public StatusBar(Game game, IStateService stateService) : base(game)
    {
        _stateService = stateService;
    }

    public void Draw()
    {
        // TODO: implement this
    }
}
