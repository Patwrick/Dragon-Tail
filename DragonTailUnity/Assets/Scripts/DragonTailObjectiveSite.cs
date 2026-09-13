using System.Collections.Generic;
using UnityEngine;

namespace DragonTail
{
    /// <summary>Authored scenery states, assembled in local space and restored by seeking.</summary>
    public sealed class DragonTailObjectiveSite : MonoBehaviour
    {
        public static readonly string[] Names = { "LOGISTICS SHED", "SERVICE DEPOT", "STORAGE TANKS" };
        public int StructuralPieceCount { get; private set; }
        public ObjectiveState State { get; private set; }

        readonly List<Material> materials = new List<Material>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Texture2D> textures = new List<Texture2D>();
        Transform intactRoot, damagedRoot, rubbleRoot, perimeterRoot;
        Material concrete, plaster, steel, roof, rusty, dark, glass, wood, burnt, rubble, pale;
        Mesh tankRing;
        bool built;

        public void Build(int index)
        {
            if (built) return;
            built = true;
            index = Mathf.Clamp(index, 0, Names.Length - 1);
            gameObject.name = Names[index];
            CreateMaterials(index);
            intactRoot = Group("Intact structure");
            damagedRoot = Group("Damaged structure");
            rubbleRoot = Group("Collapsed structure");
            perimeterRoot = Group("Concrete pad and site props");

            Box("Concrete foundation", perimeterRoot, new Vector3(0, .075f, 0), new Vector3(5.7f, .15f, 4.5f), concrete);
            for (int side = -1; side <= 1; side += 2)
                Box("Raised foundation edge", perimeterRoot, new Vector3(side * 2.78f, .17f, 0), new Vector3(.12f, .19f, 4.5f), pale);
            Box("Drain channel", perimeterRoot, new Vector3(0, .16f, -2.05f), new Vector3(4.1f, .025f, .11f), dark);
            if (index == 0) { BuildWarehouse(); BuildDamagedWarehouse(); }
            else if (index == 1) { BuildServiceDepot(); BuildDamagedDepot(); }
            else { BuildTanks(); BuildDamagedTanks(); }
            BuildRubble(index);
            AddFoundationWeathering(index);
            BatchGeometry(intactRoot);
            BatchGeometry(damagedRoot);
            BatchGeometry(rubbleRoot);
            BatchGeometry(perimeterRoot);
            SetState(ObjectiveState.Untouched);
        }

        public void SetState(ObjectiveState state)
        {
            State = state;
            if (!built) return;
            intactRoot.gameObject.SetActive(state == ObjectiveState.Untouched);
            damagedRoot.gameObject.SetActive(state == ObjectiveState.Damaged);
            rubbleRoot.gameObject.SetActive(state == ObjectiveState.Cleared);
        }

        Transform Group(string label, Transform parent = null)
        {
            var child = new GameObject(label).transform;
            child.SetParent(parent ? parent : transform, false);
            return child;
        }

        Material Surface(string label, Color color, float metal = 0f, float smoothness = .15f)
        {
            var material = new Material(Shader.Find("Standard")) { name = label, color = color };
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Glossiness", smoothness);
            materials.Add(material);
            return material;
        }

