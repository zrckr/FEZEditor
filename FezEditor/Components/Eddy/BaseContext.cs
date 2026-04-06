using FezEditor.Services;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal abstract class BaseContext : IDisposable
{
    protected static readonly Color HoverColor = Color.Blue with { A = 85 }; // 33%

    protected static readonly Color SelectionColor = Color.Red with { A = 85 }; // 33%

    protected const int InvalidId = -1;

    protected Level Level { get; }

    protected IEddyEditor Eddy { get; }

    protected ResourceService ResourceService { get; }

    protected StatusService StatusService { get; }

    private bool _wasActive;

    protected BaseContext(Game game, Level level, IEddyEditor eddy)
    {
        Level = level;
        Eddy = eddy;
        ResourceService = game.GetService<ResourceService>();
        StatusService = game.GetService<StatusService>();
    }

    public virtual void Revisualize(bool partial = false)
    {
    }

    public virtual void DrawProperties()
    {
    }

    public void Update()
    {
        TestConditions();

        var isActive = IsContextAllowed(Eddy.Context);
        if (isActive)
        {
            if (!_wasActive)
            {
                Begin();
            }

            Act();
        }
        else if (_wasActive)
        {
            End();
        }

        _wasActive = isActive;
    }

    protected virtual void TestConditions()
    {
    }

    protected virtual void Begin()
    {
    }

    protected virtual void Act()
    {
    }

    protected virtual void End()
    {
    }

    protected abstract bool IsContextAllowed(EddyContext context);

    public virtual void Dispose()
    {
    }
}