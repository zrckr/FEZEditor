namespace FezEditor.Structure;

public enum MultiMeshDataType
{
    Vector4,
    Matrix
}

public static class MultiMeshDataTypeExtensions
{
    public static int GetStride(this MultiMeshDataType type)
    {
        return type switch
        {
            MultiMeshDataType.Vector4 => 1,
            MultiMeshDataType.Matrix => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}