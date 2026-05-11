using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class DismemberPreviewUtility
{
    private const string PreviewRootName = "__MTK_DismemberPreviewRoot__";
    private const float ProjectedVertexMergeEpsilon = 0.0001f;
    private const float PreviewFanTriangleAreaEpsilon = 0.000001f;
    private static readonly MethodInfo ClearSkinnedMeshCacheMethod = typeof(RagdollDismembermentVisual).GetMethod(
        "ClearSkinnedMeshCache",
        BindingFlags.Instance | BindingFlags.NonPublic);

    public static void ClearPreview()
    {
        var root = GameObject.Find(PreviewRootName);
        if (root != null)
        {
            var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh != null)
                {
                    Object.DestroyImmediate(meshFilter.sharedMesh);
                }
            }

            Object.DestroyImmediate(root);
        }
    }

    public static bool PreviewFragment(RagdollDismembermentVisual visual, BodyFragment fragment, Material capMaterial)
    {
        if (visual == null || fragment == null || fragment.bone == null)
        {
            return false;
        }

        var previewMeshes = GetPreviewMeshes(visual, fragment, capMaterial);
        if (previewMeshes.Count == 0)
        {
            return false;
        }

        ClearPreview();
        var root = new GameObject(PreviewRootName)
        {
            hideFlags = HideFlags.DontSaveInEditor
        };

        foreach (var previewMesh in previewMeshes)
        {
            CreatePreviewObject(root.transform, previewMesh, previewMesh.AttachedSideMesh, "_Attached");
            CreatePreviewObject(root.transform, previewMesh, previewMesh.DetachedSideMesh, "_Detached");
        }

        Selection.activeGameObject = root;
        SceneView.RepaintAll();
        return true;
    }

    public static List<PreviewMeshData> GetPreviewMeshes(RagdollDismembermentVisual visual, BodyFragment fragment, Material capMaterial)
    {
        var result = new List<PreviewMeshData>();
        var parent = FindParentFragment(visual, fragment);
        if (parent == null || parent.SkinnedMeshes == null)
        {
            return result;
        }

        foreach (var skinnedMesh in parent.SkinnedMeshes)
        {
            if (skinnedMesh == null)
            {
                continue;
            }

            if (visual.ignoreDismember != null && visual.ignoreDismember.Contains(skinnedMesh))
            {
                continue;
            }

            if (!TryBuildPreviewMesh(visual, skinnedMesh, fragment, capMaterial, out var previewMesh))
            {
                continue;
            }

            result.Add(previewMesh);
        }

        return result;
    }

    public static BodyFragment FindParentFragment(RagdollDismembermentVisual visual, BodyFragment fragment)
    {
        if (visual == null || fragment == null || fragment.bone == null || visual.Fragments == null)
        {
            return null;
        }

        var parents = visual.Fragments.FindAll(f =>
            f != null &&
            f != fragment &&
            f.bone != null &&
            fragment.bone.IsChildOf(f.bone) &&
            f.SkinnedMeshes != null &&
            f.SkinnedMeshes.Count > 0);

        if (parents == null || parents.Count == 0)
        {
            return null;
        }

        return fragment.GetNearestParent(parents.ToArray());
    }

    private static bool TryBuildPreviewMesh(
        RagdollDismembermentVisual visual,
        SkinnedMeshRenderer skinnedMesh,
        BodyFragment fragment,
        Material capMaterial,
        out PreviewMeshData previewMesh)
    {
        previewMesh = null;

        if (visual == null || skinnedMesh == null || skinnedMesh.sharedMesh == null || fragment == null || fragment.bone == null)
        {
            return false;
        }

        var bakedMesh = new Mesh
        {
            name = skinnedMesh.sharedMesh.name + "_PreviewBake"
        };

        skinnedMesh.BakeMesh(bakedMesh, true);

        try
        {
            var selectionMask = BuildSelectionMask(skinnedMesh, fragment, visual);
            if (selectionMask == null || selectionMask.Length == 0)
            {
                return false;
            }

            var selectionStatus = GetSelectionStatus(selectionMask);
            if (selectionStatus == RagdollDismembermentVisual.SelectionStatus.Empty)
            {
                return false;
            }

            Mesh detachedMesh;
            Mesh attachedMesh;
            bool capAdded = false;
            if (selectionStatus == RagdollDismembermentVisual.SelectionStatus.Full)
            {
                detachedMesh = bakedMesh.Copy();
                attachedMesh = null;
            }
            else
            {
                detachedMesh = bakedMesh.SimpleSplit(selectionMask);
                attachedMesh = bakedMesh.Copy();

                if (capMaterial != null && !IsEdgeCreationIgnored(visual, skinnedMesh))
                {
                    capAdded = TryAddCap(visual, skinnedMesh, fragment, selectionMask, attachedMesh, detachedMesh);
                }
            }

            previewMesh = new PreviewMeshData
            {
                SourceRenderer = skinnedMesh,
                AttachedSideMesh = attachedMesh,
                DetachedSideMesh = detachedMesh,
                AttachedMaterials = BuildMaterials(skinnedMesh.sharedMaterials, capAdded ? capMaterial : null, attachedMesh != null),
                DetachedMaterials = BuildMaterials(skinnedMesh.sharedMaterials, capAdded ? capMaterial : null, detachedMesh != null)
            };
            return true;
        }
        finally
        {
            Object.DestroyImmediate(bakedMesh);
        }
    }

    private static bool[] BuildSelectionMask(SkinnedMeshRenderer skinnedMesh, BodyFragment fragment, RagdollDismembermentVisual visual)
    {
        if (visual == null || visual.Fragments == null)
        {
            return null;
        }

        ClearSelectionCache(visual);

        var childFragments = visual.Fragments.FindAll(f =>
            f != null &&
            f.bone != null &&
            (f == fragment || f.bone == fragment.bone || f.bone.IsChildOf(fragment.bone)));

        if (childFragments.Count == 0)
        {
            childFragments.Add(fragment);
        }

        for (int i = 0; i < childFragments.Count; i++)
        {
            visual.GetSelection(
                skinnedMesh,
                childFragments[i],
                i == 0 ? RagdollDismembermentVisual.SelectionMode.Replace : RagdollDismembermentVisual.SelectionMode.Add);
        }

        var mask = new bool[visual.SelectionMaskLength];
        System.Array.Copy(visual.SelectionMask, mask, visual.SelectionMaskLength);
        return mask;
    }

    private static void ClearSelectionCache(RagdollDismembermentVisual visual)
    {
        if (visual == null || ClearSkinnedMeshCacheMethod == null)
        {
            return;
        }

        ClearSkinnedMeshCacheMethod.Invoke(visual, null);
    }

    private static RagdollDismembermentVisual.SelectionStatus GetSelectionStatus(bool[] selectionMask)
    {
        if (selectionMask == null || selectionMask.Length == 0)
        {
            return RagdollDismembermentVisual.SelectionStatus.Empty;
        }

        bool first = selectionMask[0];
        for (int i = 0; i < selectionMask.Length; i++)
        {
            if (selectionMask[i] != first)
            {
                return RagdollDismembermentVisual.SelectionStatus.Mixed;
            }
        }

        return first ? RagdollDismembermentVisual.SelectionStatus.Full : RagdollDismembermentVisual.SelectionStatus.Empty;
    }

    private static void CreatePreviewObject(Transform root, PreviewMeshData previewMesh, Mesh mesh, string suffix)
    {
        if (mesh == null || previewMesh.SourceRenderer == null)
        {
            return;
        }

        var source = previewMesh.SourceRenderer;
        var previewObject = new GameObject(source.name + suffix)
        {
            hideFlags = HideFlags.DontSaveInEditor
        };

        previewObject.transform.SetParent(root, false);
        previewObject.transform.position = source.transform.position;
        previewObject.transform.rotation = source.transform.rotation;
        previewObject.transform.localScale = source.transform.lossyScale;

        var meshFilter = previewObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        var meshRenderer = previewObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterials = suffix == "_Attached" ? previewMesh.AttachedMaterials : previewMesh.DetachedMaterials;
        EditorUtility.SetSelectedRenderState(meshRenderer, EditorSelectedRenderState.Wireframe);
    }

    private static bool IsEdgeCreationIgnored(RagdollDismembermentVisual visual, SkinnedMeshRenderer skinnedMesh)
    {
        return visual.ignoreEdgeCreation != null && visual.ignoreEdgeCreation.Contains(skinnedMesh);
    }

    private static bool TryAddCap(
        RagdollDismembermentVisual visual,
        SkinnedMeshRenderer skinnedMesh,
        BodyFragment fragment,
        bool[] selectionMask,
        Mesh attachedMesh,
        Mesh detachedMesh)
    {
        var edgeIndices = BuildEdgeIndices(skinnedMesh.sharedMesh, selectionMask);
        if (edgeIndices.Count == 0)
        {
            return false;
        }

        var capData = BuildCapMeshData(skinnedMesh, fragment, edgeIndices);
        if (capData == null || capData.triangleIndices == null || capData.triangleIndices.Length < 3)
        {
            return false;
        }

        if (attachedMesh != null)
        {
            AddSubMeshWithUV(attachedMesh, capData.triangleIndices, capData.vertexUVMap);
        }

        if (detachedMesh != null)
        {
            AddSubMeshWithUV(detachedMesh, capData.triangleIndices, capData.vertexUVMap);
        }

        return true;
    }

    private static RagdollDismembermentVisual.CapMeshData BuildCapMeshData(
        SkinnedMeshRenderer skinnedMesh,
        BodyFragment fragment,
        List<int> edgeIndices)
    {
        if (skinnedMesh == null || skinnedMesh.sharedMesh == null || fragment == null || edgeIndices == null || edgeIndices.Count < 3)
        {
            return null;
        }

        var uniqueEdgeIndices = edgeIndices.Distinct().ToList();
        if (uniqueEdgeIndices.Count < 3)
        {
            return null;
        }

        var projected = ProjectEdgeVerticesToPlane(skinnedMesh, fragment, uniqueEdgeIndices);
        if (projected.Count < 3)
        {
            return null;
        }

        int[] triangles = ConvexHullFanTriangulate(projected);
        if (triangles == null || triangles.Length < 3)
        {
            return null;
        }

        var vertexUVMap = GenerateCapUVMap(projected);
        return new RagdollDismembermentVisual.CapMeshData
        {
            triangleIndices = triangles,
            vertexUVMap = vertexUVMap
        };
    }

    private static List<int> BuildEdgeIndices(Mesh mesh, bool[] selectionMask)
    {
        var edgeIndices = new List<int>();
        if (mesh == null || selectionMask == null)
        {
            return edgeIndices;
        }

        var triangles = mesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            bool test1 = selectionMask[triangles[i]];
            bool test2 = selectionMask[triangles[i + 1]];
            bool test3 = selectionMask[triangles[i + 2]];

            if (test1 == test2 && test2 == test3)
            {
                continue;
            }

            if (test1)
            {
                edgeIndices.Add(triangles[i]);
            }

            if (test2)
            {
                edgeIndices.Add(triangles[i + 1]);
            }

            if (test3)
            {
                edgeIndices.Add(triangles[i + 2]);
            }
        }

        return edgeIndices;
    }

    private static List<ProjectedEdgeVertex> ProjectEdgeVerticesToPlane(
        SkinnedMeshRenderer skinnedMesh,
        BodyFragment fragment,
        List<int> edgeIndices)
    {
        var result = new List<ProjectedEdgeVertex>();
        var mesh = skinnedMesh.sharedMesh;
        var vertices = mesh.vertices;
        var bindposes = mesh.bindposes;
        var bones = skinnedMesh.bones;

        int bindposeIndex = System.Array.FindIndex(bones, b => b == fragment.bone);
        if (bindposeIndex < 0 || bindposeIndex >= bindposes.Length)
        {
            bindposeIndex = 0;
        }

        Matrix4x4 bindposeMatrix = bindposes[bindposeIndex];
        Vector3 normal = fragment.cutPlaneNormal.normalized;
        Vector3 binormal = fragment.cutPlaneBinormal.normalized;
        Vector3 tangent = Vector3.Cross(normal, binormal).normalized;
        Vector3 planeOrigin = fragment.cutPlanePos;

        if (normal.sqrMagnitude < 0.000001f || binormal.sqrMagnitude < 0.000001f || tangent.sqrMagnitude < 0.000001f)
        {
            tangent = Vector3.right;
            binormal = Vector3.up;
        }

        for (int i = 0; i < edgeIndices.Count; i++)
        {
            int vertexIndex = edgeIndices[i];
            if (vertexIndex < 0 || vertexIndex >= vertices.Length)
            {
                continue;
            }

            Vector3 vertexPos = vertices[vertexIndex];
            Vector3 boneLocalVertex = bindposeMatrix.MultiplyPoint3x4(vertexPos);
            Vector3 relativePos = boneLocalVertex - planeOrigin;

            result.Add(new ProjectedEdgeVertex
            {
                VertexIndex = vertexIndex,
                PlanePosition = new Vector2(
                    Vector3.Dot(relativePos, tangent),
                    Vector3.Dot(relativePos, binormal))
            });
        }

        return result;
    }

    private static Dictionary<int, Vector2> GenerateCapUVMap(List<ProjectedEdgeVertex> projectedVertices)
    {
        var uvMap = new Dictionary<int, Vector2>();
        if (projectedVertices == null || projectedVertices.Count == 0)
        {
            return uvMap;
        }

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);

        for (int i = 0; i < projectedVertices.Count; i++)
        {
            var planePos = projectedVertices[i].PlanePosition;
            if (planePos.x < min.x) min.x = planePos.x;
            if (planePos.y < min.y) min.y = planePos.y;
            if (planePos.x > max.x) max.x = planePos.x;
            if (planePos.y > max.y) max.y = planePos.y;
        }

        Vector2 size = max - min;
        if (Mathf.Abs(size.x) < 0.0001f) size.x = 1f;
        if (Mathf.Abs(size.y) < 0.0001f) size.y = 1f;

        for (int i = 0; i < projectedVertices.Count; i++)
        {
            var projectedVertex = projectedVertices[i];
            uvMap[projectedVertex.VertexIndex] = new Vector2(
                (projectedVertex.PlanePosition.x - min.x) / size.x,
                (projectedVertex.PlanePosition.y - min.y) / size.y);
        }

        return uvMap;
    }

    private static int[] ConvexHullFanTriangulate(List<ProjectedEdgeVertex> points)
    {
        var hull = BuildConvexHull(points);
        if (hull.Count < 3)
        {
            return null;
        }

        var triangles = new List<int>((hull.Count - 2) * 6);
        var origin = hull[0];
        for (int i = 1; i < hull.Count - 1; i++)
        {
            var b = hull[i];
            var c = hull[i + 1];
            if (Mathf.Abs(Cross(origin.PlanePosition, b.PlanePosition, c.PlanePosition)) <= PreviewFanTriangleAreaEpsilon)
            {
                continue;
            }

            triangles.Add(origin.VertexIndex);
            triangles.Add(b.VertexIndex);
            triangles.Add(c.VertexIndex);
            triangles.Add(c.VertexIndex);
            triangles.Add(b.VertexIndex);
            triangles.Add(origin.VertexIndex);
        }

        return triangles.Count >= 3 ? triangles.ToArray() : null;
    }

    private static List<ProjectedEdgeVertex> BuildConvexHull(List<ProjectedEdgeVertex> points)
    {
        var sorted = DeduplicateProjectedVertices(points)
            .Where(p => IsFinite(p.PlanePosition))
            .OrderBy(p => p.PlanePosition.x)
            .ThenBy(p => p.PlanePosition.y)
            .ToList();
        if (sorted.Count < 3)
        {
            return sorted;
        }

        var lower = new List<ProjectedEdgeVertex>();
        for (int i = 0; i < sorted.Count; i++)
        {
            AddHullPoint(lower, sorted[i]);
        }

        var upper = new List<ProjectedEdgeVertex>();
        for (int i = sorted.Count - 1; i >= 0; i--)
        {
            AddHullPoint(upper, sorted[i]);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
    }

    private static void AddHullPoint(List<ProjectedEdgeVertex> hull, ProjectedEdgeVertex point)
    {
        while (hull.Count >= 2)
        {
            var a = hull[hull.Count - 2];
            var b = hull[hull.Count - 1];
            if (Cross(a.PlanePosition, b.PlanePosition, point.PlanePosition) > PreviewFanTriangleAreaEpsilon)
            {
                break;
            }

            hull.RemoveAt(hull.Count - 1);
        }

        hull.Add(point);
    }

    private static List<ProjectedEdgeVertex> DeduplicateProjectedVertices(List<ProjectedEdgeVertex> points)
    {
        if (points == null || points.Count < 2)
        {
            return points ?? new List<ProjectedEdgeVertex>();
        }

        var unique = new List<ProjectedEdgeVertex>(points.Count);
        var occupiedCells = new Dictionary<Vector2Int, ProjectedEdgeVertex>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            var candidate = points[i];
            if (!IsFinite(candidate.PlanePosition))
            {
                continue;
            }

            Vector2Int cell = new Vector2Int(
                Mathf.RoundToInt(candidate.PlanePosition.x / ProjectedVertexMergeEpsilon),
                Mathf.RoundToInt(candidate.PlanePosition.y / ProjectedVertexMergeEpsilon));
            if (occupiedCells.ContainsKey(cell))
            {
                continue;
            }

            occupiedCells[cell] = candidate;
            unique.Add(candidate);
        }

        return unique;
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    private static bool IsFinite(Vector2 value)
    {
        return IsFinite(value.x) && IsFinite(value.y);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static void AddSubMeshWithUV(Mesh mesh, int[] tris, Dictionary<int, Vector2> vertexUVMap)
    {
        if (mesh == null || tris == null)
        {
            return;
        }

        int subMeshIndex = mesh.subMeshCount;
        mesh.subMeshCount += 1;
        mesh.SetIndices(tris, MeshTopology.Triangles, subMeshIndex);

        if (vertexUVMap == null || vertexUVMap.Count == 0)
        {
            return;
        }

        Vector2[] existingUVs = mesh.uv;
        if (existingUVs == null || existingUVs.Length != mesh.vertexCount)
        {
            existingUVs = new Vector2[mesh.vertexCount];
        }

        foreach (var kvp in vertexUVMap)
        {
            if (kvp.Key >= 0 && kvp.Key < existingUVs.Length)
            {
                existingUVs[kvp.Key] = kvp.Value;
            }
        }

        mesh.uv = existingUVs;
    }

    private static Material[] BuildMaterials(Material[] sourceMaterials, Material capMaterial, bool hasMesh)
    {
        sourceMaterials = sourceMaterials ?? new Material[0];

        if (!hasMesh)
        {
            return sourceMaterials;
        }

        if (capMaterial == null)
        {
            return sourceMaterials;
        }

        var materials = new Material[sourceMaterials.Length + 1];
        for (int i = 0; i < sourceMaterials.Length; i++)
        {
            materials[i] = sourceMaterials[i];
        }

        materials[sourceMaterials.Length] = capMaterial;
        return materials;
    }

    public class PreviewMeshData
    {
        public SkinnedMeshRenderer SourceRenderer;
        public Mesh AttachedSideMesh;
        public Mesh DetachedSideMesh;
        public Material[] AttachedMaterials;
        public Material[] DetachedMaterials;
    }

    private class ProjectedEdgeVertex
    {
        public int VertexIndex;
        public Vector2 PlanePosition;
    }
}
