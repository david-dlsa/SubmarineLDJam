using System.Collections.Generic;
using SubmarineLDJam.Domain.Caves;
using UnityEngine;

namespace SubmarineLDJam.Presentation
{
    /// <summary>
    /// Turns the rock grid into a smooth uneven shape (marching squares):
    /// a flat mesh for the rock, and closed outline loops for colliders.
    /// No texture. Only the shape of the wall.
    /// </summary>
    public static class CaveMeshBuilder
    {
        private const float Threshold = 0.5f;

        public sealed class Geometry
        {
            public Vector3[] Vertices;
            public int[] Triangles;

            /// <summary>Each loop is closed (last point = first point). Use for EdgeCollider2D.</summary>
            public readonly List<Vector2[]> Loops = new List<Vector2[]>();
        }

        /// <summary>World position of a cell center. The cave is centered on (0,0).</summary>
        public static Vector2 CellCenter(CaveMap map, float cellSize, int x, int y)
            => new Vector2((x + 0.5f - map.Width * 0.5f) * cellSize, (y + 0.5f - map.Height * 0.5f) * cellSize);

        public static Vector2 CellCenter(CaveMap map, float cellSize, CaveCell cell)
            => CellCenter(map, cellSize, cell.X, cell.Y);

        public static Geometry Build(CaveMap map, float cellSize)
        {
            int w = map.Width, h = map.Height;

            // How "rocky" each spot is, 0 to 1. A bit blurred, so edges are not straight lines.
            var field = new float[w, h];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    float self = map.IsRock(x, y) ? 1f : 0f;
                    float around = 0f;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            if ((dx != 0 || dy != 0) && map.IsRock(x + dx, y + dy)) around += 1f;

                    field[x, y] = 0.5f * self + 0.5f * (around / 8f);
                }
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var crossings = new Dictionary<long, Vector2>();
            var links = new Dictionary<long, List<long>>();

            var cx = new int[4];
            var cy = new int[4];
            var value = new float[4];
            var inside = new bool[4];
            var edge = new long[4];
            var polygon = new List<Vector2>(8);

            for (int x = 0; x < w - 1; x++)
            {
                for (int y = 0; y < h - 1; y++)
                {
                    // Corners go around counter-clockwise: 0 bottom-left, 1 bottom-right, 2 top-right, 3 top-left.
                    // Edge i joins corner i and corner i+1.
                    cx[0] = x; cy[0] = y;
                    cx[1] = x + 1; cy[1] = y;
                    cx[2] = x + 1; cy[2] = y + 1;
                    cx[3] = x; cy[3] = y + 1;

                    int insideCount = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        value[i] = field[cx[i], cy[i]];
                        inside[i] = value[i] >= Threshold;
                        if (inside[i]) insideCount++;
                    }
                    if (insideCount == 0) continue;

                    polygon.Clear();
                    int crossingCount = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        int j = (i + 1) & 3;
                        if (inside[i]) polygon.Add(CellCenter(map, cellSize, cx[i], cy[i]));

                        if (inside[i] != inside[j])
                        {
                            edge[i] = Crossing(crossings, field, map, cellSize, cx[i], cy[i], cx[j], cy[j]);
                            polygon.Add(crossings[edge[i]]);
                            crossingCount++;
                        }
                        else
                        {
                            edge[i] = -1;
                        }
                    }

                    // Two rock corners on opposite sides: rock is joined in the middle, or not.
                    bool saddle = insideCount == 2 && inside[0] == inside[2];
                    bool joined = saddle && (value[0] + value[1] + value[2] + value[3]) * 0.25f >= Threshold;

                    // Mesh
                    if (saddle && !joined)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (!inside[i]) continue;
                            AddTriangle(vertices, triangles,
                                CellCenter(map, cellSize, cx[i], cy[i]),
                                crossings[edge[i]],
                                crossings[edge[(i + 3) & 3]]);
                        }
                    }
                    else
                    {
                        AddFan(vertices, triangles, polygon);
                    }

                    // Outline
                    if (crossingCount == 2)
                    {
                        long a = -1, b = -1;
                        for (int i = 0; i < 4; i++)
                        {
                            if (edge[i] < 0) continue;
                            if (a < 0) a = edge[i]; else b = edge[i];
                        }
                        Link(links, a, b);
                    }
                    else if (crossingCount == 4)
                    {
                        // The two lines cut off two opposite corners: the water ones if rock is joined, the rock ones if not.
                        bool cutCorners02 = inside[0] != joined;
                        if (cutCorners02)
                        {
                            Link(links, edge[3], edge[0]);
                            Link(links, edge[1], edge[2]);
                        }
                        else
                        {
                            Link(links, edge[0], edge[1]);
                            Link(links, edge[2], edge[3]);
                        }
                    }
                }
            }

            var geometry = new Geometry
            {
                Vertices = vertices.ToArray(),
                Triangles = triangles.ToArray()
            };
            BuildLoops(links, crossings, geometry.Loops);
            return geometry;
        }

        /// <summary>Point on the line between two grid points where "rockiness" crosses the threshold. Same point for both squares that share the line.</summary>
        private static long Crossing(Dictionary<long, Vector2> cache, float[,] field, CaveMap map, float cellSize,
            int ax, int ay, int bx, int by)
        {
            int ia = ay * map.Width + ax;
            int ib = by * map.Width + bx;
            if (ia > ib)
            {
                (ia, ib) = (ib, ia);
                (ax, bx) = (bx, ax);
                (ay, by) = (by, ay);
            }

            long id = (long)ia * (map.Width * map.Height) + ib;
            if (!cache.ContainsKey(id))
            {
                float va = field[ax, ay];
                float vb = field[bx, by];
                float t = (Threshold - va) / (vb - va);
                cache[id] = Vector2.Lerp(CellCenter(map, cellSize, ax, ay), CellCenter(map, cellSize, bx, by), t);
            }
            return id;
        }

        private static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector2 a, Vector2 b, Vector2 c)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
        }

        /// <summary>Convex polygon, drawn as a fan from its first point.</summary>
        private static void AddFan(List<Vector3> vertices, List<int> triangles, List<Vector2> polygon)
        {
            int start = vertices.Count;
            foreach (var point in polygon) vertices.Add(point);

            for (int k = 1; k < polygon.Count - 1; k++)
            {
                triangles.Add(start);
                triangles.Add(start + k + 1);
                triangles.Add(start + k);
            }
        }

        private static void Link(Dictionary<long, List<long>> links, long a, long b)
        {
            AddLink(links, a, b);
            AddLink(links, b, a);
        }

        private static void AddLink(Dictionary<long, List<long>> links, long from, long to)
        {
            if (!links.TryGetValue(from, out var list))
            {
                list = new List<long>(2);
                links[from] = list;
            }
            list.Add(to);
        }

        /// <summary>Joins the small line pieces into closed loops.</summary>
        private static void BuildLoops(Dictionary<long, List<long>> links, Dictionary<long, Vector2> crossings, List<Vector2[]> loops)
        {
            var visited = new HashSet<long>();

            foreach (var pair in links)
            {
                if (visited.Contains(pair.Key)) continue;

                var points = new List<Vector2>();
                long current = pair.Key;

                while (true)
                {
                    visited.Add(current);
                    points.Add(crossings[current]);

                    long next = -1;
                    foreach (var neighbor in links[current])
                    {
                        if (visited.Contains(neighbor)) continue;
                        next = neighbor;
                        break;
                    }

                    if (next < 0) break;
                    current = next;
                }

                if (points.Count < 3) continue;
                points.Add(points[0]); // close it
                loops.Add(points.ToArray());
            }
        }
    }
}
