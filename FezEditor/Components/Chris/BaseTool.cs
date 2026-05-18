using FezEditor.Services;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Chris;

internal abstract class BaseTool
{
    protected IChrisEditor Chris { get; }

    protected StatusService StatusService { get; }

    protected BaseTool(Game game, IChrisEditor chris)
    {
        Chris = chris;
        StatusService = game.GetService<StatusService>();
    }

    public void Update()
    {
        TestConditions();
        if (IsToolAllowed(Chris.CurrentTool))
        {
            Act();
        }
    }

    public virtual void DrawOverlay()
    {
    }

    protected virtual void TestConditions()
    {
    }

    protected virtual void Act()
    {
    }

    protected abstract bool IsToolAllowed(ChrisTool tool);

    protected enum LmbState
    {
        Idle,
        Pressed,
        Dragging
    }
}