using UnityEngine;
using UnityEngine.Rendering;

namespace WhackySlappy.Core
{
    /// <summary>
    /// A large inverted sphere mesh that covers the plain sky clear colour
    /// with a vertical gradient (fog colour at the horizon, deeper blue at
    /// the zenith), removing the hard sky/fog seam. The gradient is driven
    /// through the Standard shader's emission map (lit by nothing else), so
    /// sun, ambient and fog never alter the sky colours; fog still fades
    /// real geometry into the gradient bottom, which is exactly the fog
    /// colour. No skybox material, no custom shader: the mesh and texture
    /// are built in code. The dome follows Camera.main so it is never
    /// crossed by the far plane.
    /// </summary>
    public class SkyDome : MonoBehaviour
    {
        // Rows in the 1x64 vertical gradient; the flat fog-coloured bottom
        // half hides the far ground, the top half ramps to zenith blue.
        private const int GradientRows = 64;

        private void LateUpdate()
        {
            // Camera only moves in Update, so following it after that is
            // enough to keep the dome centred before the frame renders.
            var cam = Camera.main;
            if (cam != null)
                transform.position = cam.transform.position;
        }

        /// <summary>
        /// Builds the dome under parent (idempotent: skips if one already
        /// exists). horizonColor must equal RenderSettings.fogColor so the
        /// gradient bottom and the fog blend into each other.
        /// </summary>
        public static GameObject Create(
            GameObject parent, Color horizonColor, Color zenithColor, float radius)
        {
            if (parent == null)
                return null;
            if (parent.transform.Find("SkyDome") != null)
                return null;

            var go = new GameObject("SkyDome");
            go.transform.SetParent(parent.transform, false);
            var startCam = Camera.main;
            go.transform.position = startCam != null
                ? startCam.transform.position : Vector3.zero;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildInvertedSphere(radius);

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var mat = DefaultLitMaterial.Create();
            if (mat != null)
            {
                // Everything visible comes from the gradient texture: the
                // base colour is black and the _EMISSION keyword is forced
                // on (the factory copy may not carry one). No GI flags, so
                // the dome never bakes or lights anything else.
                mat.color = Color.black;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.white);
                mat.SetTexture("_EmissionMap", MakeGradient(horizonColor, zenithColor));
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                renderer.material = mat;
            }

            // No collider: the dome is visual only.
            go.AddComponent<SkyDome>();
            return go;
        }

        /// <summary>
        /// Vertical gradient strip (1 wide x GradientRows tall). v = 0 sits
        /// at the dome's south pole, v = 1 at the zenith; the horizon line
        /// is v = 0.5, so the lower half stays flat fog colour (hidden by
        /// the meadow anyway) and the visible band ramps to zenith blue.
        /// </summary>
        private static Texture2D MakeGradient(Color horizonColor, Color zenithColor)
        {
            var rows = new Color[GradientRows];
            for (int i = 0; i < GradientRows; i++)
            {
                float v = i / (float)(GradientRows - 1);
                float t = Mathf.InverseLerp(0.5f, 1f, v);
                rows[i] = Color.Lerp(horizonColor, zenithColor, t);
            }

            var tex = new Texture2D(1, GradientRows);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels(rows);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Procedural UV sphere, triangles wound INWARD so the inside of
        /// the shell is the front face (Unity front faces use the right-
        /// hand rule; with latitude rows running north to south, winding
        /// each quad (a, b, a+1) / (a+1, b, b+1) where b is the row south
        /// of a points normals at the sphere centre). UV v follows latitude
        /// so the gradient strip maps top-to-bottom.
        /// </summary>
        private static Mesh BuildInvertedSphere(float radius)
        {
            const int Rings = 16;
            const int Segments = 32;

            var verts = new Vector3[(Rings + 1) * (Segments + 1)];
            var uvs = new Vector2[(Rings + 1) * (Segments + 1)];
            for (int i = 0; i <= Rings; i++)
            {
                float theta = Mathf.PI * (i / (float)Rings); // 0 at north pole
                float ringR = radius * Mathf.Sin(theta);
                float y = radius * Mathf.Cos(theta);
                for (int j = 0; j <= Segments; j++)
                {
                    float phi = 2f * Mathf.PI * (j / (float)Segments);
                    int k = i * (Segments + 1) + j;
                    verts[k] = new Vector3(
                        ringR * Mathf.Cos(phi), y, ringR * Mathf.Sin(phi));
                    uvs[k] = new Vector2(j / (float)Segments, 1f - i / (float)Rings);
                }
            }

            var tris = new int[Rings * Segments * 6];
            int t = 0;
            for (int i = 0; i < Rings; i++)
            {
                for (int j = 0; j < Segments; j++)
                {
                    int a = i * (Segments + 1) + j;
                    int b = a + Segments + 1;
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
                }
            }

            var mesh = new Mesh();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
