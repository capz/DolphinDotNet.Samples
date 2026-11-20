using System;
using System.Collections.Generic;
using Dolphin.Graphics;

// The photograph supplies the shell's color; this geometry supplies its depth.
// Black additive drawing leaves the plate unchanged while writing the shell at
// Z=0. Cap surfaces above it remain visible and submerged stems disappear.
internal sealed class ControllerOcclusion : IDisposable
{
    private readonly Mesh shell;
    private readonly Transform identity = new Transform();
    private readonly Material material = new Material
    {
        Blend = BlendMode.Additive,
        Lighting = LightingMode.Unlit,
        Cull = CullMode.None,
        DepthTest = true,
        DepthWrite = true
    };

    internal int NativeBytes => shell.NativeBytes;
    internal int TriangleCount { get; }

    internal ControllerOcclusion(GraphicsDevice device)
    {
        if (device == null) throw new ArgumentNullException(nameof(device));
        var contours = new List<Vector2[]> { BodySilhouette() };
        var apertures = ControllerControls.GetApertures();
        // Rear triggers have no opening through the front of the shell. Their
        // portion outside the silhouette remains visible without an aperture.
        for (int i = 0; i < 8; i++) contours.Add(apertures[i]);

        var edges = new List<Edge>();
        var heights = new List<float>();
        foreach (var contour in contours)
        {
            for (int i = 0; i < contour.Length; i++)
            {
                var a = contour[i];
                var b = contour[(i + 1) % contour.Length];
                heights.Add(a.Y);
                if (Math.Abs(a.Y - b.Y) > .0001f) edges.Add(new Edge(a, b));
            }
        }
        // Primitive comparisons keep the startup path within the published
        // AOT runtime; Single.CompareTo/IComparer<float> are unnecessary here.
        for (int i = 1; i < heights.Count; i++)
        {
            float value = heights[i];
            int at = i;
            while (at > 0 && heights[at - 1] > value)
            {
                heights[at] = heights[at - 1];
                at--;
            }
            heights[at] = value;
        }
        var vertices = new List<VertexPositionColor>();
        var indices = new List<ushort>();
        var crossings = new List<Crossing>();
        for (int band = 1; band < heights.Count; band++)
        {
            float top = heights[band - 1], bottom = heights[band];
            if (bottom - top < .0001f) continue;
            float middle = (top + bottom) * .5f;
            crossings.Clear();
            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (middle > edge.Top && middle < edge.Bottom)
                    crossings.Add(new Crossing(edge.At(middle), i));
            }
            for (int i = 1; i < crossings.Count; i++)
            {
                var value = crossings[i];
                int at = i;
                while (at > 0 && crossings[at - 1].X > value.X)
                {
                    crossings[at] = crossings[at - 1];
                    at--;
                }
                crossings[at] = value;
            }
            if ((crossings.Count & 1) != 0)
                throw new InvalidOperationException("Controller shell mask has an open contour.");
            // Every vertex height splits a band, so no edges cross within a
            // trapezoid. Even/odd filling cuts exact polygonal holes without a
            // texture, stencil dependency, or per-frame geometry update.
            for (int i = 0; i < crossings.Count; i += 2)
            {
                var left = edges[crossings[i].EdgeIndex];
                var right = edges[crossings[i + 1].EdgeIndex];
                if (crossings[i + 1].X - crossings[i].X < .0001f) continue;
                if (vertices.Count + 4 > ushort.MaxValue)
                    throw new InvalidOperationException("Controller shell mask is too complex.");
                ushort start = (ushort)vertices.Count;
                vertices.Add(Vertex(left.At(top), top));
                vertices.Add(Vertex(right.At(top), top));
                vertices.Add(Vertex(right.At(bottom), bottom));
                vertices.Add(Vertex(left.At(bottom), bottom));
                indices.Add(start); indices.Add((ushort)(start + 1)); indices.Add((ushort)(start + 2));
                indices.Add(start); indices.Add((ushort)(start + 2)); indices.Add((ushort)(start + 3));
            }
        }
        TriangleCount = indices.Count / 3;
        shell = Mesh.Create(vertices.ToArray(), indices.ToArray());
    }

    internal void Draw(Renderer renderer) => renderer.DrawMesh(shell, material, identity);
    public void Dispose() => shell.Dispose();

    private static VertexPositionColor Vertex(float x, float y) =>
        new VertexPositionColor(new Vector3(x - 320, 240 - y, 0), new Color32(0, 0, 0, 255));

    private readonly struct Edge
    {
        private readonly float x, y, slope;
        internal readonly float Top, Bottom;
        internal Edge(Vector2 a, Vector2 b)
        {
            x = a.X; y = a.Y; slope = (b.X - a.X) / (b.Y - a.Y);
            Top = Math.Min(a.Y, b.Y); Bottom = Math.Max(a.Y, b.Y);
        }
        internal float At(float atY) => x + (atY - y) * slope;
    }

    private readonly struct Crossing
    {
        internal readonly float X;
        internal readonly int EdgeIndex;
        internal Crossing(float x, int edgeIndex) { X = x; EdgeIndex = edgeIndex; }
    }

    private static Vector2[] BodySilhouette() => new[]
    {
        // Native 640x480 coordinates measured from the approved plate. The
        // central arch is concave; filling a convex hull would hide C-stick
        // and trigger travel in empty space behind the controller.
        new Vector2(320, 67.5f), new Vector2(354, 69), new Vector2(386, 74),
        new Vector2(416, 82), new Vector2(440, 92), new Vector2(460, 101),
        new Vector2(478, 112), new Vector2(493, 130), new Vector2(504, 151),
        new Vector2(510, 177), new Vector2(514, 207), new Vector2(517, 238),
        new Vector2(519, 272), new Vector2(517, 306), new Vector2(513, 330),
        new Vector2(506, 347), new Vector2(497, 351), new Vector2(488, 348),
        new Vector2(479, 337), new Vector2(470, 317), new Vector2(461, 291),
        new Vector2(451, 267), new Vector2(440, 246), new Vector2(432, 256),
        new Vector2(422, 274), new Vector2(408, 289), new Vector2(392, 298),
        new Vector2(378, 301), new Vector2(363, 298), new Vector2(349, 291),
        new Vector2(337, 280), new Vector2(330, 265), new Vector2(330, 250),
        new Vector2(335, 233), new Vector2(344, 216), new Vector2(355, 201),
        new Vector2(349, 197), new Vector2(335, 193), new Vector2(320, 192),
        new Vector2(305, 193), new Vector2(290, 197), new Vector2(282, 201),
        new Vector2(292, 217), new Vector2(300, 235), new Vector2(305, 252),
        new Vector2(304, 267), new Vector2(297, 281), new Vector2(286, 292),
        new Vector2(272, 299), new Vector2(258, 301), new Vector2(244, 298),
        new Vector2(229, 290), new Vector2(217, 277), new Vector2(207, 260),
        new Vector2(201, 245), new Vector2(193, 261), new Vector2(184, 285),
        new Vector2(175, 314), new Vector2(166, 337), new Vector2(157, 349),
        new Vector2(148, 351), new Vector2(139, 347), new Vector2(131, 335),
        new Vector2(125, 315), new Vector2(121, 291), new Vector2(119.5f, 266),
        new Vector2(120, 241), new Vector2(123, 216), new Vector2(127, 190),
        new Vector2(133, 163), new Vector2(141, 142), new Vector2(153, 125),
        new Vector2(169, 111), new Vector2(189, 100), new Vector2(212, 91),
        new Vector2(239, 81), new Vector2(268, 74), new Vector2(295, 69)
    };
}
