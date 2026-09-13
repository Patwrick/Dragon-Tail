using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DragonTail
{
    /// <summary>Cosmetic quadcopter model, independent of the authored flight timeline.</summary>
    public sealed class DragonTailDroneModel : MonoBehaviour
    {
        public Transform[] Rotors { get; } = new Transform[4];
        public Renderer RoleLight { get; private set; }
        readonly Dictionary<Material, List<CombineInstance>> batches = new Dictionary<Material, List<CombineInstance>>();
        readonly List<GameObject> sources = new List<GameObject>();
        readonly List<Mesh> meshes = new List<Mesh>();

        public void Build(Material shell, Material frame, Material metal, Material glass, Material light)
        {
            Piece("Rounded upper fuselage", PrimitiveType.Sphere, new Vector3(0,.04f,0), new Vector3(.59f,.28f,.77f), shell);
            Piece("Lower airframe", PrimitiveType.Cube, new Vector3(0,-.08f,0), new Vector3(.43f,.15f,.53f), frame);
            Piece("Battery cover", PrimitiveType.Sphere, new Vector3(0,.16f,-.07f), new Vector3(.4f,.14f,.46f), frame);
            Piece("Battery securing band", PrimitiveType.Cube, new Vector3(0,.18f,-.08f), new Vector3(.43f,.028f,.07f), metal);
            Piece("Nose camera gimbal", PrimitiveType.Sphere, new Vector3(0,-.12f,.33f), new Vector3(.23f,.21f,.2f), frame);
            var lens=Piece("Camera lens", PrimitiveType.Cylinder, new Vector3(0,-.12f,.434f), new Vector3(.13f,.022f,.13f), glass);
            lens.localRotation=Quaternion.Euler(90,0,0);
            // The dynamic role accent is small; the airframe retains its own materials.
            RoleLight=Piece("Status light strip", PrimitiveType.Cube, new Vector3(0,.223f,.12f), new Vector3(.29f,.035f,.075f), light, false).GetComponent<Renderer>();
            Mesh propeller=CreatePropeller();
            int rotor=0;
            for(int x=-1;x<=1;x+=2) for(int z=-1;z<=1;z+=2)
            {
                Vector3 motor=new Vector3(x*.56f,.08f,z*.56f);
                Tube("Carbon arm", new Vector3(x*.18f,-.015f,z*.15f), motor+Vector3.down*.045f, .052f, frame);
                Tube("Arm cable sleeve", new Vector3(x*.17f,.04f,z*.14f), motor+Vector3.up*.02f, .017f, metal);
                Piece("Motor mount", PrimitiveType.Sphere, motor+Vector3.down*.07f, new Vector3(.24f,.13f,.24f), shell);
                Piece("Motor housing", PrimitiveType.Cylinder, motor, new Vector3(.17f,.09f,.17f), frame);
                Piece("Motor top ring", PrimitiveType.Cylinder, motor+Vector3.up*.088f, new Vector3(.18f,.016f,.18f), metal);
                var pivot=new GameObject("Spinning two-blade propeller").transform;
                pivot.SetParent(transform,false);pivot.localPosition=motor+Vector3.up*.132f;Rotors[rotor++]=pivot;
                pivot.gameObject.AddComponent<MeshFilter>().sharedMesh=propeller;
                pivot.gameObject.AddComponent<MeshRenderer>().sharedMaterial=frame;
                var cap=Piece("Propeller nut",PrimitiveType.Sphere,pivot.localPosition+Vector3.up*.025f,new Vector3(.065f,.045f,.065f),metal,false);
                cap.SetParent(pivot,true);
                Tube("Landing strut",new Vector3(x*.23f,-.12f,z*.21f),new Vector3(x*.32f,-.34f,z*.26f),.026f,metal);
            }
            for(int side=-1;side<=1;side+=2)
            {
                Tube("Landing skid",new Vector3(side*.32f,-.35f,-.35f),new Vector3(side*.32f,-.35f,.34f),.029f,frame);
                Piece("Shoulder panel",PrimitiveType.Cube,new Vector3(side*.253f,.075f,-.07f),new Vector3(.025f,.085f,.25f),metal);
            }
            CombineStaticParts();
        }

        Transform Piece(string label,PrimitiveType kind,Vector3 position,Vector3 scale,Material material,bool combine=true)
        {
            var piece=GameObject.CreatePrimitive(kind);piece.name=label;piece.transform.SetParent(transform,false);
            piece.transform.localPosition=position;piece.transform.localScale=scale;piece.GetComponent<Renderer>().sharedMaterial=material;
            var collider=piece.GetComponent<Collider>();if(collider){collider.enabled=false;Destroy(collider);}
            if(combine) sources.Add(piece);
            return piece.transform;
        }

        void Tube(string label,Vector3 from,Vector3 to,float radius,Material material)
        {
            var delta=to-from;
            var piece=Piece(label,PrimitiveType.Cylinder,(from+to)*.5f,new Vector3(radius*2,delta.magnitude*.5f,radius*2),material);
            piece.localRotation=Quaternion.FromToRotation(Vector3.up,delta);
        }

        Mesh CreatePropeller()
        {
            Vector3[] outline={new Vector3(.05f,0,-.036f),new Vector3(.25f,0,-.063f),new Vector3(.35f,0,-.025f),
                new Vector3(.32f,0,.017f),new Vector3(.04f,0,.036f),new Vector3(-.05f,0,.036f),
                new Vector3(-.25f,0,.063f),new Vector3(-.35f,0,.025f),new Vector3(-.32f,0,-.017f),new Vector3(-.04f,0,-.036f)};
            var vertices=new List<Vector3>();var triangles=new List<int>();
            // Separate upper/lower faces preserve their normals on the thin blade.
            for(int side=0;side<2;side++)
            {
                int start=vertices.Count;float y=side==0?.008f:-.008f;vertices.Add(Vector3.up*y);
                foreach(var p in outline) vertices.Add(p+Vector3.up*y);
                for(int i=0;i<outline.Length;i++)
                {
                    int a=start+1+i,b=start+1+(i+1)%outline.Length;
                    triangles.Add(start);triangles.Add(side==0?b:a);triangles.Add(side==0?a:b);
                }
            }
            var mesh=new Mesh{name="Swept two-blade propeller"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            meshes.Add(mesh);return mesh;
        }

        void CombineStaticParts()
        {
            foreach(var source in sources)
            {
                var renderer=source.GetComponent<MeshRenderer>();var material=renderer.sharedMaterial;
                if(!batches.TryGetValue(material,out var parts)){parts=new List<CombineInstance>();batches.Add(material,parts);}
                parts.Add(new CombineInstance{mesh=source.GetComponent<MeshFilter>().sharedMesh,transform=transform.worldToLocalMatrix*source.transform.localToWorldMatrix});
                renderer.enabled=false;
            }
            foreach(var batch in batches)
            {
                var mesh=new Mesh{name="Airframe / "+batch.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(batch.Value.ToArray(),true,true);meshes.Add(mesh);
                var group=new GameObject(mesh.name);group.transform.SetParent(transform,false);group.AddComponent<MeshFilter>().sharedMesh=mesh;
                group.AddComponent<MeshRenderer>().sharedMaterial=batch.Key;
            }
            foreach(var source in sources) Destroy(source);
            sources.Clear();batches.Clear();
        }

        void OnDestroy(){foreach(var mesh in meshes) if(mesh) Destroy(mesh);}
    }
}
