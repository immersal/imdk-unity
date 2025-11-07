/*===============================================================================
Copyright (C) 2024 Immersal - Part of Hexagon. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sales@immersal.com for licensing requests.
===============================================================================*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Immersal
{
    public static class PlyImporter
    {
        public static Mesh PlyToMesh(byte[] bytes, string name)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    Mesh mesh = StreamToMesh(stream);
                    mesh.name = name;
                    return mesh;
                }
            }
            catch (Exception e)
            {
                ImmersalLogger.LogError($"Failed importing {name}: {e.Message}");
                return null;
            }
        }

        public static Mesh PlyToMesh(string filePath)
        {
            try
            {
                var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                Mesh mesh = StreamToMesh(stream);
                mesh.name = Path.GetFileNameWithoutExtension(filePath);
                return mesh;
            }
            catch (Exception e)
            {
                ImmersalLogger.LogError($"Failed importing {filePath}: {e.Message}");
                return null;
            }
        }

        private static Mesh StreamToMesh(Stream stream)
        {
            if (!CheckHeader(new StreamReader(stream), out int vertexCount))
                throw new ArgumentException("Unexpected header data");

            PlyDataBody body = ReadDataBody(new BinaryReader(stream), vertexCount);

            var mesh = new Mesh
            {
                name = "Sparse",
                indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            mesh.SetVertices(body.Vertices);
            mesh.SetColors(body.Colors);

            mesh.SetIndices(
                Enumerable.Range(0, vertexCount).ToArray(),
                MeshTopology.Points, 0
            );
            
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static bool CheckHeader(StreamReader reader, out int vertexCount, int maxHeaderLineReads = 15)
        {
            vertexCount = -1;
            int readCount = 0;
            int linesRead = 0;

            // Magic number line ("ply")
            var line = reader.ReadLine();
            readCount += line.Length + 1;
            if (line != "ply")
                throw new ArgumentException("Magic number ('ply') mismatch.");

            // check if it's binary/little endian.
            line = reader.ReadLine();
            readCount += line.Length + 1;
            if (line != "format binary_little_endian 1.0")
                throw new ArgumentException(
                    "Invalid data format ('" + line + "'). " +
                    "Should be binary/little endian.");

            while (linesRead < maxHeaderLineReads)
            {
                line = reader.ReadLine();
                linesRead++;
                readCount += line.Length + 1;
                if (line == "end_header") break;
                var col = line.Split();

                if (col[0] == "element")
                {
                    if (col[1] == "vertex")
                    {
                        vertexCount = Convert.ToInt32(col[2]);
                    }
                }
            }

            // Rewind the stream back to the exact position of the reader.
            reader.BaseStream.Position = readCount;

            return vertexCount > -1;
        }

        private static PlyDataBody ReadDataBody(BinaryReader reader, int vertexCount)
        {
            PlyDataBody data = new PlyDataBody(vertexCount);

            for (var i = 0; i < vertexCount; i++)
            {
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                float z = reader.ReadSingle();
                Byte r = reader.ReadByte();
                Byte g = reader.ReadByte();
                Byte b = reader.ReadByte();

                //a = reader.ReadByte();
                Byte a = Byte.MaxValue;

                data.AddPoint(-x, y, z, r, g, b, a);
            }

            return data;
        }
    }

    public class PlyDataBody
    {
        public List<Vector3> Vertices;
        public List<Color32> Colors;

        public PlyDataBody(int vertexCount)
        {
            Vertices = new List<Vector3>(vertexCount);
            Colors = new List<Color32>(vertexCount);
        }

        public void AddPoint(
            float x, float y, float z,
            byte r, byte g, byte b, byte a
        )
        {
            Vertices.Add(new Vector3(x, y, z));
            Colors.Add(new Color32(r, g, b, a));
        }
    }
}