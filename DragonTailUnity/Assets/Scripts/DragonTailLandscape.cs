using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DragonTail
{
    /// <summary>Procedural scenery only. The authored game sequence owns every drone and objective.</summary>
    public sealed class DragonTailLandscape : MonoBehaviour
    {
        public const float Width = 128f;
        public const float Depth = 80f;
        public const float TerrainReliefMax = 14f;

        private readonly List<Material> materials = new List<Material>();
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly Dictionary<Material, List<CombineInstance>> batches = new Dictionary<Material, List<CombineInstance>>();
        private Transform sceneryRoot;
        private Mesh cube, sphere, coarseRock, cylinder, roof;
        private Material earth, asphalt, gravel, paint, water, streamBed, bark, timber, wire, plaster, plasterWarm, roofSlate, roofRust, window;
        private Material[] foliage, rocks;
        private Texture2D grain;
        private System.Random random;
        private bool built;

        /// <summary>Authored ground: a clear camp, a fly-over hill, and level objective sites.</summary>
        public static float GroundHeight(float x, float z)
        {
            float nx = x * .5f, nz = z * .5f;
            float corridor = Smooth(4.8f, 8.4f, Mathf.Abs(nz));
            float objectiveClearance = Smooth(35f, 38f, x) * (1f - Smooth(53f, 57f, x)) *
                (1f - Smooth(18f, 23f, Mathf.Abs(z)));
            float broad = Mathf.PerlinNoise((nx + 70f) * .052f, (nz + 90f) * .084f);
            float detail = Mathf.PerlinNoise((nx + 150f) * .15f, (nz + 200f) * .19f);
            float ridgeZ = nz >= 0f ? 15.3f + 1.4f * Mathf.Sin(nx * .105f) : -16.8f + .8f * Mathf.Sin(nx * .19f);
            float ridge = Mathf.Exp(-Mathf.Pow((nz - ridgeZ) / 4.5f, 2f));
            float warp = Mathf.PerlinNoise((nx + 310f) * .083f, (nz + 40f) * .11f) * 4f;
            float drainageNoise = Mathf.PerlinNoise((nx + warp + 61f) * .19f, (nz + 83f) * .125f);
            float gully = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(drainageNoise - .5f) * 5f), 4f) *
                (.3f + .7f * Mathf.PerlinNoise((nx + 51f) * .07f, (nz + 32f) * .1f));
            float knolls = Mathf.PerlinNoise((nx + 14f) * .24f, (nz + 18f) * .21f) - .5f;
            float height = (.45f + broad * 2.3f + detail * .6f + ridge * (2.1f + broad * 1.8f) +
                knolls * .6f - gully * ridge * .42f) * corridor * 2.25f;
            float creekDistance = Mathf.Abs(z - StreamZ(x));
            float channel = 1f - Smooth(.7f, 3.1f, creekDistance);
            height = Mathf.Max(0f, height * (1f - channel * .65f) - channel * .08f);
            float hill = 7.35f * Mathf.Exp(-Mathf.Pow((x + 33.5f) / 9.2f, 2f) - Mathf.Pow((z - .7f) / 11.5f, 2f));
            height += hill;
            height = Mathf.Lerp(Mathf.Min(height, 7.5f), height, Smooth(3f, 7f, Mathf.Abs(z)));
            float campDistance = Mathf.Sqrt((x + 48f) * (x + 48f) + z * z);
            float campClearance = Smooth(7f, 12.5f, campDistance);
            return Mathf.Clamp(height * campClearance * (1f - objectiveClearance), 0f, TerrainReliefMax);
        }

        private static float StreamZ(float x)
        {
            return -24.6f + 1.7f * Mathf.Sin(x * .0825f) + .52f * Mathf.Sin(x * .245f);
        }

        private static float Smooth(float from, float to, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));
        }

        public void Build()
        {
            if (built) return;
            built = true;
            random = new System.Random(7241);
            sceneryRoot = new GameObject("Valley landscape / scenery only").transform;
            sceneryRoot.SetParent(transform, false);
            CreateMaterials();
            cube = CreateCube();
            sphere = CreateIcosphere(2, .16f, "Rounded leafy crown", true);
            coarseRock = CreateIcosphere(1, .34f, "Weathered fractured stone");
            cylinder = CreateCylinder(10);
            roof = CreateRoof();
            CreateTerrain();
            CreateRoadsAndStream();
            CreateForest();
            CreateRockOutcrops();
            CreateFarmsteads();
            CreateFencesAndUtilities();
            CompleteBatches();
        }

        private Material MakeMaterial(string name, string hex, float smoothness = .1f, bool withGrain = true)
        {
            var material = new Material(Shader.Find("Standard")) { name = name };
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            material.color = color;
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", 0f);
            if (withGrain)
            {
                material.mainTexture = grain;
                material.mainTextureScale = new Vector2(3f, 3f);
            }
            materials.Add(material);
            return material;
        }

        private void CreateMaterials()
        {
            grain = new Texture2D(128, 128, TextureFormat.RGB24, true, true) { name = "Fine surface grain", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float n = Mathf.PerlinNoise(x * .16f, y * .16f) * .12f + Hash(x, y) * .09f;
                float value = .82f + n;
                pixels[y * 128 + x] = new Color(value, value, value);
            }
            grain.SetPixels(pixels); grain.Apply(); textures.Add(grain);
            earth = MakeMaterial("Exposed earth", "554A39");
            asphalt = MakeMaterial("Weathered pavement", "555952", .14f);
            gravel = MakeMaterial("Gravel shoulders", "7D8070");
            paint = MakeMaterial("Faded road paint", "ADAC98", .05f);
            water = MakeMaterial("Shallow drainage water", "466F65", .72f, false);
            water.SetFloat("_Metallic", .12f);
            streamBed = MakeMaterial("Silt and creek stones", "746F56");
            bark = MakeMaterial("Tree bark", "534C3B");
            timber = MakeMaterial("Weathered fence timber", "7C7965");
            wire = MakeMaterial("Utility lines", "3D4541", .3f);
            plaster = MakeMaterial("Farm plaster", "AAA99C");
            plasterWarm = MakeMaterial("Ochre plaster", "958A73");
            roofSlate = MakeMaterial("Slate roof", "4A5657", .13f);
            roofRust = MakeMaterial("Oxide roof", "715345", .12f);
            window = MakeMaterial("Dark window glass", "364D50", .48f, false);
            foliage = new[]
            {
                MakeMaterial("Foliage oak", "426D37"), MakeMaterial("Foliage deep", "2F5735"),
                MakeMaterial("Foliage young", "527C3E"), MakeMaterial("Foliage moss", "3B6738"),
                MakeMaterial("Foliage ash", "58754A")
            };
            rocks = new[]
            {
                MakeMaterial("Stone slate", "70796D"), MakeMaterial("Stone pale", "8A9281"),
                MakeMaterial("Stone shaded", "596659")
            };
        }

        private void CreateTerrain()
        {
            const int columns = 256, rows = 160;
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[columns * rows * 6];
            for (int z = 0; z <= rows; z++) for (int x = 0; x <= columns; x++)
            {
                float wx = -Width * .5f + Width * x / columns;
                float wz = -Depth * .5f + Depth * z / rows;
                int index = z * (columns + 1) + x;
                vertices[index] = new Vector3(wx, GroundHeight(wx, wz), wz);
                uvs[index] = new Vector2(x / (float)columns, z / (float)rows);
                if (x == columns || z == rows) continue;
                int t = (z * columns + x) * 6;
                triangles[t] = index; triangles[t + 1] = index + columns + 1; triangles[t + 2] = index + 1;
                triangles[t + 3] = index + 1; triangles[t + 4] = index + columns + 1; triangles[t + 5] = index + columns + 2;
            }
            Mesh terrainMesh = OwnMesh("Rolling valley terrain", vertices, triangles, uvs);
            Material terrain = MakeMaterial("Valley grass, soil and rock", "FFFFFF", .06f, false);
            var albedo = new Texture2D(768, 480, TextureFormat.RGB24, true) { name = "Valley ground albedo", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            var pixels = new Color[768 * 480];
            Color olive = new Color(.19f, .34f, .15f), dry = new Color(.31f, .38f, .21f), stone = new Color(.34f, .38f, .31f);
            for (int py = 0; py < 480; py++) for (int px = 0; px < 768; px++)
            {
                float x = -Width * .5f + px / 767f * Width, z = -Depth * .5f + py / 479f * Depth;
                float broad = Mathf.PerlinNoise((x + 100f) * .13f, (z + 100f) * .16f);
                float fine = Mathf.PerlinNoise((x + 150f) * .7f, (z + 150f) * .7f);
                float h = GroundHeight(x, z);
                float slope = Mathf.Abs(GroundHeight(x + .25f, z) - GroundHeight(x - .25f, z)) +
                    Mathf.Abs(GroundHeight(x, z + .25f) - GroundHeight(x, z - .25f));
                Color color = Color.Lerp(olive, dry, Mathf.Clamp01(broad * .62f + fine * .2f));
                float rockBand = Smooth(.68f, .96f, .5f + .5f * Mathf.Sin(h * 5f + x * .14f + fine * 1.4f));
                float exposure = Mathf.Clamp01(slope * .42f + Smooth(7f, 13f, h) * .32f + rockBand * Smooth(.16f, .65f, slope) * .32f);
                color = Color.Lerp(color, stone, exposure);
                float channel = 1f - Smooth(.35f, 1.3f, Mathf.Abs(z - StreamZ(x)));
                color = Color.Lerp(color, new Color(.19f, .28f, .18f), channel * .55f);
                float blades = Mathf.PerlinNoise((x + 84f) * 11f, (z + 94f) * 3.3f);
                color *= .84f + fine * .12f + blades * .1f + Hash(px, py) * .07f;
                pixels[py * 768 + px] = color;
            }
            albedo.SetPixels(pixels); albedo.Apply(); textures.Add(albedo);
            terrain.mainTexture = albedo;
            DisplayMesh("Continuous valley terrain", terrainMesh, terrain);
            CreateContours(vertices, triangles);

            // A soil cut at the outside boundary replaces the old board, grid and decorative rim.
            var perimeter = new List<Vector3>();
            for (int x = 0; x <= columns; x++) perimeter.Add(vertices[x]);
            for (int z = 1; z <= rows; z++) perimeter.Add(vertices[z * (columns + 1) + columns]);
            for (int x = columns - 1; x >= 0; x--) perimeter.Add(vertices[rows * (columns + 1) + x]);
            for (int z = rows - 1; z >= 1; z--) perimeter.Add(vertices[z * (columns + 1)]);
            var sideVertices = new List<Vector3>(); var sideTriangles = new List<int>();
            for (int i = 0; i < perimeter.Count; i++)
            {
                Vector3 a = perimeter[i], b = perimeter[(i + 1) % perimeter.Count];
                int start = sideVertices.Count;
                sideVertices.Add(a); sideVertices.Add(b);
                sideVertices.Add(new Vector3(a.x, -2.2f, a.z)); sideVertices.Add(new Vector3(b.x, -2.2f, b.z));
                AddQuad(sideTriangles, start, start + 1, start + 2, start + 3);
            }
            Mesh sides = OwnMesh("Terrain edge soil", sideVertices.ToArray(), sideTriangles.ToArray());
            DisplayMesh("Natural terrain edge", sides, earth);
        }

        private void CreateContours(Vector3[] groundVertices, int[] groundTriangles)
        {
            Material minor = MakeMaterial("Minor elevation contours", "798A65", 0f, false);
            Material major = MakeMaterial("Major elevation contours", "A5AC86", 0f, false);
            minor.shader = Shader.Find("Unlit/Color"); major.shader = Shader.Find("Unlit/Color");
            var minorVertices = new List<Vector3>(); var minorTriangles = new List<int>();
            var majorVertices = new List<Vector3>(); var majorTriangles = new List<int>();
            // Intersect the actual rendered triangles at every two units of elevation.
            // This is terrain annotation only; it has no effect on game routing or visibility.
            for (int triangle = 0; triangle < groundTriangles.Length; triangle += 3)
            {
                Vector3 a = groundVertices[groundTriangles[triangle]], b = groundVertices[groundTriangles[triangle + 1]], c = groundVertices[groundTriangles[triangle + 2]];
                float low = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), high = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                int first = Mathf.Max(1, Mathf.CeilToInt(low * .5f));
                int last = Mathf.FloorToInt(high * .5f);
                for (int index = first; index <= last; index++)
                {
                    float elevation = index * 2f;
                    Vector3 start = Vector3.zero, end = Vector3.zero;
                    int hits = 0;
                    if (ContourCrossing(a, b, elevation, out Vector3 point)) { start = point; hits++; }
                    if (ContourCrossing(b, c, elevation, out point)) { if (hits == 0) start = point; else end = point; hits++; }
                    if (ContourCrossing(c, a, elevation, out point)) { if (hits == 0) start = point; else end = point; hits++; }
                    if (hits < 2 || (end - start).sqrMagnitude < .00001f) continue;
                    bool stronger = index % 2 == 0;
                    AddContourSegment(start, end, stronger ? .13f : .075f,
                        stronger ? majorVertices : minorVertices, stronger ? majorTriangles : minorTriangles);
                }
            }
            DisplayMesh("Elevation contours / interval 2", OwnMesh("Minor contour geometry", minorVertices.ToArray(), minorTriangles.ToArray()), minor, false);
            DisplayMesh("Elevation contours / major interval 4", OwnMesh("Major contour geometry", majorVertices.ToArray(), majorTriangles.ToArray()), major, false);
        }

        private static bool ContourCrossing(Vector3 a, Vector3 b, float elevation, out Vector3 point)
        {
            point = Vector3.zero;
            if (!((a.y <= elevation && b.y > elevation) || (b.y <= elevation && a.y > elevation))) return false;
            point = Vector3.Lerp(a, b, (elevation - a.y) / (b.y - a.y));
            return true;
        }

        private static void AddContourSegment(Vector3 start, Vector3 end, float width, List<Vector3> vertices, List<int> triangles)
        {
            Vector3 direction = end - start;
            Vector3 side = new Vector3(-direction.z, 0, direction.x).normalized * width * .5f;
            int first = vertices.Count;
            Vector3[] corners = { start - side, start + side, end - side, end + side };
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 p = corners[i]; p.y = GroundHeight(p.x, p.z) + .105f; vertices.Add(p);
            }
            AddQuad(triangles, first, first + 1, first + 2, first + 3);
        }

        private void CreateRoadsAndStream()
        {
            // Rural lanes stay inside remote hamlets; none connect the camp or objective sites.
            for (int hamlet = 0; hamlet < 3; hamlet++)
            {
                float left = hamlet == 0 ? -58f : hamlet == 1 ? -23f : -53f;
                float right = hamlet == 0 ? -38f : hamlet == 1 ? -1f : -27f;
                float centerZ = hamlet == 0 ? 16.1f : hamlet == 1 ? 18.3f : -17.3f;
                var lane = new List<Vector3>();
                for (int i = 0; i <= 64; i++)
                {
                    float x = Mathf.Lerp(left, right, i / 64f);
                    float z = centerZ + .7f * Mathf.Sin(x * .26f + hamlet);
                    lane.Add(new Vector3(x, 0, z));
                }
                AddStrip(lane, 1.12f, .055f, gravel, "Isolated hamlet lane");
            }
            var stream = new List<Vector3>();
            for (int i = 0; i <= 342; i++)
            {
                float x = -Width * .5f + Width * i / 342f;
                stream.Add(new Vector3(x, 0, StreamZ(x)));
            }
            AddStrip(OffsetPath(stream, -.86f), .56f, .05f, streamBed, "Near drainage bank");
            AddStrip(OffsetPath(stream, .86f), .56f, .05f, streamBed, "Far drainage bank");
            AddStrip(stream, 1.23f, .08f, water, "Winding shallow stream");
            var bridgeLane = new List<Vector3>();
            for (int i = 0; i <= 45; i++)
            {
                float z = Mathf.Lerp(-29.7f, -17.4f, i / 45f);
                bridgeLane.Add(new Vector3(-36f + .25f * Mathf.Sin(z * .35f), 0, z));
            }
            AddStrip(bridgeLane, 1.15f, .055f, gravel, "Rural creek crossing");
            CreateBridge(-36f, StreamZ(-36f));
        }

        private void CreateBridge(float x, float z)
        {
            float deckY = GroundHeight(x, z) + .65f;
            Add(cube, new Vector3(x, deckY, z), new Vector3(1.68f, .22f, 3.25f), Quaternion.identity, timber);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int end = -1; end <= 1; end += 2)
                    Add(cube, new Vector3(x + side * .83f, deckY + .21f, z + end * 1.35f), new Vector3(.12f, 1.05f, .12f), Quaternion.identity, timber);
                Add(cube, new Vector3(x + side * .83f, deckY + .64f, z), new Vector3(.09f, .1f, 3.35f), Quaternion.identity, timber);
            }
            for (int i = -5; i <= 5; i++)
                Add(cube, new Vector3(x, deckY + .12f, z + i * .285f), new Vector3(1.65f, .025f, .035f), Quaternion.identity, earth);
        }

        private void CreateForest()
        {
            Vector2[] clusters =
            {
                new Vector2(-54, 26), new Vector2(-34, 30), new Vector2(-14, 26), new Vector2(8, 30), new Vector2(52, 30),
                new Vector2(-56, -32), new Vector2(-28, -32), new Vector2(-8, -34), new Vector2(14, -32), new Vector2(54, -32),
                new Vector2(-39, 12), new Vector2(-27, -10), new Vector2(22, 24), new Vector2(-59, 3)
            };
            for (int c = 0; c < clusters.Length; c++) for (int i = 0; i < 11; i++)
            {
                float angle = Range(0, Mathf.PI * 2), distance = Mathf.Sqrt(Range(0, 1)) * 4.9f;
                float x = Mathf.Clamp(clusters[c].x + Mathf.Cos(angle) * distance, -61.5f, 61.5f);
                float z = Mathf.Clamp(clusters[c].y + Mathf.Sin(angle) * distance * .75f, -37f, 37f);
                if (x > 35 && x < 57 && Mathf.Abs(z) < 22f) continue;
                if ((new Vector2(x + 48f, z)).sqrMagnitude < 90f || Mathf.Abs(z) < 6.3f) continue;
                if (Mathf.Abs(z - StreamZ(x)) < 2f) z -= 2.5f;
                float y = GroundHeight(x, z), height = Range(2.3f, 3.9f), width = Range(1.4f, 2.25f);
                Add(cylinder, new Vector3(x, y + height * .3f, z), new Vector3(.16f, height * .65f, .16f), Quaternion.Euler(Range(-4, 4), 0, Range(-4, 4)), bark);
                int crownCount = i % 3 == 0 ? 5 : 4;
                for (int branch = 0; branch < crownCount; branch++)
                {
                    float azimuth = branch * Mathf.PI * 2f / crownCount + Range(-.3f, .3f);
                    Vector3 center = new Vector3(x + Mathf.Cos(azimuth) * width * .23f, y + height * (.6f + branch * .053f), z + Mathf.Sin(azimuth) * width * .24f);
                    Beam(new Vector3(x, y + height * .42f, z), center, .055f, bark);
                    Add(sphere, center, new Vector3(width * Range(.74f, 1.04f), height * Range(.37f, .53f), width * Range(.74f, 1.04f)),
                        Quaternion.Euler(Range(-17, 17), Range(0, 360), Range(-17, 17)), foliage[(c + i + (branch == 3 ? 1 : 0)) % foliage.Length]);
                }
            }
            for (int i = 0; i < 60; i++)
            {
                float x = Range(-59, 59), z = (i % 2 == 0 ? 1 : -1) * Range(12, 22);
                if (x > 34 || (x < -38 && z < -13 && z > -22)) continue;
                float y = GroundHeight(x, z), size = Range(.4f, .85f);
                Add(sphere, new Vector3(x, y + size * .3f, z), new Vector3(size, size * .55f, size * .8f), Quaternion.Euler(0, Range(0, 360), 0), foliage[i % foliage.Length]);
            }
        }

        private void CreateRockOutcrops()
        {
            // Low, overlapping weathered shelves follow the outer ridge contours.
            for (int band = 0; band < 7; band++)
            {
                float centerX = -54f + band * 16.8f;
                float centerZ = band % 2 == 0 ? 29f : -34.6f;
                for (int layer = 0; layer < 3; layer++) for (int slab = 0; slab < 4; slab++)
                {
                    float x = centerX + (slab - 1.5f) * .86f + Range(-.2f, .2f);
                    float z = centerZ + layer * .52f + .22f * Mathf.Sin(x * .7f);
                    float ground = GroundHeight(x, z);
                    Add(coarseRock, new Vector3(x, ground + .06f, z), new Vector3(Range(1.1f, 1.8f), .35f + layer * .07f, .8f),
                        Quaternion.Euler(8, Range(-16, 16), Range(-8, 8)), rocks[(layer + band) % rocks.Length]);
                }
            }
            for (int i = 0; i < 145; i++)
            {
                float x = Range(-61f, 61f), z = (i % 2 == 0 ? 1 : -1) * Range(20, 37.6f);
                if (x > 35 && x < 57 && Mathf.Abs(z) < 22f) continue;
                if (Mathf.Abs(z - StreamZ(x)) < 1.25f) continue;
                float size = Range(.55f, 1.8f), y = GroundHeight(x, z);
                Add(coarseRock, new Vector3(x, y + size * .12f, z), new Vector3(size, size * Range(.35f, .72f), size * Range(.7f, 1.3f)),
                    Quaternion.Euler(Range(-17, 17), Range(0, 360), Range(-14, 14)), rocks[i % rocks.Length]);
                if (i % 5 == 0)
                {
                    Add(cube, new Vector3(x + .5f, y - .05f, z + .4f), new Vector3(size * 2.1f, .24f, size * .85f),
                        Quaternion.Euler(8, Range(0, 180), 7), rocks[(i + 1) % rocks.Length]);
                }
            }
            for (int i = 0; i < 65; i++)
            {
                float x = Range(-61, 61), z = StreamZ(x) + (i % 2 == 0 ? -.89f : .89f);
                float size = Range(.16f, .43f);
                Add(coarseRock, new Vector3(x, GroundHeight(x, z) + .055f, z), new Vector3(size, size * .55f, size * .8f),
                    Quaternion.Euler(0, Range(0, 360), 0), rocks[i % rocks.Length]);
            }
        }

        private void CreateFarmsteads()
        {
            Vector2[] sites =
            {
                new Vector2(-28, 7.1f), new Vector2(-26, 10), new Vector2(-22.4f, 8.3f), new Vector2(-20.2f, 10.3f),
                new Vector2(-10.2f, 8.4f), new Vector2(-7.2f, 9.2f), new Vector2(-3.9f, 8.3f), new Vector2(-1.4f, 10.4f),
                new Vector2(-24.5f, -8.7f), new Vector2(-21.4f, -9.2f), new Vector2(-16.2f, -9.1f), new Vector2(-14f, -7.5f)
            };
            for (int i = 0; i < sites.Length; i++)
            {
                Vector2 site = sites[i] * 2f;
                Vector3 origin = new Vector3(site.x, GroundHeight(site.x, site.y), site.y);
                Quaternion rotation = Quaternion.Euler(0, Range(-18, 18), 0);
                float width = Range(1.55f, 2.25f), depth = Range(1.2f, 1.75f), height = Range(.95f, 1.4f);
                AddLocal(cube, origin, rotation, new Vector3(0, -.06f, 0), new Vector3(width + .25f, .3f, depth + .25f), gravel);
                AddLocal(cube, origin, rotation, new Vector3(0, height * .5f, 0), new Vector3(width, height, depth), i % 3 == 0 ? plasterWarm : plaster);
                AddLocal(roof, origin, rotation, new Vector3(0, height, 0), new Vector3(width + .26f, .58f, depth + .3f), i % 3 == 0 ? roofRust : roofSlate);
                AddLocal(cube, origin, rotation, new Vector3(width * .27f, height + .43f, depth * .16f), new Vector3(.23f, .62f, .24f), plasterWarm);
                AddLocal(cube, origin, rotation, new Vector3(-width * .23f, height * .48f, -depth * .5f - .013f), new Vector3(.31f, height * .69f, .035f), timber);
                AddLocal(cube, origin, rotation, new Vector3(width * .22f, height * .61f, -depth * .5f - .024f), new Vector3(.38f, .35f, .045f), window);
                AddLocal(cube, origin, rotation, new Vector3(width * .5f + .02f, height * .61f, 0), new Vector3(.04f, .34f, .4f), window);
                AddVillageDetail(origin, rotation, width, depth, height, i);
                if (i % 3 == 0)
                    AddLocal(cube, origin, rotation, new Vector3(-width * .75f, .42f, .08f), new Vector3(width * .5f, .84f, depth * .78f), plasterWarm);
            }
        }

        private void AddVillageDetail(Vector3 origin, Quaternion rotation, float width, float depth, float height, int index)
        {
            float roofDepth = (depth + .3f) * .5f;
            Material roofMaterial = index % 3 == 0 ? roofRust : roofSlate;
            // Shingle courses, ridge cap and deep eaves remain visible from the overview.
            for (int row = -4; row <= 4; row++)
            {
                float z = row / 4f * roofDepth;
                float y = height + .58f * (1f - Mathf.Abs(z) / roofDepth) + .013f;
                AddLocal(cube, origin, rotation, new Vector3(0, y, z), new Vector3(width + .27f, .019f, .034f), wire);
            }
            AddLocal(cube, origin, rotation, new Vector3(0, height + .602f, 0), new Vector3(width + .32f, .07f, .095f), roofMaterial);
            for (int side = -1; side <= 1; side += 2)
            {
                AddLocal(cube, origin, rotation, new Vector3(0, height - .025f, side * roofDepth), new Vector3(width + .3f, .105f, .08f), timber);
                AddLocal(cylinder, origin, rotation, new Vector3(width * .48f, height * .5f, side * (depth * .5f + .08f)), new Vector3(.045f, height, .045f), roofSlate);
            }
            // Front glazing has a sill, surround and divided panes.
            float windowX = width * .22f, windowY = height * .61f, front = -depth * .5f - .05f;
            for (int side = -1; side <= 1; side += 2)
            {
                AddLocal(cube, origin, rotation, new Vector3(windowX + side * .21f, windowY, front), new Vector3(.05f, .43f, .08f), plaster);
                AddLocal(cube, origin, rotation, new Vector3(windowX, windowY + side * .2f, front), new Vector3(.47f, .055f, .09f), plaster);
            }
            AddLocal(cube, origin, rotation, new Vector3(windowX, windowY, front - .008f), new Vector3(.022f, .35f, .035f), timber);
            AddLocal(cube, origin, rotation, new Vector3(windowX, windowY, front - .01f), new Vector3(.38f, .025f, .04f), timber);
            AddLocal(cube, origin, rotation, new Vector3(windowX, windowY - .22f, front - .025f), new Vector3(.51f, .06f, .16f), gravel);
            float doorX = -width * .23f;
            AddLocal(cube, origin, rotation, new Vector3(doorX, height * .85f, front), new Vector3(.41f, .065f, .085f), plaster);
            for (int side = -1; side <= 1; side += 2)
                AddLocal(cube, origin, rotation, new Vector3(doorX + side * .18f, height * .49f, front), new Vector3(.045f, height * .72f, .07f), plaster);
            AddLocal(cube, origin, rotation, new Vector3(doorX, .065f, front - .19f), new Vector3(.57f, .13f, .46f), gravel);
            AddLocal(cube, origin, rotation, new Vector3(doorX + .1f, height * .48f, front - .015f), new Vector3(.04f, .045f, .035f), wire);
            // Chimney cap and masonry courses give the silhouette a finished edge.
            AddLocal(cube, origin, rotation, new Vector3(width * .27f, height + .765f, depth * .16f), new Vector3(.31f, .09f, .32f), roofSlate);
            for (int row = 0; row < 3; row++)
                AddLocal(cube, origin, rotation, new Vector3(width * .27f, height + .43f + row * .1f, depth * .16f), new Vector3(.238f, .018f, .247f), roofRust);
            if (index % 2 == 0)
            {
                AddLocal(cube, origin, rotation, new Vector3(doorX, height * .88f, front - .35f), new Vector3(.79f, .07f, .67f), roofMaterial);
                for (int side = -1; side <= 1; side += 2)
                    AddLocal(cube, origin, rotation, new Vector3(doorX + side * .34f, height * .44f, front - .6f), new Vector3(.06f, height * .88f, .06f), timber);
            }
            // Small yard objects are outside the story corridor and use the same batches.
            Vector3 barrelLocal = new Vector3(width * .67f, .26f, -depth * .34f);
            AddLocal(cylinder, origin, rotation, barrelLocal, new Vector3(.37f, .52f, .37f), roofSlate);
            AddLocal(cylinder, origin, rotation, barrelLocal + Vector3.up * .275f, new Vector3(.4f, .045f, .4f), wire);
            for (int stack = 0; stack < 3; stack++)
                AddLocal(cube, origin, rotation, new Vector3(-width * .68f, .105f + stack * .115f, -depth * .39f), new Vector3(.59f, .09f, .43f), timber);
        }

        private void CreateFencesAndUtilities()
        {
            AddFence(new Vector2(-59, 12.7f), new Vector2(-40, 13.4f), 13);
            AddFence(new Vector2(-23, 14.5f), new Vector2(-2, 14.8f), 14);
            AddFence(new Vector2(-53, -14.5f), new Vector2(-40, -14.8f), 9);
            AddFence(new Vector2(-31, -13.5f), new Vector2(-25, -13.9f), 5);
            Vector3 previous = Vector3.zero;
            for (int i = 0; i < 8; i++)
            {
                float x = -57 + i * 2.7f, z = 14.8f + .3f * Mathf.Sin(x * .21f);
                float ground = GroundHeight(x, z);
                Vector3 top = new Vector3(x, ground + 2.2f, z);
                Add(cylinder, new Vector3(x, ground + 1.1f, z), new Vector3(.095f, 2.2f, .095f), Quaternion.identity, timber);
                Add(cube, top, new Vector3(.12f, .11f, .9f), Quaternion.identity, timber);
                for (int side = -1; side <= 1; side += 2)
                {
                    Add(cylinder, top + new Vector3(0, .1f, side * .32f), new Vector3(.075f, .13f, .075f), Quaternion.identity, plaster);
                    if (i == 0) continue;
                    Vector3 last = previous + new Vector3(0, .16f, side * .32f);
                    for (int segment = 1; segment <= 10; segment++)
                    {
                        float t = segment / 10f;
                        Vector3 next = Vector3.Lerp(previous, top, t) + new Vector3(0, .16f - .24f * Mathf.Sin(t * Mathf.PI), side * .32f);
                        Beam(last, next, .018f, wire);
                        last = next;
                    }
                }
                previous = top;
            }
        }

        private void AddFence(Vector2 start, Vector2 end, int divisions)
        {
            Vector3 previous = Vector3.zero;
            for (int i = 0; i <= divisions; i++)
            {
                Vector2 point = Vector2.Lerp(start, end, i / (float)divisions);
                Vector3 p = new Vector3(point.x, GroundHeight(point.x, point.y), point.y);
                Add(cube, p + Vector3.up * .42f, new Vector3(.095f, .84f, .095f), Quaternion.Euler(0, 5, 0), timber);
                if (i > 0)
                {
                    Beam(previous + Vector3.up * .3f, p + Vector3.up * .3f, .055f, timber);
                    Beam(previous + Vector3.up * .66f, p + Vector3.up * .66f, .055f, timber);
                }
                previous = p;
            }
        }

        private static List<Vector3> OffsetPath(List<Vector3> path, float offset)
        {
            var result = new List<Vector3>(path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 tangent = path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(0, i - 1)];
                result.Add(path[i] + new Vector3(-tangent.z, 0, tangent.x).normalized * offset);
            }
            return result;
        }

        private void AddStrip(List<Vector3> path, float width, float lift, Material material, string name)
        {
            var vertices = new Vector3[path.Count * 2]; var uvs = new Vector2[vertices.Length]; var triangles = new int[(path.Count - 1) * 6];
            float length = 0;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 tangent = path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(0, i - 1)];
                Vector3 normal = new Vector3(-tangent.z, 0, tangent.x).normalized;
                for (int side = 0; side < 2; side++)
                {
                    Vector3 p = path[i] + normal * width * (side == 0 ? -.5f : .5f);
                    p.y = GroundHeight(p.x, p.z) + lift;
                    vertices[i * 2 + side] = p; uvs[i * 2 + side] = new Vector2(side, length * .2f);
                }
                if (i < path.Count - 1) AddQuad(triangles, i * 6, i * 2, i * 2 + 1, i * 2 + 2, i * 2 + 3);
                if (i + 1 < path.Count) length += Vector3.Distance(path[i], path[i + 1]);
            }
            Mesh mesh = OwnMesh(name, vertices, triangles, uvs);
            Add(mesh, Matrix4x4.identity, material);
        }

        private void AddLocal(Mesh mesh, Vector3 origin, Quaternion rotation, Vector3 local, Vector3 scale, Material material)
        {
            Add(mesh, origin + rotation * local, scale, rotation, material);
        }

        private void Add(Mesh mesh, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            Add(mesh, Matrix4x4.TRS(position, rotation, scale), material);
        }

        private void Add(Mesh mesh, Matrix4x4 matrix, Material material)
        {
            if (!batches.TryGetValue(material, out List<CombineInstance> batch))
            {
                batch = new List<CombineInstance>(); batches.Add(material, batch);
            }
            batch.Add(new CombineInstance { mesh = mesh, transform = matrix });
        }

        private void Beam(Vector3 a, Vector3 b, float width, Material material)
        {
            Vector3 delta = b - a;
            Add(cylinder, (a + b) * .5f, new Vector3(width, delta.magnitude, width), Quaternion.FromToRotation(Vector3.up, delta), material);
        }

        private void CompleteBatches()
        {
            foreach (KeyValuePair<Material, List<CombineInstance>> pair in batches)
            {
                var combined = new Mesh { name = pair.Key.name + " scenery batch", indexFormat = IndexFormat.UInt32 };
                combined.CombineMeshes(pair.Value.ToArray(), true, true, false);
                combined.RecalculateBounds(); meshes.Add(combined);
                DisplayMesh(combined.name, combined, pair.Key);
            }
            batches.Clear();
        }

        private void DisplayMesh(string name, Mesh mesh, Material material, bool castShadows = true)
        {
            var instance = new GameObject(name);
            instance.transform.SetParent(sceneryRoot, false);
            instance.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = instance.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private Mesh OwnMesh(string name, Vector3[] vertices, int[] triangles, Vector2[] uv = null)
        {
            var mesh = new Mesh { name = name, indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = vertices; mesh.triangles = triangles;
            if (uv != null) mesh.uv = uv;
            else
            {
                var generated = new Vector2[vertices.Length];
                for (int i = 0; i < vertices.Length; i++) generated[i] = new Vector2(vertices[i].x + vertices[i].y * .3f, vertices[i].z + vertices[i].y * .5f);
                mesh.uv = generated;
            }
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
            return mesh;
        }

        private Mesh CreateCube()
        {
            Vector3[] corners =
            {
                new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f), new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f)
            };
            int[] indices = { 0,3,1, 1,3,2, 5,6,4, 4,6,7, 4,7,0, 0,7,3, 1,2,5, 5,2,6, 3,7,2, 2,7,6, 4,0,5, 5,0,1 };
            return FlatMesh("Shared scenery box", corners, indices);
        }

        private Mesh CreateRoof()
        {
            Vector3[] points =
            {
                new Vector3(-.5f,0,-.5f), new Vector3(.5f,0,-.5f), new Vector3(-.5f,0,.5f), new Vector3(.5f,0,.5f),
                new Vector3(-.5f,1,0), new Vector3(.5f,1,0)
            };
            int[] faces = { 0,4,1, 1,4,5, 2,3,4, 3,5,4, 0,2,4, 1,5,3, 0,1,2, 1,3,2 };
            return FlatMesh("Shared gabled roof", points, faces);
        }

        private Mesh CreateCylinder(int segments)
        {
            var points = new List<Vector3>(); var indices = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                int k = points.Count;
                points.Add(new Vector3(Mathf.Cos(a) * .5f, -.5f, Mathf.Sin(a) * .5f));
                points.Add(new Vector3(Mathf.Cos(a) * .5f, .5f, Mathf.Sin(a) * .5f));
                points.Add(new Vector3(Mathf.Cos(b) * .5f, -.5f, Mathf.Sin(b) * .5f));
                points.Add(new Vector3(Mathf.Cos(b) * .5f, .5f, Mathf.Sin(b) * .5f));
                AddQuad(indices, k, k + 1, k + 2, k + 3);
                points.Add(new Vector3(0, -.5f, 0)); points.Add(new Vector3(0, .5f, 0));
                indices.Add(k + 4); indices.Add(k); indices.Add(k + 2);
                indices.Add(k + 5); indices.Add(k + 3); indices.Add(k + 1);
            }
            return FlatMesh("Shared eight sided trunk", points.ToArray(), indices.ToArray());
        }

        private Mesh CreateIcosphere(int subdivisions, float irregularity, string name, bool smooth = false)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var vertices = new List<Vector3>
            {
                new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),
                new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),
                new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1)
            };
            for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i].normalized * .5f;
            var faces = new List<int> { 0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1 };
            for (int step = 0; step < subdivisions; step++)
            {
                var next = new List<int>();
                for (int i = 0; i < faces.Count; i += 3)
                {
                    int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                    int ab = vertices.Count; vertices.Add((vertices[a] + vertices[b]).normalized * .5f);
                    int bc = vertices.Count; vertices.Add((vertices[b] + vertices[c]).normalized * .5f);
                    int ca = vertices.Count; vertices.Add((vertices[c] + vertices[a]).normalized * .5f);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                faces = next;
            }
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 v = vertices[i];
                float variation = Mathf.PerlinNoise(v.x * 7 + 3.1f, v.z * 8 + v.y * 3 + 4.7f) * 2f - 1f;
                vertices[i] = v * (1f + variation * irregularity);
            }
            if (!smooth) return FlatMesh(name, vertices.ToArray(), faces.ToArray());
            Mesh result = OwnMesh(name, vertices.ToArray(), faces.ToArray());
            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = vertices[i].normalized;
            result.normals = normals;
            return result;
        }

        private Mesh FlatMesh(string name, Vector3[] points, int[] faces)
        {
            var vertices = new Vector3[faces.Length]; var triangles = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++) { vertices[i] = points[faces[i]]; triangles[i] = i; }
            return OwnMesh(name, vertices, triangles);
        }

        private static void AddQuad(List<int> indices, int a, int b, int c, int d)
        {
            indices.Add(a); indices.Add(b); indices.Add(c); indices.Add(c); indices.Add(b); indices.Add(d);
        }

        private static void AddQuad(int[] indices, int offset, int a, int b, int c, int d)
        {
            indices[offset] = a; indices[offset + 1] = b; indices[offset + 2] = c;
            indices[offset + 3] = c; indices[offset + 4] = b; indices[offset + 5] = d;
        }

        private float Range(float min, float max) { return Mathf.Lerp(min, max, (float)random.NextDouble()); }

        private static float Hash(int x, int y)
        {
            uint n = unchecked((uint)(x * 374761393 + y * 668265263));
            n = unchecked((n ^ (n >> 13)) * 1274126177);
            return (n & 65535) / 65535f;
        }

        private void OnDestroy()
        {
            if (sceneryRoot != null) Destroy(sceneryRoot.gameObject);
            foreach (Material material in materials) if (material != null) Destroy(material);
            foreach (Texture2D texture in textures) if (texture != null) Destroy(texture);
            foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
            materials.Clear(); textures.Clear(); meshes.Clear(); batches.Clear();
        }
    }
}