        void CreateMaterials(int index)
        {
            concrete = Surface("Weathered concrete", new Color(.57f, .56f, .51f));
            plaster = Surface("Painted masonry", index == 0 ? new Color(.62f, .57f, .44f) : new Color(.66f, .65f, .56f));
            steel = Surface("Dull galvanized steel", new Color(.57f, .60f, .58f), .42f, .25f);
            roof = Surface("Aged green steel roofing", new Color(.29f, .37f, .35f), .26f, .19f);
            rusty = Surface("Oxidized fittings", new Color(.36f, .24f, .16f), .28f);
            dark = Surface("Rubber and deep recesses", new Color(.105f, .13f, .13f));
            glass = Surface("Muted blue window glass", new Color(.19f, .32f, .35f), .22f, .62f);
            wood = Surface("Shipping timber", new Color(.45f, .32f, .18f));
            burnt = Surface("Scorched steel", new Color(.16f, .17f, .16f), .28f);
            rubble = Surface("Broken concrete", new Color(.35f, .35f, .30f));
            pale = Surface("Worn edge paint", new Color(.65f, .61f, .49f));

            var grain = new Texture2D(32, 32, TextureFormat.RGB24, true) { name = "Concrete grain", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[32 * 32];
            var random = new System.Random(71 + index);
            for (int i = 0; i < pixels.Length; i++)
            {
                float value = .82f + (float)random.NextDouble() * .18f;
                pixels[i] = new Color(value, value, value);
            }
            grain.SetPixels(pixels); grain.Apply(true, true); textures.Add(grain);
            concrete.mainTexture = MakeSurfaceTexture("Concrete mottling", 101 + index, 0);
            concrete.mainTextureScale = Vector2.one * 2;
            rubble.mainTexture = grain; rubble.mainTextureScale = Vector2.one * 2;
            plaster.mainTexture = MakeSurfaceTexture("Weathered masonry courses", 121 + index, 1);
            plaster.mainTextureScale = new Vector2(2, 2);
            roof.mainTexture = MakeSurfaceTexture("Scratched roof panels", 141 + index, 2);
            steel.mainTexture = MakeSurfaceTexture("Rain-streaked sheet metal", 161 + index, 3);
            wood.mainTexture = MakeSurfaceTexture("Rough timber grain", 181 + index, 4);
            burnt.mainTexture = MakeSurfaceTexture("Charred and stained surface", 201 + index, 0);
        }

        Texture2D MakeSurfaceTexture(string label, int seed, int pattern)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, true)
            { name = label, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
            var pixels = new Color[size * size];
            var random = new System.Random(seed);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float mottling = Mathf.PerlinNoise(x * .075f + seed, y * .075f + seed * .3f);
                float value = .61f + mottling * .3f + (float)random.NextDouble() * .09f;
                if (pattern == 1)
                {
                    int course = y / 16;
                    bool mortar = y % 16 < 2 || (x + (course % 2) * 16) % 32 < 2;
                    value *= mortar ? .62f : 1f;
                }
                else if (pattern == 2)
                {
                    if (x % 32 < 2) value *= .52f;
                    value *= .87f + .13f * Mathf.PerlinNoise(x * .22f, y * .022f + seed);
                }
                else if (pattern == 3)
                    value *= .78f + .22f * Mathf.PerlinNoise(x * .21f + seed, y * .012f);
                else if (pattern == 4)
                    value *= .63f + .37f * Mathf.PerlinNoise(x * .023f + seed, y * .46f);
                float warm = pattern == 2 || pattern == 3 ? Mathf.Max(0, .35f - mottling) * .45f : 0;
                pixels[y * size + x] = new Color(value + warm, value - warm * .4f, value - warm * .8f);
            }
            texture.SetPixels(pixels); texture.Apply(true, true); textures.Add(texture); return texture;
        }

        void AddFoundationWeathering(int index)
        {
            for (int joint = 0; joint < 4; joint++)
                Box("Concrete expansion joint", perimeterRoot, new Vector3(-2.2f + joint * 1.45f, .154f, 0), new Vector3(.018f, .006f, 4.26f), dark);
            for (int edge = -1; edge <= 1; edge += 2)
                Box("Foundation weathered fascia", perimeterRoot, new Vector3(0, .076f, edge * 2.251f), new Vector3(5.67f, .06f, .012f), rubble);
            if (index < 2)
                Box("Loading threshold", perimeterRoot, new Vector3(index == 0 ? -.73f : -1.77f, .205f, -1.43f), new Vector3(index == 0 ? 1.83f : .9f, .11f, .47f), concrete);
        }

