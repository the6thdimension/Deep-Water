using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

[ExecuteInEditMode]
public class PointCacheBaker : MonoBehaviour
{
    public GameObject MeshToAttach;
    private VisualEffect vfx;
    private Texture2D pointCache;
    private float size;
    private float yPos;

#if UNITY_EDITOR
    private void OnValidate ()
    {
        Awake ();
    }
#endif

    void Awake ()
    {
        vfx = GetComponent<VisualEffect> ();
        if (MeshToAttach.GetComponentInChildren<SkinnedMeshRenderer> ()) { }
        else
        {
            UpdateSize (MeshToAttach);
            UpdateCachePoint (MeshToAttach);
        }

        vfx.SetTexture ("PointCache", pointCache);
        vfx.SetFloat ("TotalSize", size);
        vfx.SetFloat ("PivotOffset", yPos);
    }

    void LateUpdate ()
    {
        if (MeshToAttach)
        {
            if (MeshToAttach.GetComponentInChildren<SkinnedMeshRenderer> ())
            {
                UpdateSize (MeshToAttach, true);
                UpdateCachePoint (MeshToAttach, true);
                vfx.SetTexture ("PointCache", pointCache);
                vfx.SetFloat ("TotalSize", size);
                vfx.SetFloat ("PivotOffset", yPos);
            }
            if (MeshToAttach.activeSelf)
                vfx.Play ();
            else
                vfx.Stop ();
            transform.position = MeshToAttach.transform.position;
            transform.rotation = MeshToAttach.transform.rotation;
            transform.localScale = MeshToAttach.transform.localScale;
        }
    }

    void UpdateSize (GameObject character, bool useSkinned = false)
    {
        if (useSkinned)
        {
            SkinnedMeshRenderer[] renderers = character.GetComponentsInChildren<SkinnedMeshRenderer> ();
            Bounds bound = new Bounds ();

            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                Mesh baked = new Mesh ();
                renderer.BakeMesh (baked);
                bound.Encapsulate (baked.bounds);
            }
            size = Mathf.Max (bound.extents.x * 2, bound.extents.y * 2, bound.extents.z * 2);
            yPos = bound.extents.y * 2;
        }
        else
        {
            MeshFilter[] renderers = character.GetComponentsInChildren<MeshFilter> ();
            Bounds bound = new Bounds ();

            foreach (MeshFilter renderer in renderers)
            {
                Mesh baked = renderer.sharedMesh;
                bound.Encapsulate (baked.bounds);
            }
            size = Mathf.Max (bound.extents.x * 2, bound.extents.y * 2, bound.extents.z * 2);
            yPos = bound.extents.y * 2;
        }
    }

    void UpdateCachePoint (GameObject character, bool useSkinned = false)
    {
        Mesh baked;
        Vector3[] vertices;
        Transform parent;
        List<Color> normalizedVertices = new List<Color> ();
        if (useSkinned)
        {
            SkinnedMeshRenderer[] renderers = character.GetComponentsInChildren<SkinnedMeshRenderer> ();
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                parent = renderer.gameObject.transform.parent;
                baked = new Mesh ();
                renderer.BakeMesh (baked);
                vertices = baked.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = (character.gameObject.transform.InverseTransformPoint (renderer.gameObject.transform.TransformPoint (vertices[i])) + new Vector3 (size * 0.5f, 0, size * 0.5f)) / size;
                    normalizedVertices.Add (new Color (vertices[i].x, vertices[i].y, vertices[i].z));
                }
            }
        }
        else
        {
            MeshFilter[] renderers = character.GetComponentsInChildren<MeshFilter> ();
            foreach (MeshFilter renderer in renderers)
            {
                parent = renderer.gameObject.transform.parent;
                baked = renderer.sharedMesh;
                vertices = baked.vertices;
                int increaser = (int) Mathf.Floor ((vertices.Length / 3000) / 2);
                if (increaser == 0) increaser = 1;
                for (int i = 0; i < vertices.Length; i += increaser)
                {
                    vertices[i] = (character.gameObject.transform.InverseTransformPoint (renderer.gameObject.transform.TransformPoint (vertices[i])) + new Vector3 (size * 0.5f, yPos, size * 0.5f)) / size;
                    normalizedVertices.Add (new Color (vertices[i].x, vertices[i].y, vertices[i].z));
                }
            }
        }
        if (pointCache == null || pointCache.width != normalizedVertices.Count)
        {
            pointCache = new Texture2D (1, normalizedVertices.Count, TextureFormat.RGBA32, false, true);
            pointCache.filterMode = FilterMode.Point;
        }
        pointCache.SetPixels (normalizedVertices.ToArray ());
        pointCache.Apply ();
    }
}