using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Structure;

[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexPositionNormalColor : IVertexType
{
    private static readonly VertexDeclaration _vertexDeclaration;
    
    public VertexDeclaration VertexDeclaration => _vertexDeclaration;
    
    public Vector3 Position;
    
    public Vector3 Normal;
    
    public Color Color;

    static VertexPositionNormalColor()
    {
        _vertexDeclaration = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0));
    }
    
    public VertexPositionNormalColor(Vector3 position, Vector3 normal, Color color)
    {
        Position = position;
        Normal = normal;
        Color = color;
    }
    
    public override string ToString()
    {
        return $"{{Position:{Position} Normal:{Normal} Color:{Color}}}";
    }

    public bool Equals(VertexPositionNormalColor other)
    {
        if (other.Position.Equals(Position) && other.Normal.Equals(Normal))
        {
            return other.Color.Equals(Color);
        }

        return false;
    }
}