using FezEditor.Actors;
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

    private readonly Actor _subRoot;

    private bool _wasActive;

    protected BaseContext(Game game, Level level, IEddyEditor eddy)
    {
        Level = level;
        Eddy = eddy;
        ResourceService = game.GetService<ResourceService>();
        StatusService = game.GetService<StatusService>();
        _subRoot = eddy.Scene.CreateActor();
        _subRoot.Name = GetType().Name;
    }

    protected Actor CreateSubActor()
    {
        return Eddy.Scene.CreateActor(_subRoot);
    }

    public virtual void PartialRevisualize(EddyContext context)
    {
    }

    public virtual void FullVisualize()
    {
    }

    public virtual void DrawProperties()
    {
    }

    public virtual void DrawOverlay()
    {
    }

    public void Update()
    {
        TestConditions();

        var activeContext = Eddy.SelectedContext != EddyContext.Default
            ? Eddy.SelectedContext
            : Eddy.HoveredContext;

        var isActive = IsContextAllowed(activeContext);
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
        Eddy.Scene.DestroyActor(_subRoot);
    }
}