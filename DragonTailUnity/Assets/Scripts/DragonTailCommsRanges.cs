using UnityEngine;
using UnityEngine.Rendering;

namespace DragonTail
{
    /// <summary>Illustrative game-radius overlays, with no radio or propagation calculation.</summary>
    public sealed class DragonTailCommsRanges : MonoBehaviour
    {
        public const float DisplayRadius = 36f;
        const int Segments = 96, Rings = 13;
        readonly Mesh[] meshes = new Mesh[8];
        readonly GameObject[] zones = new GameObject[8];
        readonly Vector3[][] vertices = new Vector3[8][];
        readonly Color[][] colors = new Color[8][];
        readonly Vector3[] previous = new Vector3[8];
        readonly bool[] positioned = new bool[8];
        readonly Vector2[] offsets = new Vector2[1 + Rings * Segments];
        readonly float[] opacity = new float[1 + Rings * Segments];
        Material material;
        float[,] heights;
        int columns, rows;
        public int VisibleCount { get; private set; }

        void Awake()
        {
            // Cache the purely visual ground surface so eight moving overlays do not
            // regenerate procedural terrain noise every frame.
            columns = Mathf.CeilToInt(DragonTailLandscape.Width * 2);
            rows = Mathf.CeilToInt(DragonTailLandscape.Depth * 2);
            heights = new float[columns + 1, rows + 1];
            for(int z=0;z<=rows;z++) for(int x=0;x<=columns;x++)
                heights[x,z]=DragonTailLandscape.GroundHeight(-DragonTailLandscape.Width*.5f+x*.5f,-DragonTailLandscape.Depth*.5f+z*.5f);
            material=new Material(Shader.Find("Sprites/Default")){name="Translucent red game range",color=Color.white};
            material.renderQueue=3001;
            opacity[0]=.047f;
            for(int ring=0;ring<Rings;ring++)
            {
                float radius=ring<11 ? DisplayRadius*(ring+1)/12f : ring==11 ? DisplayRadius-.15f : DisplayRadius;
                for(int point=0;point<Segments;point++)
                {
                    int index=1+ring*Segments+point;float a=point*Mathf.PI*2/Segments;
                    offsets[index]=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                    opacity[index]=ring>=11?.38f:.047f;
                }
            }
            var triangles=new int[Segments*3+(Rings-1)*Segments*6];int at=0;
            for(int point=0;point<Segments;point++)
            {
                triangles[at++]=0;triangles[at++]=1+(point+1)%Segments;triangles[at++]=1+point;
            }
            for(int ring=1;ring<Rings;ring++) for(int point=0;point<Segments;point++)
            {
                int a=1+(ring-1)*Segments+point,b=1+(ring-1)*Segments+(point+1)%Segments;
                int c=a+Segments,d=b+Segments;
                triangles[at++]=a;triangles[at++]=b;triangles[at++]=c;
                triangles[at++]=b;triangles[at++]=d;triangles[at++]=c;
            }
            for(int i=0;i<zones.Length;i++)
            {
                zones[i]=new GameObject("Range overlay D"+(i+1).ToString("00"));zones[i].transform.SetParent(transform,false);
                vertices[i]=new Vector3[offsets.Length];colors[i]=new Color[offsets.Length];
                meshes[i]=new Mesh{name=zones[i].name};meshes[i].MarkDynamic();meshes[i].vertices=vertices[i];meshes[i].triangles=triangles;
                zones[i].AddComponent<MeshFilter>().sharedMesh=meshes[i];
                var renderer=zones[i].AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                zones[i].SetActive(false);
            }
        }

        float Height(float x,float z)
        {
            float gx=Mathf.Clamp((x+DragonTailLandscape.Width*.5f)*2,0,columns-.001f);
            float gz=Mathf.Clamp((z+DragonTailLandscape.Depth*.5f)*2,0,rows-.001f);
            int ix=(int)gx,iz=(int)gz;
            return Mathf.Lerp(Mathf.Lerp(heights[ix,iz],heights[ix+1,iz],gx-ix),Mathf.Lerp(heights[ix,iz+1],heights[ix+1,iz+1],gx-ix),gz-iz);
        }

        public void Render(StoryFrame frame,bool show)
        {
            VisibleCount=0;
            for(int i=0;i<zones.Length;i++)
            {
                bool active=show&&frame.Drones[i].Visible;zones[i].SetActive(active);
                if(!active)continue;VisibleCount++;
                Vector3 center=frame.Drones[i].Position;
                if(positioned[i]&&(previous[i]-center).sqrMagnitude<.0001f)continue;
                for(int v=0;v<offsets.Length;v++)
                {
                    float x=center.x+offsets[v].x,z=center.z+offsets[v].y;
                    float edge=Mathf.Min(DragonTailLandscape.Width*.5f-Mathf.Abs(x),DragonTailLandscape.Depth*.5f-Mathf.Abs(z));
                    vertices[i][v]=new Vector3(x,Height(x,z)+.17f+i*.002f,z);
                    colors[i][v]=new Color(.92f,.19f,.22f,opacity[v]*Mathf.Clamp01(edge/1.2f));
                }
                meshes[i].vertices=vertices[i];meshes[i].colors=colors[i];meshes[i].RecalculateBounds();
                previous[i]=center;positioned[i]=true;
            }
        }

        void OnDestroy()
        {
            foreach(var mesh in meshes)if(mesh)Destroy(mesh);
            if(material)Destroy(material);
        }
    }
}
