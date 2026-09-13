using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace DragonTail
{
    /// <summary>Authored game animation. The playhead is the only source of story state.</summary>
    public sealed class DragonTailExperience : MonoBehaviour
    {
        public StoryFrame CurrentFrame { get; private set; }
        public float Playhead { get; private set; }
        public float Speed { get; private set; } = 1f;
        public bool Playing { get; private set; }
        public bool ShowLinks { get; set; } = true;
        public bool ShowRanges { get; set; } = true;
        public float ViewZoom { get; private set; } = 1f;
        Vector3 viewFocus = new Vector3(0, 3, 0);
        static readonly Vector3 CameraOffset = new Vector3(-15, 76, -119);
        const float BaseSignalHeight = 2.8f;
        public Camera SceneCamera { get; private set; }

        static readonly Color Cyan = Hex("68DCCA"), Amber = Hex("EDC36F"), Coral = Hex("FF8569"), CommsOrange = Hex("FF8A1F");
        readonly Transform[] drones = new Transform[8];
        readonly Transform[] rotors = new Transform[32];
        readonly Renderer[] droneLights = new Renderer[8];
        readonly LineRenderer[] trails = new LineRenderer[8];
        readonly LineRenderer[] shadows = new LineRenderer[8];
        readonly DragonTailObjectiveSite[] sites = new DragonTailObjectiveSite[3];
        readonly LineRenderer[] objectiveRings = new LineRenderer[3];
        readonly Transform[] packets = new Transform[8];
        readonly LineRenderer[] links = new LineRenderer[4];
        readonly List<Material> ownedMaterials = new List<Material>();
        Material dark, body, cyan, amber, coral, muted, stone, commsOrange, commsGlow;
        DragonTailLandscape landscape;
        DragonTailEffects effects;
        DragonTailCommsRanges ranges;
        Font labelFont;
        GUIStyle labelStyle;
        int runtimeErrors;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            Application.logMessageReceived += CountErrors;
            ConfigureLaunchWindow();
            CreateMaterials();
            CreateCameraAndLight();
            landscape = gameObject.AddComponent<DragonTailLandscape>();
            landscape.Build();
            CreateRelayMarkers();
            CreateBase();
            CreateObjectives();
            CreateDrones();
            CreateEffects();
            gameObject.AddComponent<DragonTailHUD>().Experience = this;
            ApplyFrame();
            string[] args = Environment.GetCommandLineArgs();
            int captureIndex = Array.IndexOf(args, "--capture-dir");
            if (captureIndex >= 0 && captureIndex + 1 < args.Length)
            {
                StartCoroutine(CaptureReview(args[captureIndex + 1]));
            }
        }

        void ConfigureLaunchWindow()
        {
            if (Application.isEditor) return;
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-screen-width") >= 0 || Array.IndexOf(args, "-screen-height") >= 0) return;
            RectInt area = Screen.mainWindowDisplayInfo.workArea;
            float fit = area.width > 0 && area.height > 0
                ? Mathf.Min(1f, (area.width - 48f) / 1600f, (area.height - 64f) / 1000f) : 1f;
            int width = Mathf.RoundToInt(1600 * fit), height = Mathf.RoundToInt(1000 * fit);
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            Debug.Log("DRAGON_TAIL_WINDOW requested=" + width + "x" + height + " workArea=" + area);
            StartCoroutine(PlaceLaunchWindow());
        }

        IEnumerator PlaceLaunchWindow()
        {
            // Resolution changes finish at the end of a frame. Reposition after that,
            // since a saved origin can put the enlarged window outside the display.
            yield return null;
            yield return new WaitForEndOfFrame();
            var display = Screen.mainWindowDisplayInfo;
            var area = display.workArea;
            yield return Screen.MoveMainWindowTo(display, new Vector2Int(area.x + 24, area.y + 32));
            Debug.Log("DRAGON_TAIL_WINDOW actual=" + Screen.width + "x" + Screen.height +
                " position=" + Screen.mainWindowPosition + " display=" + display.width + "x" + display.height);
        }

        void CountErrors(string message, string trace, LogType kind)
        {
            if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) runtimeErrors++;
        }

        void Update()
        {
            if (Playing)
            {
                Playhead = Mathf.Min(StoryTimeline.Duration, Playhead + Time.unscaledDeltaTime * Speed);
                if (Playhead >= StoryTimeline.Duration) Playing = false;
            }
            ApplyFrame();
        }

        public void TogglePlayback()
        {
            if (Playhead >= StoryTimeline.Duration) Playhead = 0;
            Playing = !Playing;
        }
        public void ResetPlayback() { Playing = false; Playhead = 0; ApplyFrame(); }
        public void Seek(float seconds) { Playing = false; Playhead = Mathf.Clamp(seconds, 0, StoryTimeline.Duration); ApplyFrame(); }
        public void SetSpeed(float speed) { Speed = Mathf.Clamp(speed, .25f, 4f); }

        public void ZoomView(float factor)
        {
            ViewZoom=Mathf.Clamp(ViewZoom*factor,1f,3.5f);
            if(ViewZoom<=1f)viewFocus=new Vector3(0,3,0);
            ApplyCameraView();
        }
        public void PanView(Vector2 delta)
        {
            if(ViewZoom<=1f)return;
            Vector3 right=SceneCamera.transform.right;right.y=0;right.Normalize();
            Vector3 forward=Vector3.Cross(right,Vector3.up).normalized;
            viewFocus+=(-right*delta.x+forward*delta.y)*(.16f/ViewZoom);
            viewFocus.x=Mathf.Clamp(viewFocus.x,-53,53);viewFocus.z=Mathf.Clamp(viewFocus.z,-28,28);ApplyCameraView();
        }
        public void ResetView(){ViewZoom=1;viewFocus=new Vector3(0,3,0);ApplyCameraView();}
        void ApplyCameraView()
        {
            SceneCamera.transform.position=viewFocus+CameraOffset;SceneCamera.transform.LookAt(viewFocus);SceneCamera.fieldOfView=38f/ViewZoom;
        }

        void CreateMaterials()
        {
            dark = Material("172129");
            body = Material("D7DFD9"); stone = Material("66716E");
            cyan = Material(Cyan, true); amber = Material(Amber, true); coral = Material(Coral, true);
            muted = Material("477675");
            commsOrange = Material(CommsOrange,true); commsGlow = Material(Hex("FFD18F"),true);
        }
        Material Material(string color) { return Material(Hex(color), false); }
        Material Material(Color color, bool unlit)
        {
            var material = new Material(Shader.Find(unlit ? "Unlit/Color" : "Standard"));
            material.color = color;
            if (!unlit) { material.SetFloat("_Glossiness", .25f); material.SetFloat("_Metallic", .12f); }
            ownedMaterials.Add(material); return material;
        }
        static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var c); return c; }

        void CreateCameraAndLight()
        {
            SceneCamera = new GameObject("Overview camera").AddComponent<Camera>();
            SceneCamera.transform.position = viewFocus + CameraOffset;
            SceneCamera.transform.LookAt(viewFocus);
            SceneCamera.orthographic = false; SceneCamera.fieldOfView = 38f;
            SceneCamera.rect = new Rect(.015f, .22f, .715f, .64f);
            SceneCamera.clearFlags = CameraClearFlags.SolidColor;
            SceneCamera.backgroundColor = Hex("1C2B34"); SceneCamera.farClipPlane = 400;
            var background = new GameObject("Background camera").AddComponent<Camera>();
            background.depth = -2; background.clearFlags = CameraClearFlags.SolidColor;
            background.backgroundColor = Hex("0B141D"); background.cullingMask = 0;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.51f, .59f, .66f);
            var key = new GameObject("Afternoon light").AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.05f;
            key.color = new Color(1f, .97f, .90f); key.transform.rotation = Quaternion.Euler(47, -35, 0);
            key.shadows = LightShadows.Soft; key.shadowStrength = .6f;
            QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowDistance = 220;
            QualitySettings.antiAliasing = 4;
        }

        void CreateRelayMarkers()
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = StoryTimeline.RelayPositions[i]; p.y = DragonTailLandscape.GroundHeight(p.x,p.z)+.12f;
                Ring("Checkpoint " + (i + 1), p, 1.15f, .04f, Hex("96815B"));
                for (int j = 0; j < 4; j++)
                {
                    float a = j * Mathf.PI / 2;
                    Primitive("Checkpoint tick", PrimitiveType.Cube,
                        p + new Vector3(Mathf.Cos(a)*1.45f, .04f, Mathf.Sin(a)*1.45f),
                        new Vector3(.13f,.12f,.13f), amber);
                }
            }
        }

        void CreateBase()
        {
            Vector3 p=StoryTimeline.BasePosition;
            var canvas=Material("425D3F");var bags=Material("64705A");var equipment=Material("38423A");
            var fabric=Material("576B48");var skin=Material("9C806A");
            // A low field camp tucked behind the authored ridge. All pieces are scenery.
            var roof=Primitive("Low woodland shelter",PrimitiveType.Cube,p+new Vector3(-.8f,1.95f,.8f),new Vector3(5.4f,.1f,3.8f),canvas);
            roof.rotation=Quaternion.Euler(0,12,-7);
            for(int i=0;i<9;i++)
            {
                float x=-2.7f+(i%3)*1.55f,z=-.3f+(i/3)*1.05f;
                var patch=Primitive("Cloth color panel",PrimitiveType.Cube,p+new Vector3(x,2.02f-(x+.8f)*.12f,z),new Vector3(1.1f,.025f,.7f),i%2==0?fabric:equipment);
                patch.rotation=Quaternion.Euler(0,12,-7);
            }
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                Primitive("Shelter support",PrimitiveType.Cylinder,p+new Vector3(-.8f+x*2.45f,.95f,.8f+z*1.55f),new Vector3(.07f,.95f,.07f),stone);
            Primitive("Rear cloth screen",PrimitiveType.Cube,p+new Vector3(-.8f,1.04f,2.43f),new Vector3(5,.1f,1.8f),canvas).rotation=Quaternion.Euler(90,0,0);
            Primitive("Folding equipment table",PrimitiveType.Cube,p+new Vector3(.15f,.76f,.4f),new Vector3(1.65f,.08f,.9f),equipment);
            for(int x=-1;x<=1;x+=2)
                Primitive("Table legs",PrimitiveType.Cube,p+new Vector3(.15f+x*.65f,.38f,.4f),new Vector3(.06f,.74f,.75f),stone);
            for(int i=0;i<2;i++)
            {
                float x=-.4f+i*.8f;
                Primitive("Laptop base",PrimitiveType.Cube,p+new Vector3(x,.84f,.35f),new Vector3(.6f,.035f,.4f),dark);
                var screen=Primitive("Dim control display",PrimitiveType.Cube,p+new Vector3(x,1.02f,.56f),new Vector3(.58f,.35f,.03f),muted);screen.rotation=Quaternion.Euler(-12,0,0);
                Primitive("Field chair",PrimitiveType.Cube,p+new Vector3(x,.36f,-.65f),new Vector3(.5f,.1f,.5f),equipment);
                Primitive("Seated controller",PrimitiveType.Capsule,p+new Vector3(x,.64f,-.65f),new Vector3(.35f,.31f,.33f),fabric);
                Primitive("Controller head",PrimitiveType.Sphere,p+new Vector3(x,1.04f,-.61f),new Vector3(.24f,.27f,.24f),skin);
                Primitive("Field cap",PrimitiveType.Sphere,p+new Vector3(x,1.16f,-.61f),new Vector3(.28f,.1f,.28f),equipment);
                for(int side=-1;side<=1;side+=2)
                {
                    Primitive("Seated legs",PrimitiveType.Cube,p+new Vector3(x+side*.1f,.37f,-.42f),new Vector3(.13f,.15f,.48f),fabric);
                    Primitive("Controller sleeves",PrimitiveType.Cube,p+new Vector3(x+side*.2f,.73f,-.34f),new Vector3(.12f,.12f,.54f),fabric);
                }
            }
            for(int i=0;i<10;i++)
            {
                var bag=Primitive("Low earthbag edging",PrimitiveType.Sphere,p+new Vector3(-3.25f+(i%5)*1.35f,.24f,i<5?-1.65f:2.75f),new Vector3(1.3f,.45f,.7f),bags);
                bag.rotation=Quaternion.Euler(0,(i%3-1)*9,0);
            }
            for(int i=0;i<3;i++)
                Primitive("Equipment case",PrimitiveType.Cube,p+new Vector3(-2.6f,.32f,-.65f+i*.65f),new Vector3(.75f,.6f,.55f),equipment);
            Primitive("Compact camp antenna",PrimitiveType.Cylinder,p+Vector3.up*1.42f,new Vector3(.045f,1.4f,.045f),stone);
            Primitive("Antenna tip",PrimitiveType.Cube,p+Vector3.up*BaseSignalHeight,new Vector3(.32f,.04f,.05f),stone);
        }

        void CreateObjectives()
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = StoryTimeline.ObjectivePositions[i];
                var root = new GameObject("Objective " + (char)('A' + i));
                root.transform.position = p;
                sites[i] = root.AddComponent<DragonTailObjectiveSite>();
                sites[i].Build(i);
                objectiveRings[i] = Ring("Objective perimeter", p + Vector3.up*.13f, 3.15f, .045f, Coral);
            }
        }

        void CreateDrones()
        {
            var shell=Material("B8C1BF");shell.SetFloat("_Glossiness",.4f);
            var frame=Material("263136");frame.SetFloat("_Metallic",.3f);
            var metal=Material("727F82");metal.SetFloat("_Metallic",.65f);metal.SetFloat("_Glossiness",.45f);
            var glass=Material("1F3F4C");glass.SetFloat("_Metallic",.3f);glass.SetFloat("_Glossiness",.78f);
            for(int i=0;i<8;i++)
            {
                var root=new GameObject("Drone D"+(i+1).ToString("00")).transform;drones[i]=root;
                var model=root.gameObject.AddComponent<DragonTailDroneModel>();model.Build(shell,frame,metal,glass,cyan);
                droneLights[i]=model.RoleLight;
                for(int j=0;j<4;j++) rotors[i*4+j]=model.Rotors[j];
                root.localScale=Vector3.one*1.25f;
                trails[i]=Line("Motion trail",new Vector3[16],.04f,Cyan);
                shadows[i]=Ring("Drone ground marker",Vector3.zero,.55f,.03f,Cyan);
            }
        }

        void CreateEffects()
        {
            for(int i=0;i<links.Length;i++) links[i]=Line("Communication link",new Vector3[2],.065f,CommsOrange);
            for(int i=0;i<packets.Length;i++) packets[i]=Primitive("Message pulse",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.19f,i%2==0?commsGlow:commsOrange);
            effects=gameObject.AddComponent<DragonTailEffects>();
            ranges=gameObject.AddComponent<DragonTailCommsRanges>();
        }

        void ApplyFrame()
        {
            CurrentFrame=StoryTimeline.Sample(Playhead);
            var nextFrame=StoryTimeline.Sample(Mathf.Min(Playhead+.08f,StoryTimeline.Duration));
            var history=new StoryFrame[16];
            for(int j=0;j<history.Length;j++) history[j]=StoryTimeline.Sample(Mathf.Max(0,Playhead-j*.08f));
            for(int i=0;i<8;i++)
            {
                var pose=CurrentFrame.Drones[i]; drones[i].position=pose.Position;
                drones[i].gameObject.SetActive(pose.Visible);
                var next=nextFrame.Drones[i].Position;
                var delta=next-pose.Position; delta.y=0;
                if(delta.sqrMagnitude>.0001f) drones[i].rotation=Quaternion.LookRotation(delta,Vector3.up);
                else drones[i].rotation=Quaternion.Euler(0,90,0);
                var role=pose.Role;
                var m=role==DroneRole.Relay?amber:role==DroneRole.FollowUp?coral:role==DroneRole.Finished?muted:cyan;
                var color=m.color;droneLights[i].sharedMaterial=m;
                for(int j=0;j<4;j++) rotors[i*4+j].localRotation=Quaternion.Euler(0,Playhead*1700+i*25+j*40,0);
                for(int j=0;j<16;j++) trails[i].SetPosition(j,history[j].Drones[i].Position);
                trails[i].startColor=color;trails[i].endColor=new Color(color.r,color.g,color.b,.03f);
                trails[i].enabled=pose.Visible&&role!=DroneRole.Finished;
                shadows[i].enabled=pose.Visible;
                shadows[i].transform.position=new Vector3(pose.Position.x,DragonTailLandscape.GroundHeight(pose.Position.x,pose.Position.z)+.1f,pose.Position.z);
                shadows[i].startColor=shadows[i].endColor=color*.65f;
            }
            for(int i=0;i<3;i++)
            {
                var state=CurrentFrame.Objectives[i];
                sites[i].SetState(state);
                var color=state==ObjectiveState.Cleared?Cyan:state==ObjectiveState.Damaged?Amber:Coral;
                objectiveRings[i].startColor=objectiveRings[i].endColor=color;
            }
            DrawLinks();
            effects.Render(CurrentFrame);
            ranges.Render(CurrentFrame,ShowRanges);
        }

        void DrawLinks()
        {
            var points=new List<Vector3>{StoryTimeline.BasePosition+Vector3.up*BaseSignalHeight};
            for(int station=0;station<3;station++)
            {
                // Stable tail identities: D06, D07, D08 at checkpoints 01, 02, 03.
                int unit=5+station;
                if(CurrentFrame.Drones[unit].Role==DroneRole.Relay) points.Add(CurrentFrame.Drones[unit].Position);
            }
            Vector3 centroid=Vector3.zero;int count=0;
            for(int i=0;i<8;i++) if(CurrentFrame.Drones[i].Role==DroneRole.Primary){centroid+=CurrentFrame.Drones[i].Position;count++;}
            if(count>0) points.Add(centroid/count);
            bool visible=ShowLinks&&CurrentFrame.CommsActive&&points.Count>1;
            for(int i=0;i<links.Length;i++)
            {
                bool show=visible&&i<points.Count-1;links[i].enabled=show;
                packets[i*2].gameObject.SetActive(show);packets[i*2+1].gameObject.SetActive(show);
                if(!show) continue;
                links[i].SetPosition(0,points[i]);links[i].SetPosition(1,points[i+1]);
                float progress=Mathf.Repeat(Playhead*.65f+i*.18f,1);
                packets[i*2].position=Vector3.Lerp(points[i],points[i+1],progress);
                packets[i*2+1].position=Vector3.Lerp(points[i+1],points[i],Mathf.Repeat(progress+.42f,1));
            }
        }

        void OnGUI()
        {
            if(CurrentFrame==null||SceneCamera==null) return;
            if(labelStyle==null)
            {
                labelFont=Font.CreateDynamicFontFromOSFont(new[]{"Avenir Next","Helvetica Neue","Arial"},14);
                labelStyle=new GUIStyle(GUI.skin.label){font=labelFont,fontSize=12,alignment=TextAnchor.MiddleCenter};
            }
            Matrix4x4 old=GUI.matrix;
            GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1440f,Screen.height/900f,1));
            WorldLabel(StoryTimeline.BasePosition+new Vector3(0,0,-4),"HILLSIDE CAMP",Cyan,140);
            for(int i=0;i<3;i++)
            {
                var s=CurrentFrame.Objectives[i];
                WorldLabel(StoryTimeline.ObjectivePositions[i]+new Vector3(0,3.5f,0),((char)('A'+i)).ToString(),s==ObjectiveState.Cleared?Cyan:s==ObjectiveState.Damaged?Amber:Coral,28);
                if(Playhead<38) WorldLabel(new Vector3(StoryTimeline.RelayPositions[i].x,DragonTailLandscape.GroundHeight(StoryTimeline.RelayPositions[i].x,-3.5f),-3.5f),"0"+(i+1),Hex("A4ACA2"),30);
            }
            for(int i=0;i<8;i++)
            {
                var role=CurrentFrame.Drones[i].Role;
                if(role==DroneRole.Relay)
                {
                    WorldLabel(drones[i].position+Vector3.up*2.1f,"D"+(i+1).ToString("00")+" / HIGH",Amber,112);
                }
            }
            GroupLabel(DroneRole.Primary,"PRIMARY / "+CurrentFrame.PrimaryCount.ToString("00"),Cyan,130);
            GroupLabel(DroneRole.FollowUp,"REPEAT PASS",Coral,210);
            GUI.matrix=old;
        }
        void GroupLabel(DroneRole role,string text,Color color,float width)
        {
            Vector3 center=Vector3.zero;int count=0;
            var ids=new List<string>();
            for(int i=0;i<8;i++) if(CurrentFrame.Drones[i].Role==role){center+=drones[i].position;count++;ids.Add("D"+(i+1).ToString("00"));}
            if(role==DroneRole.FollowUp) text+=" / "+string.Join(" ",ids);
            float labelHeight=role==DroneRole.Primary?8f:5.3f;
            if(count>0) WorldLabel(center/count+Vector3.up*labelHeight,text,color,width);
        }
        void WorldLabel(Vector3 world,string text,Color color,float width)
        {
            Vector3 point=SceneCamera.WorldToScreenPoint(world);
            float x=point.x*1440/Screen.width,y=(Screen.height-point.y)*900/Screen.height;
            if(x<20||x>1050||y<130||y>710) return;
            var rect=new Rect(x-width/2,y-10,width,20);
            GUI.color=new Color(.035f,.07f,.095f,.88f);GUI.DrawTexture(rect,Texture2D.whiteTexture);
            GUI.color=Color.white;labelStyle.normal.textColor=color;GUI.Label(rect,text,labelStyle);
        }

        Transform Primitive(string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.position=position;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;
            var collider=go.GetComponent<Collider>();if(collider) Destroy(collider);
            return go.transform;
        }
        LineRenderer Line(string name,Vector3[] points,float width,Color color)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();
            line.sharedMaterial=new Material(Shader.Find("Sprites/Default"));ownedMaterials.Add(line.sharedMaterial);
            line.positionCount=points.Length;line.SetPositions(points);line.widthMultiplier=width;
            line.startColor=line.endColor=color;line.numCapVertices=3;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            return line;
        }
        LineRenderer Ring(string name,Vector3 center,float radius,float width,Color color)
        {
            var points=new Vector3[64];for(int i=0;i<points.Length;i++){float a=i*Mathf.PI*2/points.Length;points[i]=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);}
            var line=Line(name,points,width,color);line.useWorldSpace=false;line.loop=true;line.transform.position=center;return line;
        }
        [Serializable]
        sealed class RuntimeReport
        {
            public bool passed, play, pause, speed, replay, reset;
            public bool renderLifecycle, relayLinks, shieldEffects, rewindVisuals;
            public int runtimeErrors, screenshots;
        }

        bool RenderedStateMatches()
        {
            int protectedCount=0,visibleCount=0;
            for(int i=0;i<8;i++)
            {
                var pose=CurrentFrame.Drones[i];
                if(drones[i].gameObject.activeSelf!=pose.Visible) return false;
                if(!pose.Visible&&(trails[i].enabled||shadows[i].enabled)) return false;
                if(pose.Shielded) protectedCount++;
                if(pose.Visible) visibleCount++;
            }
            for(int i=0;i<sites.Length;i++)
                if(sites[i].State!=CurrentFrame.Objectives[i]) return false;
            return effects.RenderedShieldCount==protectedCount && effects.RenderedExplosionCount==CurrentFrame.Explosions.Length && ranges.VisibleCount==(ShowRanges?visibleCount:0);
        }

        bool LinkEndpointsMatch()
        {
            var expected=new List<Vector3>{StoryTimeline.BasePosition+Vector3.up*BaseSignalHeight};
            for(int i=5;i<8;i++) if(CurrentFrame.Drones[i].Role==DroneRole.Relay) expected.Add(CurrentFrame.Drones[i].Position);
            Vector3 center=Vector3.zero;int count=0;
            for(int i=0;i<8;i++) if(CurrentFrame.Drones[i].Role==DroneRole.Primary){center+=CurrentFrame.Drones[i].Position;count++;}
            if(count>0) expected.Add(center/count);
            for(int i=0;i<links.Length;i++)
            {
                bool active=ShowLinks&&CurrentFrame.CommsActive&&i<expected.Count-1;
                if(links[i].enabled!=active) return false;
                if(active&&(Vector3.Distance(links[i].GetPosition(0),expected[i])>.001f||Vector3.Distance(links[i].GetPosition(1),expected[i+1])>.001f)) return false;
            }
            return true;
        }

        IEnumerator CaptureReview(string directory)
        {
            Directory.CreateDirectory(directory);
            yield return null; yield return new WaitForEndOfFrame();
            // Exercise the same public playback controls used by the HUD.
            ResetPlayback(); TogglePlayback();
            yield return new WaitForSecondsRealtime(.25f);
            bool advanced=Playhead>0;
            TogglePlayback();float paused=Playhead;
            yield return new WaitForSecondsRealtime(.15f);
            bool pauseWorks=Mathf.Approximately(paused,Playhead);
            SetSpeed(2);bool speedWorks=Mathf.Approximately(Speed,2);SetSpeed(1);
            bool lifecycleWorks=true, linksWork=true, shieldWorks=true;
            float[] moments={0,10.75f,14.2f,51.2f,26,30.45f,34.45f,37.3f,42,48.2f,54.5f,58};
            for(int i=0;i<moments.Length;i++)
            {
                Seek(moments[i]);yield return null;yield return new WaitForEndOfFrame();
                lifecycleWorks &= RenderedStateMatches(); linksWork &= LinkEndpointsMatch();
                if(moments[i]>=14.2f&&moments[i]<=14.7f) shieldWorks &= effects.RenderedShieldCount==1;
                Texture2D screenshot=ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(directory,i.ToString("00")+"-"+moments[i].ToString("00.0",System.Globalization.CultureInfo.InvariantCulture)+".png"),screenshot.EncodeToPNG());
                Destroy(screenshot);
            }
            TogglePlayback();bool replayWorks=Playing&&Playhead==0;ResetPlayback();
            bool resetWorks=!Playing&&Playhead==0&&CurrentFrame.PrimaryCount==8&&RenderedStateMatches();
            Seek(26);Vector3 orbitPosition=drones[5].position;
            Seek(54.5f);bool gone=CurrentFrame.DestroyedCount==8&&RenderedStateMatches();
            Seek(26);bool rewindWorks=gone&&CurrentFrame.DestroyedCount==0&&RenderedStateMatches()&&Vector3.Distance(drones[5].position,orbitPosition)<.001f;
            ResetPlayback();
            bool pass=advanced&&pauseWorks&&speedWorks&&replayWorks&&resetWorks&&lifecycleWorks&&linksWork&&shieldWorks&&rewindWorks&&runtimeErrors==0;
            var result=new RuntimeReport {passed=pass,play=advanced,pause=pauseWorks,speed=speedWorks,replay=replayWorks,reset=resetWorks,renderLifecycle=lifecycleWorks,relayLinks=linksWork,shieldEffects=shieldWorks,rewindVisuals=rewindWorks,runtimeErrors=runtimeErrors,screenshots=moments.Length};
            string report=JsonUtility.ToJson(result,true);
            File.WriteAllText(Path.Combine(directory,"runtime-checks.json"),report);
            Debug.Log("DRAGON_TAIL_RUNTIME "+report);
            Application.Quit(pass?0:1);
        }
        void OnDestroy()
        {
            Application.logMessageReceived-=CountErrors;
            foreach(var material in ownedMaterials) if(material) Destroy(material);
            if(labelFont) Destroy(labelFont);
        }
    }
}
