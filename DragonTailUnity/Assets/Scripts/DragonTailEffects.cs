using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DragonTail
{
    /// <summary>Deterministic, seekable explosions and passive relay shield visuals.</summary>
    public sealed class DragonTailEffects : MonoBehaviour
    {
        sealed class ExplosionVisual
        {
            public Transform Root, Core;
            public Transform[] Sparks, Smoke;
            public Material[] SmokeMaterials;
            public LineRenderer Ring;
        }
        sealed class ShieldVisual
        {
            public Transform Root;
            public Material Surface;
            public LineRenderer[] Rings;
            public LineRenderer HeightGuide, OrbitGuide;
        }
        readonly ExplosionVisual[] explosions = new ExplosionVisual[8];
        readonly ShieldVisual[] shields = new ShieldVisual[3];
        readonly List<Material> materials = new List<Material>();
        static readonly Color Gold = new Color32(237,195,111,255);
        static readonly Color Fire = new Color32(255,126,73,255);
        Material hot, ember;
        public int RenderedExplosionCount { get; private set; }
        public int RenderedShieldCount { get; private set; }

        void Awake()
        {
            hot = Flat(new Color(1,.91f,.59f)); ember = Flat(Fire);
            for (int i=0; i<8; i++) explosions[i] = CreateExplosion(i);
            for (int i=0; i<3; i++) shields[i] = CreateShield(i);
        }
        Transform Root(string name)
        {
            var root = new GameObject(name).transform; root.SetParent(transform,false); return root;
        }
        Material Flat(Color color, bool transparent=false)
        {
            var mat = new Material(Shader.Find(transparent ? "Sprites/Default" : "Unlit/Color"));
            mat.color=color; materials.Add(mat); return mat;
        }
        Transform Shape(string name, PrimitiveType shape, Transform parent, Material material)
        {
            var go=GameObject.CreatePrimitive(shape);go.name=name;go.transform.SetParent(parent,false);
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material;
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            var col=go.GetComponent<Collider>();if(col) Destroy(col);
            return go.transform;
        }
        LineRenderer Line(string name,Transform parent,Color color,float width,bool circle)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=Flat(Color.white,true);
            line.useWorldSpace=false;line.widthMultiplier=width;line.loop=circle;
            line.startColor=line.endColor=color;line.numCapVertices=3;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            line.positionCount=circle?48:2;
            if(circle) for(int j=0;j<48;j++)
            {float a=j*Mathf.PI*2/48;line.SetPosition(j,new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)));}
            return line;
        }
        ExplosionVisual CreateExplosion(int index)
        {
            var e=new ExplosionVisual { Root=Root("Explosion D"+(index+1).ToString("00")), Sparks=new Transform[12], Smoke=new Transform[5], SmokeMaterials=new Material[5] };
            e.Core=Shape("Fire burst",PrimitiveType.Sphere,e.Root,ember);
            for(int j=0;j<12;j++) e.Sparks[j]=Shape("Ember "+j,PrimitiveType.Cube,e.Root,j%3==0?hot:ember);
            for(int j=0;j<5;j++)
            {
                e.SmokeMaterials[j]=Flat(new Color(.27f,.31f,.32f,.55f),true);
                e.Smoke[j]=Shape("Smoke puff "+j,PrimitiveType.Sphere,e.Root,e.SmokeMaterials[j]);
            }
            e.Ring=Line("Blast ripple",e.Root,Fire,.08f,true);
            e.Root.gameObject.SetActive(false);return e;
        }
        ShieldVisual CreateShield(int index)
        {
            var s=new ShieldVisual {Root=Root("Relay shield "+index),Rings=new LineRenderer[3]};
            s.Surface=Flat(new Color(Gold.r,Gold.g,Gold.b,.035f),true);
            var bubble=Shape("Protective bubble",PrimitiveType.Sphere,s.Root,s.Surface);
            bubble.localScale=Vector3.one*3.2f;
            for(int j=0;j<3;j++)
            {
                s.Rings[j]=Line("Shield arc",s.Root,new Color(Gold.r,Gold.g,Gold.b,.5f),.035f,true);
                s.Rings[j].transform.localScale=Vector3.one*1.6f;
                s.Rings[j].transform.localRotation=Quaternion.Euler(j==0?0:90,j*60,0);
            }
            s.HeightGuide=Line("Elevated relay guide",transform,new Color(Gold.r,Gold.g,Gold.b,.45f),.035f,false);
            s.HeightGuide.useWorldSpace=true;
            s.OrbitGuide=Line("Authored circle path",transform,new Color(Gold.r,Gold.g,Gold.b,.35f),.03f,true);
            s.OrbitGuide.transform.position=StoryTimeline.RelayPositions[index];
            s.OrbitGuide.transform.localScale=Vector3.one*StoryTimeline.RelayOrbitRadius;
            s.OrbitGuide.enabled=false;
            s.Root.gameObject.SetActive(false);s.HeightGuide.enabled=false;return s;
        }
        public void Render(StoryFrame frame)
        {
            RenderedExplosionCount=RenderedShieldCount=0;
            foreach(var e in explosions) e.Root.gameObject.SetActive(false);
            for(int i=0;i<3;i++)
            {
                var pose=frame.Drones[i+5];var shield=shields[i];
                bool active=pose.Visible&&pose.Shielded;
                shield.Root.gameObject.SetActive(active);shield.HeightGuide.enabled=active;
                shield.OrbitGuide.enabled=active&&pose.Position.y>=StoryTimeline.RelayAltitude-.1f;
                if(!active) continue;
                RenderedShieldCount++;
                shield.Root.position=pose.Position;
                shield.HeightGuide.SetPosition(0,new Vector3(pose.Position.x,DragonTailLandscape.GroundHeight(pose.Position.x,pose.Position.z)+.14f,pose.Position.z));
                shield.HeightGuide.SetPosition(1,pose.Position);
                shield.Surface.color=new Color(Gold.r,Gold.g,Gold.b,.035f);
                for(int j=0;j<shield.Rings.Length;j++)
                {
                    shield.Rings[j].transform.localRotation=Quaternion.Euler(j==0?0:90,j*60+frame.Time*12,0);
                    shield.Rings[j].startColor=shield.Rings[j].endColor=new Color(Gold.r,Gold.g,Gold.b,.5f);
                }
            }
            foreach(var cue in frame.Explosions)
            {
                if(cue.DroneId<0||cue.DroneId>=explosions.Length) continue;
                var e=explosions[cue.DroneId];float age=cue.Age;
                e.Root.gameObject.SetActive(true);e.Root.position=cue.Position;RenderedExplosionCount++;
                // Lift the expanding fire above the building roof so it stays readable.
                float lift=cue.IsMiss?0:.95f;
                float core=Mathf.Clamp01(1-age/.65f);
                e.Core.gameObject.SetActive(core>0);
                e.Core.localPosition=Vector3.up*lift;
                e.Core.localScale=Vector3.one*(.2f+core*1.6f);
                for(int j=0;j<e.Sparks.Length;j++)
                {
                    float a=(j*137.5f+cue.DroneId*27)*Mathf.Deg2Rad;
                    Vector3 direction=new Vector3(Mathf.Cos(a),.25f+(j%4)*.25f,Mathf.Sin(a));
                    float span=(1-Mathf.Exp(-age*3.2f))*(.8f+(j%3)*.45f);
                    e.Sparks[j].localPosition=Vector3.up*lift+direction*span;
                    e.Sparks[j].localRotation=Quaternion.Euler(age*250+j*30,age*190,age*150);
                    e.Sparks[j].localScale=Vector3.one*(.18f*Mathf.Clamp01(1-age/1.05f));
                }
                float smokeFade=Mathf.Clamp01(age/.17f)*Mathf.Clamp01(1-age/StoryTimeline.ExplosionDuration);
                for(int j=0;j<e.Smoke.Length;j++)
                {
                    float a=j*2.4f;float spread=.15f+age*.55f;
                    e.Smoke[j].localPosition=new Vector3(Mathf.Cos(a)*spread,lift+age*.9f+j*.13f,Mathf.Sin(a)*spread);
                    e.Smoke[j].localScale=Vector3.one*(.25f+age*.65f+(j%2)*.15f);
                    e.SmokeMaterials[j].color=new Color(.27f,.31f,.32f,smokeFade*.6f);
                }
                e.Ring.transform.localPosition=new Vector3(0,.21f-cue.Position.y,0);
                e.Ring.transform.localScale=Vector3.one*(.2f+age*2.2f);
                var ringColor=new Color(Fire.r,Fire.g,Fire.b,Mathf.Clamp01(1-age/1.2f));
                e.Ring.startColor=e.Ring.endColor=ringColor;
            }
        }
        void OnDestroy() {foreach(var material in materials) if(material) Destroy(material);}
    }
}
