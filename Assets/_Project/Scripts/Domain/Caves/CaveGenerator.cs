using System;
using System.Collections.Generic;

namespace SubmarineLDJam.Domain.Caves
{
    public readonly struct CaveCell : IEquatable<CaveCell>
    {
        public int X { get; }
        public int Y { get; }

        public CaveCell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(CaveCell other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is CaveCell other && Equals(other);
        public override int GetHashCode() => X * 73856093 ^ Y * 19349663;
    }

    /// <summary>Numbers for one cave. Same numbers (and seed) = same cave.</summary>
    public sealed class CaveParameters
    {
        public CaveParameters(
            int width,
            int height,
            int seed,
            float rockPercent = 47f,
            int smoothSteps = 5,
            float minOpenPercent = 25f,
            int minPathCells = 40,
            int obstacles = 0,
            int enemies = 0,
            int pickups = 0)
        {
            if (width < 12 || height < 12) throw new ArgumentException("Cave must be at least 12 x 12 cells.");

            Width = width;
            Height = height;
            Seed = seed;
            RockPercent = rockPercent;
            SmoothSteps = Math.Max(0, smoothSteps);
            MinOpenPercent = minOpenPercent;
            MinPathCells = Math.Max(1, minPathCells);
            Obstacles = Math.Max(0, obstacles);
            Enemies = Math.Max(0, enemies);
            Pickups = Math.Max(0, pickups);
        }

        public int Width { get; }
        public int Height { get; }
        public int Seed { get; }

        /// <summary>Start: this much of the cave is rock (0 to 100). More rock = narrower, more twisting.</summary>
        public float RockPercent { get; }

        /// <summary>How many times the rock is smoothed. More = rounder rocks.</summary>
        public int SmoothSteps { get; }

        /// <summary>The cave is made again if less than this part of it is open water.</summary>
        public float MinOpenPercent { get; }

        /// <summary>Start and exit must be at least this many cells apart by swimming.</summary>
        public int MinPathCells { get; }

        public int Obstacles { get; }
        public int Enemies { get; }

        /// <summary>Exit hint pickups.</summary>
        public int Pickups { get; }
    }

    /// <summary>A finished cave: rock grid plus where things go. Cells are x to the right, y up.</summary>
    public sealed class CaveMap
    {
        private readonly bool[,] _rock;

        internal CaveMap(bool[,] rock, CaveCell start, CaveCell goal, int pathLength,
            IReadOnlyList<CaveCell> obstacles, IReadOnlyList<CaveCell> enemies, IReadOnlyList<CaveCell> pickups)
        {
            _rock = rock;
            Width = rock.GetLength(0);
            Height = rock.GetLength(1);
            Start = start;
            Goal = goal;
            PathLength = pathLength;
            Obstacles = obstacles;
            Enemies = enemies;
            Pickups = pickups;
        }

        public int Width { get; }
        public int Height { get; }
        public CaveCell Start { get; }
        public CaveCell Goal { get; }

        /// <summary>Swim distance, in cells, from start to exit.</summary>
        public int PathLength { get; }

        public IReadOnlyList<CaveCell> Obstacles { get; }
        public IReadOnlyList<CaveCell> Enemies { get; }
        public IReadOnlyList<CaveCell> Pickups { get; }

        /// <summary>Outside the grid counts as rock.</summary>
        public bool IsRock(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height || _rock[x, y];
    }

    /// <summary>
    /// Makes caves: random rock, smoothed (cellular automata) into round uneven shapes.
    /// Only the biggest open area is kept, so every part can be reached.
    /// Start and exit are the two far ends of that area. No Unity code.
    /// </summary>
    public static class CaveGenerator
    {
        private const int MaxAttempts = 60;
        private const int BorderCells = 2;

        private static readonly int[] Dx = { 1, -1, 0, 0 };
        private static readonly int[] Dy = { 0, 0, 1, -1 };

        public static CaveMap Generate(CaveParameters p)
        {
            CaveMap best = null;

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var rng = new Random(unchecked(p.Seed * 7919 + attempt * 104729));
                var map = TryBuild(p, rng);
                if (map == null) continue;

                if (map.PathLength >= p.MinPathCells) return map;
                if (best == null || map.PathLength > best.PathLength) best = map;
            }

            if (best == null)
                throw new InvalidOperationException("Could not make a cave with these numbers. Lower Rock Percent or Min Open Percent.");

            return best; // path shorter than asked, but still a good cave
        }

        private static CaveMap TryBuild(CaveParameters p, Random rng)
        {
            int w = p.Width, h = p.Height;
            var rock = new bool[w, h];

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    rock[x, y] = IsBorder(x, y, w, h) || rng.NextDouble() * 100.0 < p.RockPercent;

            for (int step = 0; step < p.SmoothSteps; step++) rock = Smooth(rock, w, h);

            WidenNarrowPassages(rock, w, h);
            WidenNarrowPassages(rock, w, h);

            int openCount = KeepLargestOpenArea(rock, w, h);
            if ((double)openCount * 100.0 < (double)w * h * p.MinOpenPercent) return null;

            // Two sweeps find the two far ends of the open area.
            var seed = RandomOpenCell(rock, w, h, rng);
            var first = Farthest(Distances(rock, w, h, seed), w, h);
            var distFromStart = Distances(rock, w, h, first);
            var last = Farthest(distFromStart, w, h);
            int pathLength = distFromStart[last.X, last.Y];
            var distFromGoal = Distances(rock, w, h, last);

            OpenAround(rock, first, w, h);
            OpenAround(rock, last, w, h);

            var taken = new List<CaveCell> { first, last };
            // Pickups cover the whole cave, start to exit, as evenly as the water allows.
            var pickups = PickSpread(p.Pickups, Candidates(rock, w, h, distFromStart, distFromGoal,
                Math.Max(4, pathLength / 10), int.MaxValue, 4), taken, rng);
            var obstacles = Pick(p.Obstacles, Candidates(rock, w, h, distFromStart, distFromGoal, 6, int.MaxValue, 5), taken, rng);
            var enemies = Pick(p.Enemies, Candidates(rock, w, h, distFromStart, distFromGoal, 6, int.MaxValue, 5), taken, rng);

            return new CaveMap(rock, first, last, pathLength, obstacles, enemies, pickups);
        }

        private static bool IsBorder(int x, int y, int w, int h)
            => x < BorderCells || y < BorderCells || x >= w - BorderCells || y >= h - BorderCells;

        private static bool Rock(bool[,] rock, int w, int h, int x, int y)
            => x < 0 || y < 0 || x >= w || y >= h || rock[x, y];

        /// <summary>More than 4 rock neighbors = rock. Fewer than 4 = water. Exactly 4 = stays.</summary>
        private static bool[,] Smooth(bool[,] rock, int w, int h)
        {
            var next = new bool[w, h];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (IsBorder(x, y, w, h)) { next[x, y] = true; continue; }

                    int neighbors = 0;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            if ((dx != 0 || dy != 0) && Rock(rock, w, h, x + dx, y + dy)) neighbors++;

                    next[x, y] = neighbors > 4 || (neighbors == 4 && rock[x, y]);
                }
            }
            return next;
        }

