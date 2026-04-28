namespace FezEditor.Components.Eddy;

[Flags]
public enum EddyVisuals
{
    #region Instances

    Triles = 1,
    EmptyTriles = 2,
    DisplacedTriles = 4,
    ArtObjects = 8,
    BackgroundPlanes = 16,
    NonPlayableCharacters = 32,
    Gomez = 64,
    Liquid = 128,
    Sky = 256,
    Rain = 512,

    #endregion

    #region Overlays

    Volumes = 1024,
    Paths = 2048,
    LevelBounds = 4096,
    CollisionMap = 8192,
    PickableBounds = 16384,

    #endregion

    #region Presets

    Default = Triles | EmptyTriles | DisplacedTriles | ArtObjects | BackgroundPlanes |
              NonPlayableCharacters | Gomez | Liquid | Sky | Rain |
              Volumes | Paths | LevelBounds,
    Preview = Triles | ArtObjects | BackgroundPlanes | Liquid | Sky

    #endregion
}