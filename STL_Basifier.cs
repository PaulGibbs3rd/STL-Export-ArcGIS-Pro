using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace STL_Export_Tool
{
    internal static class STL_Basifier
    {
        struct V3
        {
            public float X, Y, Z;
            public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
            public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        struct TriIdx { public int A, B, C; public TriIdx(int a, int b, int c) { A = a; B = b; C = c; } }

        /// <summary>
        /// Rescales all vertex coordinates of an STL file by a uniform factor. Used to correct
        /// meshes that were exported in degenerate/wrong units (e.g. raw decimal degrees instead
        /// of real-world linear units) into proper real-world meters.
        /// </summary>
        public static void RescaleMesh(string inPath, string outPath, double scaleX, double scaleY, double scaleZ)
        {
            List<V3> verts; List<TriIdx> tris;
            ReadSTL(inPath, out verts, out tris);

            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                verts[i] = new V3((float)(v.X * scaleX), (float)(v.Y * scaleY), (float)(v.Z * scaleZ));
            }

            WriteBinarySTL(outPath, verts, tris);
        }

        /// <summary>
        /// Reads an STL file and reports its raw vertex bounding box and triangle count,
        /// without modifying it. Useful for diagnosing whether the exporter that produced
        /// the file actually wrote meaningful real-world geometry.
        /// </summary>
        public static (int triangleCount, float minX, float minY, float minZ, float maxX, float maxY, float maxZ) GetMeshBounds(string path)
        {
            List<V3> verts; List<TriIdx> tris;
            ReadSTL(path, out verts, out tris);

            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            foreach (var v in verts)
            {
                if (v.X < minX) minX = v.X;
                if (v.Y < minY) minY = v.Y;
                if (v.Z < minZ) minZ = v.Z;
                if (v.X > maxX) maxX = v.X;
                if (v.Y > maxY) maxY = v.Y;
                if (v.Z > maxZ) maxZ = v.Z;
            }

            if (verts.Count == 0)
                minX = minY = minZ = maxX = maxY = maxZ = 0f;

            return (tris.Count, minX, minY, minZ, maxX, maxY, maxZ);
        }

        /// <summary>
        /// Creates a watertight solid by connecting 4 walls directly to the mesh boundary edges.
        /// The mesh surface IS the top - no separate top face is added.
        /// Walls drop straight down from the mesh perimeter to create a solid base.
        /// </summary>
        public static void AddExtrudedBase(string inPath, string outPath, float thickness, float padding = 0f)
        {
            if (!File.Exists(inPath)) throw new FileNotFoundException(inPath);
            if (thickness <= 0) throw new ArgumentException("Base thickness must be > 0", nameof(thickness));

            // Read original STL
            List<V3> verts; List<TriIdx> tris;
            ReadSTL(inPath, out verts, out tris);
            if (tris.Count == 0) throw new InvalidOperationException("No triangles found.");

            // Keep all original triangles - they ARE the top surface
            var allTris = new List<TriIdx>(tris);

            // Find bounding box
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var v in verts)
            {
                if (v.X < minX) minX = v.X;
                if (v.Y < minY) minY = v.Y;
                if (v.Z < minZ) minZ = v.Z;
                if (v.X > maxX) maxX = v.X;
                if (v.Y > maxY) maxY = v.Y;
            }

            // Apply padding to expand the base
            minX -= padding;
            minY -= padding;
            maxX += padding;
            maxY += padding;

            // Sanity check: if the mesh footprint is far smaller than the requested base
            // thickness, the source mesh is almost certainly degenerate (e.g. an accidental
            // tiny/zero-size export extent) rather than the thickness genuinely being wrong.
            float footprintX = maxX - minX;
            float footprintY = maxY - minY;
            float maxFootprint = Math.Max(footprintX, footprintY);
            if (maxFootprint <= 0f || thickness > maxFootprint * 50f)
            {
                throw new InvalidOperationException(
                    $"Mesh footprint ({maxFootprint:0.######} units) is far smaller than the requested base " +
                    $"thickness ({thickness} units). This usually means the export extent was too small " +
                    "(e.g. a tiny/degenerate rectangle was drawn on the map). Re-draw a larger extent and export again.");
            }

            // Base level
            float baseZ = minZ - thickness;

            // Find vertices that are ON the boundary edges of the mesh
            List<int> leftEdgeVerts = new List<int>();    // X = minX
            List<int> rightEdgeVerts = new List<int>();   // X = maxX
            List<int> backEdgeVerts = new List<int>();    // Y = minY
            List<int> frontEdgeVerts = new List<int>();   // Y = maxY

            float tolerance = Math.Min((maxX - minX), (maxY - minY)) * 0.001f; // Very small tolerance

            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                if (Math.Abs(v.X - minX) <= tolerance) leftEdgeVerts.Add(i);
                if (Math.Abs(v.X - maxX) <= tolerance) rightEdgeVerts.Add(i);
                if (Math.Abs(v.Y - minY) <= tolerance) backEdgeVerts.Add(i);
                if (Math.Abs(v.Y - maxY) <= tolerance) frontEdgeVerts.Add(i);
            }

            // Create base vertices by projecting edge vertices down
            var baseVertexMap = new Dictionary<int, int>();

            // Add base vertices for each edge vertex
            foreach (int idx in leftEdgeVerts.Concat(rightEdgeVerts).Concat(backEdgeVerts).Concat(frontEdgeVerts).Distinct())
            {
                if (!baseVertexMap.ContainsKey(idx))
                {
                    var v = verts[idx];
                    baseVertexMap[idx] = verts.Count;
                    verts.Add(new V3(v.X, v.Y, baseZ)); // Project straight down
                }
            }

            // Create corner vertices for the base
            int baseCornerStart = verts.Count;
            verts.Add(new V3(minX, minY, baseZ)); // 0: back-left
            verts.Add(new V3(maxX, minY, baseZ)); // 1: back-right
            verts.Add(new V3(maxX, maxY, baseZ)); // 2: front-right
            verts.Add(new V3(minX, maxY, baseZ)); // 3: front-left

            // Add flat bottom (4 corners only)
            allTris.Add(new TriIdx(baseCornerStart + 0, baseCornerStart + 2, baseCornerStart + 1));
            allTris.Add(new TriIdx(baseCornerStart + 0, baseCornerStart + 3, baseCornerStart + 2));

            // Create walls by connecting edge vertices to their base projections and corners
            // Left wall (X = minX)
            if (leftEdgeVerts.Count > 0)
            {
                leftEdgeVerts.Sort((a, b) => verts[a].Y.CompareTo(verts[b].Y)); // Sort by Y
                for (int i = 0; i < leftEdgeVerts.Count - 1; i++)
                {
                    int curr = leftEdgeVerts[i];
                    int next = leftEdgeVerts[i + 1];
                    int currBase = baseVertexMap[curr];
                    int nextBase = baseVertexMap[next];

                    allTris.Add(new TriIdx(curr, next, nextBase));
                    allTris.Add(new TriIdx(curr, nextBase, currBase));
                }
                // Connect to corners
                int firstLeft = leftEdgeVerts[0];
                int lastLeft = leftEdgeVerts[leftEdgeVerts.Count - 1];
                allTris.Add(new TriIdx(baseCornerStart + 0, firstLeft, baseVertexMap[firstLeft]));
                allTris.Add(new TriIdx(baseCornerStart + 3, baseVertexMap[lastLeft], lastLeft));
            }

            // Right wall (X = maxX)
            if (rightEdgeVerts.Count > 0)
            {
                rightEdgeVerts.Sort((a, b) => verts[a].Y.CompareTo(verts[b].Y));
                for (int i = 0; i < rightEdgeVerts.Count - 1; i++)
                {
                    int curr = rightEdgeVerts[i];
                    int next = rightEdgeVerts[i + 1];
                    int currBase = baseVertexMap[curr];
                    int nextBase = baseVertexMap[next];

                    allTris.Add(new TriIdx(curr, nextBase, next));
                    allTris.Add(new TriIdx(curr, currBase, nextBase));
                }
                // Connect to corners
                int firstRight = rightEdgeVerts[0];
                int lastRight = rightEdgeVerts[rightEdgeVerts.Count - 1];
                allTris.Add(new TriIdx(baseCornerStart + 1, baseVertexMap[firstRight], firstRight));
                allTris.Add(new TriIdx(baseCornerStart + 2, lastRight, baseVertexMap[lastRight]));
            }

            // Back wall (Y = minY)
            if (backEdgeVerts.Count > 0)
            {
                backEdgeVerts.Sort((a, b) => verts[a].X.CompareTo(verts[b].X));
                for (int i = 0; i < backEdgeVerts.Count - 1; i++)
                {
                    int curr = backEdgeVerts[i];
                    int next = backEdgeVerts[i + 1];
                    int currBase = baseVertexMap[curr];
                    int nextBase = baseVertexMap[next];

                    allTris.Add(new TriIdx(curr, nextBase, next));
                    allTris.Add(new TriIdx(curr, currBase, nextBase));
                }
            }

            // Front wall (Y = maxY)
            if (frontEdgeVerts.Count > 0)
            {
                frontEdgeVerts.Sort((a, b) => verts[a].X.CompareTo(verts[b].X));
                for (int i = 0; i < frontEdgeVerts.Count - 1; i++)
                {
                    int curr = frontEdgeVerts[i];
                    int next = frontEdgeVerts[i + 1];
                    int currBase = baseVertexMap[curr];
                    int nextBase = baseVertexMap[next];

                    allTris.Add(new TriIdx(curr, next, nextBase));
                    allTris.Add(new TriIdx(curr, nextBase, currBase));
                }
            }

            WriteBinarySTL(outPath, verts, allTris);
        }

        /// <summary>Safe wrapper for the extruded base method.</summary>
        public static bool TryAddExtrudedBase(string inPath, string outPath, float thickness, float padding, out string reason)
        {
            try
            {
                AddExtrudedBase(inPath, outPath, thickness, padding);
                reason = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                reason = ex.Message;
                return false;
            }
        }

        // Legacy methods for fallback
        public static void AddRectangularBaseAuto(string inPath, string outPath, float thickness, float outset = 0f, float raise = 0.001f)
        {
            List<V3> rawVerts; List<TriIdx> rawTris;
            ReadSTL(inPath, out rawVerts, out rawTris);
            
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var v in rawVerts)
            {
                if (v.X < minX) minX = v.X;
                if (v.Y < minY) minY = v.Y;
                if (v.Z < minZ) minZ = v.Z;
                if (v.X > maxX) maxX = v.X;
                if (v.Y > maxY) maxY = v.Y;
            }

            var allTris = new List<TriIdx>(rawTris);
            float baseBottomZ = minZ - Math.Abs(thickness);
            float baseTopZ = minZ + Math.Max(raise, 0f);

            float x0 = minX - outset, y0 = minY - outset;
            float x1 = maxX + outset, y1 = maxY + outset;

            int o = rawVerts.Count;
            rawVerts.AddRange(new[]
            {
                new V3(x0, y0, baseBottomZ), new V3(x1, y0, baseBottomZ),
                new V3(x1, y1, baseBottomZ), new V3(x0, y1, baseBottomZ),
                new V3(x0, y0, baseTopZ), new V3(x1, y0, baseTopZ),
                new V3(x1, y1, baseTopZ), new V3(x0, y1, baseTopZ)
            });

            allTris.AddRange(new[]
            {
                new TriIdx(o + 0, o + 2, o + 1), new TriIdx(o + 0, o + 3, o + 2),
                new TriIdx(o + 4, o + 5, o + 6), new TriIdx(o + 4, o + 6, o + 7),
                new TriIdx(o + 0, o + 1, o + 5), new TriIdx(o + 0, o + 5, o + 4),
                new TriIdx(o + 1, o + 2, o + 6), new TriIdx(o + 1, o + 6, o + 5),
                new TriIdx(o + 2, o + 3, o + 7), new TriIdx(o + 2, o + 7, o + 6),
                new TriIdx(o + 3, o + 0, o + 4), new TriIdx(o + 3, o + 4, o + 7)
            });

            WriteBinarySTL(outPath, rawVerts, allTris);
        }

        // Simplified STL I/O
        static void ReadSTL(string path, out List<V3> verts, out List<TriIdx> tris)
        {
            if (LooksLikeBinarySTL(path))
                ReadBinarySTL(path, out verts, out tris);
            else
                ReadAsciiSTL(path, out verts, out tris);
        }

        static bool LooksLikeBinarySTL(string path)
        {
            using var br = new BinaryReader(File.OpenRead(path));
            if (br.BaseStream.Length < 84) return false;
            br.BaseStream.Seek(80, SeekOrigin.Begin);
            uint triCount = br.ReadUInt32();
            long expected = 84 + (long)triCount * 50;
            return br.BaseStream.Length == expected;
        }

        static void ReadBinarySTL(string path, out List<V3> verts, out List<TriIdx> tris)
        {
            verts = new List<V3>();
            tris = new List<TriIdx>();

            using var br = new BinaryReader(File.OpenRead(path));
            br.ReadBytes(80); // header
            uint triCount = br.ReadUInt32();

            for (uint i = 0; i < triCount; i++)
            {
                br.ReadSingle(); br.ReadSingle(); br.ReadSingle(); // normal (ignored)
                var v0 = new V3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                var v1 = new V3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                var v2 = new V3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                br.ReadUInt16(); // attribute bytes

                int idx0 = verts.Count; verts.Add(v0);
                int idx1 = verts.Count; verts.Add(v1);
                int idx2 = verts.Count; verts.Add(v2);
                tris.Add(new TriIdx(idx0, idx1, idx2));
            }
        }

        static void ReadAsciiSTL(string path, out List<V3> verts, out List<TriIdx> tris)
        {
            verts = new List<V3>();
            tris = new List<TriIdx>();

            using var sr = new StreamReader(path);
            string line;
            var current = new List<int>(3);
            while ((line = sr.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.StartsWith("vertex", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        float x = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        float y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                        float z = float.Parse(parts[3], CultureInfo.InvariantCulture);
                        int idx = verts.Count; verts.Add(new V3(x, y, z));
                        current.Add(idx);
                        if (current.Count == 3)
                        {
                            tris.Add(new TriIdx(current[0], current[1], current[2]));
                            current.Clear();
                        }
                    }
                }
            }
        }

        static void WriteBinarySTL(string path, List<V3> verts, List<TriIdx> tris)
        {
            using var bw = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write));
            var header = new byte[80];
            var label = System.Text.Encoding.ASCII.GetBytes("STL with base generated by STL_Basifier");
            Array.Copy(label, header, Math.Min(label.Length, 80));
            bw.Write(header);
            bw.Write((uint)tris.Count);

            foreach (var t in tris)
            {
                var n = ComputeNormal(verts[t.A], verts[t.B], verts[t.C]);
                bw.Write(n.X); bw.Write(n.Y); bw.Write(n.Z);
                bw.Write(verts[t.A].X); bw.Write(verts[t.A].Y); bw.Write(verts[t.A].Z);
                bw.Write(verts[t.B].X); bw.Write(verts[t.B].Y); bw.Write(verts[t.B].Z);
                bw.Write(verts[t.C].X); bw.Write(verts[t.C].Y); bw.Write(verts[t.C].Z);
                bw.Write((ushort)0);
            }
        }

        static V3 ComputeNormal(V3 a, V3 b, V3 c)
        {
            var u = b - a;
            var v = c - a;
            var nx = u.Y * v.Z - u.Z * v.Y;
            var ny = u.Z * v.X - u.X * v.Z;
            var nz = u.X * v.Y - u.Y * v.X;
            var len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len < 1e-12f) return new V3(0, 0, 0);
            return new V3(nx / len, ny / len, nz / len);
        }
    }
}