        /// <summary>A water cell with rock on both sides is a 1-cell passage. Open one side so the submarine fits.</summary>
        private static void WidenNarrowPassages(bool[,] rock, int w, int h)
        {
            var toOpen = new List<CaveCell>();
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (rock[x, y]) continue;
                    if (Rock(rock, w, h, x - 1, y) && Rock(rock, w, h, x + 1, y)) toOpen.Add(new CaveCell(x + 1, y));
                    if (Rock(rock, w, h, x, y - 1) && Rock(rock, w, h, x, y + 1)) toOpen.Add(new CaveCell(x, y + 1));
                }
            }

            foreach (var cell in toOpen)
                if (!IsBorder(cell.X, cell.Y, w, h)) rock[cell.X, cell.Y] = false;
        }

        /// <summary>Fills every open area except the biggest with rock. Returns the size of the biggest.</summary>
        private static int KeepLargestOpenArea(bool[,] rock, int w, int h)
        {
            var area = new int[w, h];
            var sizes = new List<int> { 0 }; // area ids start at 1
            var queue = new Queue<CaveCell>();

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (rock[x, y] || area[x, y] != 0) continue;

                    int id = sizes.Count;
                    int size = 0;
                    area[x, y] = id;
                    queue.Enqueue(new CaveCell(x, y));

                    while (queue.Count > 0)
                    {
                        var c = queue.Dequeue();
                        size++;
                        for (int d = 0; d < 4; d++)
                        {
                            int nx = c.X + Dx[d], ny = c.Y + Dy[d];
                            if (Rock(rock, w, h, nx, ny) || area[nx, ny] != 0) continue;
                            area[nx, ny] = id;
                            queue.Enqueue(new CaveCell(nx, ny));
                        }
                    }
                    sizes.Add(size);
                }
            }

            int biggest = 0;
            for (int i = 1; i < sizes.Count; i++)
                if (biggest == 0 || sizes[i] > sizes[biggest]) biggest = i;

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (!rock[x, y] && area[x, y] != biggest) rock[x, y] = true;

            return biggest == 0 ? 0 : sizes[biggest];
        }

        private static CaveCell RandomOpenCell(bool[,] rock, int w, int h, Random rng)
        {
            var open = new List<CaveCell>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (!rock[x, y]) open.Add(new CaveCell(x, y));
            return open[rng.Next(open.Count)];
        }

        /// <summary>Swim distance in cells from a cell to every open cell. -1 = cannot reach.</summary>
        private static int[,] Distances(bool[,] rock, int w, int h, CaveCell from)
        {
            var dist = new int[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    dist[x, y] = -1;

            var queue = new Queue<CaveCell>();
            dist[from.X, from.Y] = 0;
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int nx = c.X + Dx[d], ny = c.Y + Dy[d];
                    if (Rock(rock, w, h, nx, ny) || dist[nx, ny] >= 0) continue;
                    dist[nx, ny] = dist[c.X, c.Y] + 1;
                    queue.Enqueue(new CaveCell(nx, ny));
                }
            }
            return dist;
        }

        private static CaveCell Farthest(int[,] dist, int w, int h)
        {
            var best = new CaveCell(0, 0);
            int bestDistance = -1;
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (dist[x, y] <= bestDistance) continue;
                    bestDistance = dist[x, y];
                    best = new CaveCell(x, y);
                }
            }
            return best;
        }

        /// <summary>Room around start and exit, so the submarine never begins stuck.</summary>
        private static void OpenAround(bool[,] rock, CaveCell center, int w, int h)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int x = center.X + dx, y = center.Y + dy;
                    if (x >= 0 && y >= 0 && x < w && y < h && !IsBorder(x, y, w, h)) rock[x, y] = false;
                }
            }
        }

        /// <summary>Open cells with open water all around, far enough from start and exit.</summary>
        private static List<CaveCell> Candidates(bool[,] rock, int w, int h, int[,] fromStart, int[,] fromGoal,
            int minFromStart, int maxFromStart, int minFromGoal)
        {
            var list = new List<CaveCell>();
            for (int x = 1; x < w - 1; x++)
            {
                for (int y = 1; y < h - 1; y++)
                {
                    if (rock[x, y]) continue;
                    int s = fromStart[x, y], g = fromGoal[x, y];
                    if (s < minFromStart || s > maxFromStart || g < minFromGoal) continue;

                    bool roomy = true;
                    for (int dx = -1; dx <= 1 && roomy; dx++)
                        for (int dy = -1; dy <= 1 && roomy; dy++)
                            if (rock[x + dx, y + dy]) roomy = false;

                    if (roomy) list.Add(new CaveCell(x, y));
                }
            }
            return list;
        }

        /// <summary>
        /// Spread out: each new one is the best of a few random candidates, "best" = farthest from everything taken so far.
        /// So they fill the whole cave instead of clumping. If the cave is full at spacing 4, the spacing shrinks (3, then 2)
        /// so the wanted count is reached when there is room at all.
        /// </summary>
        private static List<CaveCell> PickSpread(int count, List<CaveCell> candidates, List<CaveCell> taken, Random rng)
        {
            const int sample = 10;
            var chosen = new List<CaveCell>();
            var pool = new List<CaveCell>(candidates);

            foreach (int spacing in new[] { 4, 3, 2 })
            {
                while (chosen.Count < count)
                {
                    CaveCell best = default;
                    double bestScore = -1.0;
                    int bestIndex = -1;

                    for (int i = 0; i < sample; i++)
                    {
                        if (pool.Count == 0) break;
                        int index = rng.Next(pool.Count);
                        var candidate = pool[index];

                        double nearest = double.MaxValue;
                        foreach (var other in taken)
                        {
                            int dx = other.X - candidate.X, dy = other.Y - candidate.Y;
                            nearest = Math.Min(nearest, Math.Sqrt(dx * dx + dy * dy));
                            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) < spacing) { nearest = -1.0; break; }
                        }

                        if (nearest < 0.0)
                        {
                            pool.RemoveAt(index); // too close at this spacing
                            continue;
                        }

                        if (nearest > bestScore)
                        {
                            bestScore = nearest;
                            best = candidate;
                            bestIndex = index;
                        }
                    }

                    if (bestIndex < 0)
                    {
                        if (pool.Count == 0) break;
                        continue; // only bad candidates in this sample, try again
                    }

                    chosen.Add(best);
                    taken.Add(best);
                    pool.Remove(best);
                }

                if (chosen.Count >= count) break;
                pool = new List<CaveCell>(candidates); // smaller spacing: look again at everything
                pool.RemoveAll(c => chosen.Contains(c));
            }
            return chosen;
        }

        /// <summary>Takes random candidates, never two closer than 4 cells. Added to <paramref name="taken"/>.</summary>
        private static List<CaveCell> Pick(int count, List<CaveCell> candidates, List<CaveCell> taken, Random rng)
        {
            const int minSpacing = 4;
            var chosen = new List<CaveCell>();

            for (int i = candidates.Count - 1; i > 0; i--) // shuffle
            {
                int j = rng.Next(i + 1);
                var swap = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = swap;
            }

            foreach (var candidate in candidates)
            {
                if (chosen.Count >= count) break;

                bool tooClose = false;
                foreach (var other in taken)
                {
                    if (Math.Max(Math.Abs(other.X - candidate.X), Math.Abs(other.Y - candidate.Y)) < minSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                chosen.Add(candidate);
                taken.Add(candidate);
            }
            return chosen;
        }
    }
}
