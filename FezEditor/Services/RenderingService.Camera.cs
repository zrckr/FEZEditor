using FezEditor.Structure;
using Microsoft.Xna.Framework;

namespace FezEditor.Services;

public partial class RenderingService
{
    private class CameraData
    {
        public Matrix View = Matrix.Identity;
        public Matrix Projection = Matrix.Identity;
    }

    private readonly Dictionary<Rid, CameraData> _cameras = new();

    public Rid CameraCreate()
    {
        var rid = AllocateRid(typeof(CameraData));
        _cameras[rid] = new CameraData();
        Logger.Debug("Camera created {0}", rid);
        return rid;
    }

    public void CameraSetView(Rid camera, Matrix view)
    {
        GetResource(_cameras, camera).View = view;
    }

    public void CameraSetProjection(Rid camera, Matrix projection)
    {
        GetResource(_cameras, camera).Projection = projection;
    }

    public Matrix CameraGetView(Rid camera)
    {
        return GetResource(_cameras, camera).View;
    }

    public Matrix CameraGetProjection(Rid camera)
    {
        return GetResource(_cameras, camera).Projection;
    }
}