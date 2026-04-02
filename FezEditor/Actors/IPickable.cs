using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public interface IPickable : IComponent
{
    IEnumerable<BoundingBox> GetBounds();

    PickHit? Pick(Ray ray);
}

public record struct PickHit(float Distance, int Index);

public record struct RaycastHit(Actor Actor, float Distance, int Index);