        void BatchGeometry(Transform root)
        {
            var byMaterial = new Dictionary<Material, List<CombineInstance>>();
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (var filter in filters)
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !filter.sharedMesh || !renderer.sharedMaterial) continue;
                var material = renderer.sharedMaterial;
                if (!byMaterial.TryGetValue(material, out var instances))
                { instances = new List<CombineInstance>(); byMaterial.Add(material, instances); }
                instances.Add(new CombineInstance
                {
                    mesh = filter.sharedMesh,
                    transform = root.worldToLocalMatrix * filter.transform.localToWorldMatrix
                });
            }
            foreach (var entry in byMaterial)
            {
                var mesh = new Mesh
                {
                    name = root.name + " / " + entry.Key.name,
                    indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
                };
                mesh.CombineMeshes(entry.Value.ToArray(), true, true); mesh.RecalculateBounds(); meshes.Add(mesh);
                var batch = Group(entry.Key.name, root);
                batch.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                batch.gameObject.AddComponent<MeshRenderer>().sharedMaterial = entry.Key;
            }
            foreach (var filter in filters)
            {
                filter.gameObject.SetActive(false);
                Destroy(filter.gameObject);
            }
        }

        Transform Piece(string label, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var piece = GameObject.CreatePrimitive(type);
            piece.name = label; piece.transform.SetParent(parent, false);
            piece.transform.localPosition = position; piece.transform.localScale = scale;
            piece.GetComponent<Renderer>().sharedMaterial = material;
            var collider = piece.GetComponent<Collider>();
            if (collider) { collider.enabled = false; Destroy(collider); }
            StructuralPieceCount++;
            return piece.transform;
        }

        Transform Box(string label, Transform parent, Vector3 position, Vector3 scale, Material material)
        { return Piece(label, PrimitiveType.Cube, parent, position, scale, material); }

        Transform Tube(string label, Transform parent, Vector3 from, Vector3 to, float radius, Material material)
        {
            Vector3 delta = to - from;
            var piece = Piece(label, PrimitiveType.Cylinder, parent, (from + to) * .5f,
                new Vector3(radius * 2, delta.magnitude * .5f, radius * 2), material);
            piece.localRotation = Quaternion.FromToRotation(Vector3.up, delta);
            return piece;
        }

        void Window(Transform parent, Vector3 center, float width, float height)
        {
            Box("Recessed window", parent, center, new Vector3(width, height, .045f), glass);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Window jamb", parent, center + new Vector3(side * width * .5f, 0, -.035f), new Vector3(.055f, height + .08f, .06f), steel);
                Box("Window lintel", parent, center + new Vector3(0, side * height * .5f, -.035f), new Vector3(width + .05f, .055f, .06f), steel);
            }
            Box("Window mullion", parent, center + new Vector3(0, 0, -.035f), new Vector3(.045f, height, .06f), steel);
            Box("Window transom", parent, center + new Vector3(0, height * .06f, -.039f), new Vector3(width, .035f, .06f), steel);
            Box("Weathered window sill", parent, center + new Vector3(0, -height * .5f - .065f, -.065f), new Vector3(width + .16f, .075f, .17f), concrete);
        }

        void Crate(Transform parent, Vector3 center, Vector3 size)
        {
            Box("Timber shipping crate", parent, center, size, wood);
            for (int side = -1; side <= 1; side += 2)
                Box("Crate strap", parent, center + new Vector3(side * size.x * .3f, 0, 0), new Vector3(.035f, size.y + .015f, size.z + .025f), rusty);
        }

        void Pallet(Transform parent, Vector3 center)
        {
            for (int i = 0; i < 4; i++)
                Box("Pallet slat", parent, center + new Vector3((i - 1.5f) * .18f, .045f, 0), new Vector3(.14f, .055f, .65f), wood);
            for (int side = -1; side <= 1; side += 2)
                Box("Pallet runner", parent, center + new Vector3(0, 0, side * .23f), new Vector3(.74f, .055f, .1f), wood);
        }

        void BuildWarehouse()
        {
            Box("Corrugated warehouse shell", intactRoot, new Vector3(0, .94f, .15f), new Vector3(4.35f, 1.65f, 2.8f), plaster);
            var gables = Group("Warehouse gable infill", intactRoot);
            var gableMesh = new Mesh
            {
                name = "Warehouse gable panels",
                vertices = new[]
                {
                    new Vector3(-2.175f, 1.765f, -1.25f), new Vector3(2.175f, 1.765f, -1.25f), new Vector3(0, 2.15f, -1.25f),
                    new Vector3(-2.175f, 1.765f, 1.55f), new Vector3(2.175f, 1.765f, 1.55f), new Vector3(0, 2.15f, 1.55f)
                },
                triangles = new[] { 0, 2, 1, 3, 4, 5 }
            };
            gableMesh.RecalculateNormals(); gableMesh.RecalculateBounds(); meshes.Add(gableMesh);
            gables.gameObject.AddComponent<MeshFilter>().sharedMesh = gableMesh;
            gables.gameObject.AddComponent<MeshRenderer>().sharedMaterial = plaster;
            StructuralPieceCount++;
            for (int i = 0; i < 12; i++)
            {
                float x = -2.1f + i * .38f;
                Box("Rear wall corrugation", intactRoot, new Vector3(x, .94f, 1.565f), new Vector3(.035f, 1.62f, .045f), steel);
                if (x < -1.7f || x > .25f)
                    Box("Front wall corrugation", intactRoot, new Vector3(x, .94f, -1.265f), new Vector3(.035f, 1.62f, .045f), steel);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                var panel = Box("Pitched standing-seam roof", intactRoot, new Vector3(side * 1.15f, 1.945f, .15f), new Vector3(2.38f, .1f, 3.05f), roof);
                panel.localRotation = Quaternion.Euler(0, 0, -side * 10);
                // Child ribs use unit scale so the seam dimensions stay independent of the panel.
                var seams = Group("Roof seam battens", intactRoot);
                seams.localPosition = panel.localPosition; seams.localRotation = panel.localRotation;
                for (int rib = 0; rib < 9; rib++)
                    Box("Raised roof seam", seams, new Vector3(0, .065f, -1.4f + rib * .35f), new Vector3(2.37f, .035f, .03f), steel);
                for (int rib = 0; rib < 7; rib++)
                    Box("Side wall rib", intactRoot, new Vector3(side * 2.19f, .95f, -.95f + rib * .35f), new Vector3(.035f, 1.62f, .045f), steel);
            }
            Tube("Ridge cap", intactRoot, new Vector3(0, 2.17f, -1.4f), new Vector3(0, 2.17f, 1.7f), .04f, steel);
            Box("Roller-door recess", intactRoot, new Vector3(-.73f, .9f, -1.29f), new Vector3(1.72f, 1.5f, .06f), dark);
            for (int slat = 0; slat < 9; slat++)
                Box("Roller-door slat", intactRoot, new Vector3(-.73f, .23f + slat * .16f, -1.335f), new Vector3(1.65f, .13f, .04f), steel);
            for (int side = -1; side <= 1; side += 2)
                Box("Roller-door frame", intactRoot, new Vector3(-.73f + side * .9f, .92f, -1.34f), new Vector3(.1f, 1.58f, .1f), pale);
            Window(intactRoot, new Vector3(1.32f, 1.25f, -1.30f), .82f, .46f);
            Box("Loading canopy", intactRoot, new Vector3(-.73f, 1.78f, -1.64f), new Vector3(2.22f, .08f, .95f), roof);
            for (int side = -1; side <= 1; side += 2)
                Box("Canopy support", intactRoot, new Vector3(-.73f + side * 1.04f, .95f, -2.04f), new Vector3(.065f, 1.6f, .065f), steel);
            Box("Roof exhaust housing", intactRoot, new Vector3(1.65f, 1.98f, .9f), new Vector3(.4f, .25f, .45f), steel);
            Box("Exhaust louvres", intactRoot, new Vector3(1.65f, 1.99f, .663f), new Vector3(.31f, .12f, .025f), dark);
            for (int corner = -1; corner <= 1; corner += 2)
            {
                Box("Warehouse corner flashing", intactRoot, new Vector3(corner * 2.18f, .96f, -1.28f), new Vector3(.12f, 1.7f, .12f), roof);
                Tube("Rain downpipe", intactRoot, new Vector3(corner * 2.26f, .24f, 1.53f), new Vector3(corner * 2.26f, 1.82f, 1.53f), .045f, rusty);
            }
            Box("Warehouse foundation course", intactRoot, new Vector3(0, .29f, -1.29f), new Vector3(4.32f, .23f, .05f), concrete);
            Pallet(perimeterRoot, new Vector3(2.31f, .2f, -1.65f));
            Crate(perimeterRoot, new Vector3(2.31f, .48f, -1.65f), new Vector3(.66f, .46f, .6f));
            Crate(perimeterRoot, new Vector3(-2.38f, .42f, 1.8f), new Vector3(.65f, .5f, .58f));
        }

        void BuildServiceDepot()
        {
            Box("Service building", intactRoot, new Vector3(-.6f, .85f, .2f), new Vector3(3.6f, 1.4f, 2.5f), plaster);
            Box("Flat roof flashing", intactRoot, new Vector3(-.6f, 1.65f, .2f), new Vector3(3.85f, .18f, 2.75f), roof);
            Box("Rear roof parapet", intactRoot, new Vector3(-.6f, 1.78f, 1.5f), new Vector3(3.8f, .2f, .11f), concrete);
            for (int side = -1; side <= 1; side += 2)
                Box("Roof side parapet", intactRoot, new Vector3(-.6f + side * 1.86f, 1.76f, .2f), new Vector3(.1f, .17f, 2.63f), concrete);
            Box("Service building plinth", intactRoot, new Vector3(-.6f, .3f, -1.072f), new Vector3(3.61f, .27f, .055f), concrete);
            for (int i = 0; i < 3; i++)
                Window(intactRoot, new Vector3(-.65f + i * .62f, 1.03f, -1.07f), .49f, .56f);
            Box("Panel service door", intactRoot, new Vector3(-1.77f, .77f, -1.08f), new Vector3(.76f, 1.23f, .08f), steel);
            Box("Door inset", intactRoot, new Vector3(-1.77f, 1.02f, -1.135f), new Vector3(.54f, .4f, .035f), dark);
            Box("Door handle", intactRoot, new Vector3(-1.5f, .74f, -1.17f), new Vector3(.035f, .17f, .06f), rusty);
            Box("Roof HVAC", intactRoot, new Vector3(-.8f, 1.93f, .35f), new Vector3(1.03f, .38f, .8f), steel);
            for (int slot = 0; slot < 6; slot++)
                Box("HVAC grille", intactRoot, new Vector3(-1.21f + slot * .16f, 1.94f, -.062f), new Vector3(.055f, .23f, .018f), dark);
            Piece("HVAC fan cover", PrimitiveType.Cylinder, intactRoot, new Vector3(-.8f, 2.135f, .35f), new Vector3(.49f, .015f, .49f), dark);
            for (int bar = 0; bar < 3; bar++)
                Box("Fan grille crossbar", intactRoot, new Vector3(-.8f, 2.155f, .2f + bar * .15f), new Vector3(.43f, .015f, .018f), steel);
            Box("Roof utility enclosure", intactRoot, new Vector3(.6f, 1.95f, .96f), new Vector3(.53f, .35f, .47f), pale);
            Tube("Roof conduit", intactRoot, new Vector3(-1.6f, 1.77f, .45f), new Vector3(.85f, 1.77f, .45f), .035f, rusty);
            Tube("Conduit drop", intactRoot, new Vector3(.85f, 1.77f, .45f), new Vector3(.85f, .4f, .45f), .035f, rusty);
            Tube("Utility mast", intactRoot, new Vector3(-1.95f, 1.76f, 1.15f), new Vector3(-1.95f, 3.3f, 1.15f), .035f, steel);
            Tube("Mast crossbar", intactRoot, new Vector3(-2.3f, 2.9f, 1.15f), new Vector3(-1.6f, 2.9f, 1.15f), .025f, rusty);
            Box("Generator base", perimeterRoot, new Vector3(2.02f, .23f, .22f), new Vector3(1.08f, .16f, 1.43f), concrete);
            Box("Generator cabinet", perimeterRoot, new Vector3(2.02f, .69f, .22f), new Vector3(.94f, .8f, 1.2f), roof);
            for (int slot = 0; slot < 5; slot++)
                Box("Generator cooling louvre", perimeterRoot, new Vector3(1.73f + slot * .14f, .72f, -.397f), new Vector3(.065f, .42f, .025f), dark);
            Tube("Generator exhaust", perimeterRoot, new Vector3(2.2f, 1.1f, .65f), new Vector3(2.2f, 1.56f, .65f), .045f, rusty);
            for (int post = 0; post < 4; post++)
                Box("Utility fence post", perimeterRoot, new Vector3(2.69f, .69f, -.95f + post * .77f), new Vector3(.045f, 1.04f, .045f), steel);
            for (int rail = 0; rail < 3; rail++)
                Tube("Utility fence wire", perimeterRoot, new Vector3(2.69f, .45f + rail * .3f, -.95f), new Vector3(2.69f, .45f + rail * .3f, 1.36f), .012f, rusty);
            Crate(perimeterRoot, new Vector3(1.74f, .36f, -1.6f), new Vector3(.68f, .4f, .58f));
        }

        void BuildTanks()
        {
            tankRing = MakeTorus(.56f, .024f);
            for (int tank = 0; tank < 3; tank++)
            {
                float x = (tank - 1) * 1.38f;
                Piece("Tank concrete plinth", PrimitiveType.Cylinder, perimeterRoot, new Vector3(x, .22f, -.3f), new Vector3(1.29f, .075f, 1.29f), concrete);
                Piece("Painted storage tank", PrimitiveType.Cylinder, intactRoot, new Vector3(x, 1.01f, -.3f), new Vector3(1.07f, .73f, 1.07f), steel);
                Piece("Tank domed cap", PrimitiveType.Sphere, intactRoot, new Vector3(x, 1.74f, -.3f), new Vector3(1.065f, .27f, 1.065f), pale);
                Piece("Tank inspection hatch", PrimitiveType.Cylinder, intactRoot, new Vector3(x - .16f, 1.875f, -.27f), new Vector3(.29f, .024f, .29f), steel);
                Tube("Tank breather neck", intactRoot, new Vector3(x + .2f, 1.82f, -.29f), new Vector3(x + .2f, 2.035f, -.29f), .035f, rusty);
                Piece("Breather rain cap", PrimitiveType.Cylinder, intactRoot, new Vector3(x + .2f, 2.055f, -.29f), new Vector3(.15f, .018f, .15f), roof);
                for (int band = 0; band < 3; band++) Ring("Tank retaining band", intactRoot, new Vector3(x, .48f + band * .55f, -.3f), tankRing, rusty);
                for (int side = -1; side <= 1; side += 2)
                    Tube("Tank ladder rail", intactRoot, new Vector3(x + side * .17f, .29f, -.93f), new Vector3(x + side * .17f, 1.97f, -.93f), .018f, steel);
                for (int step = 0; step < 9; step++)
                    Tube("Tank ladder rung", intactRoot, new Vector3(x - .18f, .4f + step * .17f, -.94f), new Vector3(x + .18f, .4f + step * .17f, -.94f), .015f, rusty);
                Tube("Tank outlet pipe", intactRoot, new Vector3(x, .58f, -.3f), new Vector3(x, .58f, -1.43f), .06f, roof);
                Tube("Pipe flange", intactRoot, new Vector3(x, .58f, -1.005f), new Vector3(x, .58f, -1.055f), .105f, rusty);
                Piece("Tank valve wheel", PrimitiveType.Cylinder, intactRoot, new Vector3(x, .69f, -1.19f), new Vector3(.18f, .02f, .18f), rusty);
            }
            Tube("Shared process pipe", intactRoot, new Vector3(-2.2f, .58f, -1.43f), new Vector3(2.2f, .58f, -1.43f), .075f, roof);
            Box("Catwalk deck", intactRoot, new Vector3(0, 1.4f, .71f), new Vector3(4.25f, .085f, .43f), rusty);
            for (int post = 0; post < 6; post++)
            {
                float x = -2.03f + post * .81f;
                Box("Catwalk support", intactRoot, new Vector3(x, .83f, .71f), new Vector3(.05f, 1.15f, .06f), steel);
                Tube("Catwalk railing post", intactRoot, new Vector3(x, 1.44f, .94f), new Vector3(x, 2.02f, .94f), .02f, steel);
            }
            Tube("Catwalk handrail", intactRoot, new Vector3(-2.07f, 2.02f, .94f), new Vector3(2.07f, 2.02f, .94f), .022f, steel);
            Box("Site service hut", perimeterRoot, new Vector3(1.95f, .7f, 1.55f), new Vector3(1.12f, 1.06f, .9f), plaster);
            Box("Service hut roof", perimeterRoot, new Vector3(1.95f, 1.28f, 1.55f), new Vector3(1.23f, .1f, 1.01f), roof);
            Box("Service hut door", perimeterRoot, new Vector3(1.95f, .65f, 1.08f), new Vector3(.52f, .91f, .045f), rusty);
        }

        void BuildDamagedWarehouse()
        {
            Box("Scorched interior slab", damagedRoot, new Vector3(0, .2f, .15f), new Vector3(4.3f, .09f, 2.8f), burnt);
            Box("Remaining back wall", damagedRoot, new Vector3(0, .79f, 1.51f), new Vector3(4.2f, 1.2f, .16f), plaster);
            for (int side = -1; side <= 1; side += 2)
            {
                var wall = Box("Broken side wall", damagedRoot, new Vector3(side * 2.07f, .7f, .2f), new Vector3(.16f, 1.08f, 2.7f), burnt);
                wall.localRotation = Quaternion.Euler(0, 0, -side * 8);
                Tube("Exposed roof beam", damagedRoot, new Vector3(side * 2.1f, 1.25f, 1.35f), new Vector3(0, 1.72f, 1.35f), .055f, rusty);
            }
            for (int panel = 0; panel < 4; panel++)
            {
                var sheet = Box("Torn roof sheet", damagedRoot, new Vector3(-1.65f + panel * 1.06f, 1.14f + (panel % 2) * .27f, .24f), new Vector3(.91f, .065f, 2.56f), panel % 2 == 0 ? burnt : roof);
                sheet.localRotation = Quaternion.Euler(panel * 7 - 8, panel * 3, panel % 2 == 0 ? 19 : -26);
            }
            var door = Box("Buckled loading door", damagedRoot, new Vector3(-.7f, .51f, -1.38f), new Vector3(1.6f, .75f, .09f), rusty);
            door.localRotation = Quaternion.Euler(24, 7, -8);
            Scatter(damagedRoot, 7, 107);
        }

        void BuildDamagedDepot()
        {
            Box("Fire-blackened floor", damagedRoot, new Vector3(-.6f, .2f, .2f), new Vector3(3.65f, .1f, 2.5f), burnt);
            Box("Remaining service rooms", damagedRoot, new Vector3(-.86f, .63f, .4f), new Vector3(3.04f, .88f, 1.95f), plaster);
            Box("Broken front parapet", damagedRoot, new Vector3(-1.75f, .78f, -1.01f), new Vector3(1.2f, 1.18f, .16f), burnt);
            Window(damagedRoot, new Vector3(-1.77f, .8f, -1.11f), .55f, .39f);
            for (int piece = 0; piece < 3; piece++)
            {
                var slab = Box("Collapsed roof slab", damagedRoot, new Vector3(-1.7f + piece * 1.15f, 1.11f + (piece % 2) * .16f, .23f), new Vector3(1.04f, .15f, 2.42f), piece == 1 ? concrete : burnt);
                slab.localRotation = Quaternion.Euler(piece * 4 - 5, piece * 2, piece == 1 ? -12 : 8);
            }
            var hvac = Box("Dislodged HVAC", damagedRoot, new Vector3(-1.02f, 1.26f, .31f), new Vector3(.95f, .34f, .73f), steel);
            hvac.localRotation = Quaternion.Euler(8, 18, -16);
            Tube("Bent service mast", damagedRoot, new Vector3(-1.92f, .94f, 1.07f), new Vector3(-1.15f, 1.49f, 1.36f), .035f, rusty);
            Tube("Loose conduit", damagedRoot, new Vector3(.66f, 1.09f, .38f), new Vector3(1.28f, .29f, -.52f), .035f, rusty);
            Scatter(damagedRoot, 9, 211);
        }

        void BuildDamagedTanks()
        {
            for (int tank = 0; tank < 3; tank++)
            {
                float x = (tank - 1) * 1.38f;
                var shell = Piece("Dented tank shell", PrimitiveType.Cylinder, damagedRoot, new Vector3(x, .68f + tank * .12f, -.3f), new Vector3(1.1f, .42f + tank * .08f, .98f), tank == 1 ? burnt : steel);
                shell.localRotation = Quaternion.Euler(0, 0, tank == 1 ? -16 : 7);
                Ring("Twisted tank band", damagedRoot, new Vector3(x, 1.04f + tank * .16f, -.3f), tankRing, rusty).localRotation = shell.localRotation;
            }
            var walk = Box("Dropped catwalk", damagedRoot, new Vector3(0, .56f, .82f), new Vector3(4.13f, .07f, .42f), burnt);
            walk.localRotation = Quaternion.Euler(15, 3, -7);
            Tube("Broken pipe left", damagedRoot, new Vector3(-2.13f, .59f, -1.37f), new Vector3(-.45f, .34f, -1.54f), .075f, roof);
            Tube("Broken pipe right", damagedRoot, new Vector3(.3f, .72f, -1.45f), new Vector3(2.13f, .56f, -1.37f), .075f, rusty);
            Scatter(damagedRoot, 8, 307);
        }

        void BuildRubble(int index)
        {
            Piece("Scorched footprint", PrimitiveType.Cylinder, rubbleRoot, new Vector3(-.15f, .162f, .1f), new Vector3(4.4f, .008f, 3.15f), burnt);
            Scatter(rubbleRoot, 18, 401 + index);
            for (int beam = 0; beam < 5; beam++)
            {
                var broken = Box("Collapsed roof beam", rubbleRoot, new Vector3(-1.6f + beam * .72f, .34f + (beam % 2) * .09f, -.64f + (beam % 3) * .51f), new Vector3(1.13f, .09f, .13f), rusty);
                broken.localRotation = Quaternion.Euler(9, beam * 37, beam % 2 == 0 ? 11 : -8);
            }
            for (int panel = 0; panel < 4; panel++)
            {
                var sheet = Box("Collapsed roof fragment", rubbleRoot, new Vector3(-1.4f + panel * .8f, .28f + panel * .025f, .7f - (panel % 2) * .92f), new Vector3(.83f, .045f, .66f), panel % 2 == 0 ? roof : burnt);
                sheet.localRotation = Quaternion.Euler(5 + panel * 3, panel * 26, -7);
            }
        }

        void Scatter(Transform parent, int count, int seed)
        {
            var random = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float width = .19f + (float)random.NextDouble() * .36f;
                float height = .13f + (float)random.NextDouble() * .25f;
                float depth = .19f + (float)random.NextDouble() * .35f;
                var piece = Box("Concrete rubble chunk", parent,
                    new Vector3(-2.06f + (float)random.NextDouble() * 4.12f, .19f + height * .5f, -1.38f + (float)random.NextDouble() * 2.76f),
                    new Vector3(width, height, depth), i % 4 == 0 ? burnt : rubble);
                piece.localRotation = Quaternion.Euler(random.Next(0, 22), random.Next(0, 180), random.Next(-15, 16));
            }
        }

        Mesh MakeTorus(float radius, float tube)
        {
            const int around = 28, section = 5;
            var vertices = new Vector3[around * section];
            var triangles = new int[around * section * 6];
            for (int a = 0; a < around; a++) for (int s = 0; s < section; s++)
            {
                float angle = a * Mathf.PI * 2 / around, cross = s * Mathf.PI * 2 / section;
                float distance = radius + Mathf.Cos(cross) * tube;
                int i = a * section + s;
                vertices[i] = new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(cross) * tube, Mathf.Sin(angle) * distance);
                int nextA = ((a + 1) % around) * section + s;
                int nextS = a * section + (s + 1) % section;
                int diagonal = ((a + 1) % around) * section + (s + 1) % section;
                int t = i * 6;
                triangles[t] = i; triangles[t + 1] = nextS; triangles[t + 2] = nextA;
                triangles[t + 3] = nextS; triangles[t + 4] = diagonal; triangles[t + 5] = nextA;
            }
            var mesh = new Mesh { name = "Tank retaining-band mesh", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh); return mesh;
        }

        Transform Ring(string label, Transform parent, Vector3 position, Mesh mesh, Material material)
        {
            var ring = Group(label, parent); ring.localPosition = position;
            ring.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            ring.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            StructuralPieceCount++; return ring;
        }

        void OnDestroy()
        {
            foreach (var material in materials) if (material) Destroy(material);
            foreach (var mesh in meshes) if (mesh) Destroy(mesh);
            foreach (var texture in textures) if (texture) Destroy(texture);
        }
    }
}
