using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Scripts.Map.Visibility
{
    internal class MeshBuilder
    {
        private readonly Mesh mesh = new()
        {
            name = "Procedural Mesh"
        };

        public void Build()
        {
            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = meshDataArray[0];

            int vertexAttributeCount = 1;
            int vertexCount = 4;
            int triangleIndexCount = 6;
            
            var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(vertexAttributeCount, Allocator.Temp);
            vertexAttributes[0] = new VertexAttributeDescriptor(dimension: 3);
            meshData.SetVertexBufferParams(vertexCount, vertexAttributes);
            vertexAttributes.Dispose();

            NativeArray<Vector3> positions = meshData.GetVertexData<Vector3>();
            positions[0] = Vector3.zero;
            positions[1] = Vector3.right;
            positions[2] = Vector3.up;
            positions[3] = new Vector3(1f, 1f, 0f);

            meshData.SetIndexBufferParams(triangleIndexCount, IndexFormat.UInt16);
            NativeArray<ushort> triangleIndices = meshData.GetIndexData<ushort>();
            triangleIndices[0] = 0;
            triangleIndices[1] = 2;
            triangleIndices[2] = 1;
            triangleIndices[3] = 1;
            triangleIndices[4] = 2;
            triangleIndices[5] = 3;

            meshData.subMeshCount = 1;
            var bounds = new Bounds(new Vector3(0.5f, 0.5f), new Vector3(1f, 1f));
            meshData.SetSubMesh(0,  new SubMeshDescriptor(0, triangleIndexCount)
            {
                bounds = bounds,
                vertexCount = vertexCount
            }, MeshUpdateFlags.DontRecalculateBounds);

            mesh.bounds = bounds;
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
        }
    }
}
