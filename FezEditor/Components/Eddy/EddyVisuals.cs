namespace FezEditor.Components.Eddy;

[Flags]
public enum EddyVisuals
{
    PickableBounds = 1,
    CollisionMap = 2,
    Triles = 4,
    ArtObjects = 8,
    BackgroundPlanes = 16,
    NonPlayableCharacters = 32,
    Volumes = 64,
    Paths = 128,
    Liquid = 256,
    Sky = 512,
    Gomez = 1024,
    EmptyTriles = 2048,
    DisplacedTriles = 4096,
    Default = Triles | ArtObjects | BackgroundPlanes | NonPlayableCharacters |
              Volumes | Paths | Liquid | Sky | Gomez | EmptyTriles | DisplacedTriles
}