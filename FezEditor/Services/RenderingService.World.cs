using FezEditor.Structure;
using Microsoft.Xna.Framework;

namespace FezEditor.Services;

public partial class RenderingService
{
    private class WorldData
    {
        public required Rid Root;
        public Rid Camera;
        public Vector3 AmbientLight = Vector3.One;
        public Vector3 DiffuseLight = Vector3.One;
        public FogType FogType = FogType.None;
        public Color FogColor = Color.White;
        public float FogDensity;
    }

    private readonly Dictionary<Rid, WorldData> _worlds = new();

    public Rid WorldCreate()
    {
        var rootRid = AllocateRid(typeof(InstanceData));
        _instances[rootRid] = new InstanceData();
        var worldRid = AllocateRid(typeof(WorldData));
        _worlds[worldRid] = new WorldData { Root = rootRid };
        Logger.Debug("World created {0} (root instance {1})", worldRid, rootRid);
        return worldRid;
    }

    public Rid WorldGetRoot(Rid world)
    {
        return GetResource(_worlds, world).Root;
    }

    public Rid WorldGetCamera(Rid world)
    {
        return GetResource(_worlds, world).Camera;
    }

    public Rid WorldGetRenderTarget(Rid world)
    {
        return _renderTargets.FirstOrDefault(kv => kv.Value.World == world).Key;
    }

    public void WorldSetCamera(Rid world, Rid camera)
    {
        GetResource(_worlds, world).Camera = camera;
    }

    public void WorldSetAmbientLight(Rid world, Vector3 color)
    {
        GetResource(_worlds, world).AmbientLight = color;
    }

    public void WorldSetDiffuseLight(Rid world, Vector3 color)
    {
        GetResource(_worlds, world).DiffuseLight = color;
    }

    public void WorldSetFogType(Rid world, FogType type)
    {
        GetResource(_worlds, world).FogType = type;
    }

    public void WorldSetFogColor(Rid world, Color color)
    {
        GetResource(_worlds, world).FogColor = color;
    }

    public void WorldSetFogDensity(Rid world, float density)
    {
        GetResource(_worlds, world).FogDensity = density;
    }

    public bool WorldHasCamera(Rid world)
    {
        return TryGetResource(_worlds, world, out var wd) && (wd?.Camera.IsValid ?? false);
    }
}