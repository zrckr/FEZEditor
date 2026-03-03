using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class SceneLighting : IDisposable
{
    public Color Ambient
    {
        get => _ambient;
        set
        {
            if (_ambient != value)
            {
                _ambient = value;
                _rendering.WorldSetAmbientLight(_world, _ambient.ToVector3());
            }
        }
    }

    public Color Diffuse
    {
        get => _diffuse;
        set
        {
            if (_diffuse != value)
            {
                _diffuse = value;
                _rendering.WorldSetDiffuseLight(_world, _diffuse.ToVector3());
            }
        }
    }
    
    private Color _ambient = Color.Gray;
    
    private Color _diffuse = Color.White;
    
    private readonly RenderingService _rendering;

    private readonly Rid _world;

    internal SceneLighting(Game game, Rid worldRid)
    {
        _rendering = game.GetService<RenderingService>();
        _world = worldRid;
        _rendering.WorldSetAmbientLight(_world, _ambient.ToVector3());
        _rendering.WorldSetDiffuseLight(_world, _diffuse.ToVector3());
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}