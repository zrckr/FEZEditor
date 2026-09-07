using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

public abstract class EddySystem : IDisposable
{
    public Game Game { protected get; set; } = null!;

    public EddyEditor Eddy { protected get; set; } = null!;

    public Level Level { protected get; set; } = null!;

    public ResourceService Resources { protected get; set; } = null!;

    public RenderingService Rendering { protected get; set; } = null!;

    public InputService Input { protected get; set; } = null!;

    public AppStorageService Storage { protected get; set; } = null!;

    public InputHints Hints { protected get; set; } = null!;

    public IContentManager Content { protected get; set; } = null!;

    public virtual void Initialize()
    {
    }

    public virtual void Visualize(InstanceId instanceId)
    {
    }

    public virtual void Inspect(params HashSet<InstanceId> instanceIds)
    {
    }

    public virtual void Update()
    {
    }

    public virtual void Draw()
    {
    }

    public virtual bool IsToolEnabled(ToolState tool)
    {
        return false;
    }

    public virtual void Dispose()
    {
    }
}