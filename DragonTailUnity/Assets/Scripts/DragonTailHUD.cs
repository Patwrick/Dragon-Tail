using UnityEngine;

namespace DragonTail
{
    /// <summary>A native, resolution-independent playback interface for the authored story.</summary>
    public sealed class DragonTailHUD : MonoBehaviour
    {
        public DragonTailExperience Experience;

        private static readonly Color Ink = new Color32(14, 23, 34, 255);
        private static readonly Color Panel = new Color32(20, 32, 45, 255);
        private static readonly Color Card = new Color32(26, 40, 54, 255);
        private static readonly Color Rule = new Color32(43, 60, 74, 255);
        private static readonly Color White = new Color32(244, 241, 231, 255);
        private static readonly Color Muted = new Color32(153, 172, 184, 255);
        private static readonly Color Quiet = new Color32(105, 129, 145, 255);
        private static readonly Color Cyan = new Color32(104, 220, 202, 255);
        private static readonly Color Amber = new Color32(237, 195, 111, 255);
        private static readonly Color Coral = new Color32(255, 133, 105, 255);
        private static readonly Color CommsOrange = new Color32(255, 138, 31, 255);

        private static readonly string[] ChapterNames =
            { "ORDERS", "ADVANCE", "FIRST PASS", "RELEASE", "FOLLOW-UP", "COMPLETE" };
        private static readonly string[] ChapterTitles =
            { "One shared plan.", "Leave a trail.", "The first pass.", "Change roles.", "Repeat the first pass.", "Sequence complete." };
        private static readonly float[] ChapterStarts = { 0f, 3f, 27f, 38f, 44f, 58f };

        private Font interfaceFont;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle phaseTitleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle tinyStyle;
        private GUIStyle numberStyle;
        private GUIStyle buttonStyle;
        private GUIStyle invisibleSlider;
        private GUIStyle invisibleThumb;
        private bool stylesReady;

        private void EnsureStyles()
        {
            if (stylesReady) return;

            interfaceFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Avenir Next", "Helvetica Neue", "Arial" }, 18);
            if (interfaceFont == null)
                interfaceFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            titleStyle = MakeStyle(36, FontStyle.Bold);
            subtitleStyle = MakeStyle(18);
            phaseTitleStyle = MakeStyle(26, FontStyle.Bold);
            bodyStyle = MakeStyle(18);
            bodyStyle.wordWrap = true;
            labelStyle = MakeStyle(15, FontStyle.Bold);
            smallStyle = MakeStyle(13, FontStyle.Bold);
            tinyStyle = MakeStyle(12);
            numberStyle = MakeStyle(34, FontStyle.Bold);
            buttonStyle = MakeStyle(15, FontStyle.Bold);
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            buttonStyle.hover.textColor = White;
            buttonStyle.active.textColor = White;
            buttonStyle.focused.textColor = White;

            // The native control handles pointer capture and dragging; its visuals are drawn below.
            invisibleSlider = new GUIStyle { fixedHeight = 32 };
            invisibleSlider.padding = new RectOffset(0, 0, 0, 0);
            invisibleThumb = new GUIStyle { fixedWidth = 12, fixedHeight = 32 };
            stylesReady = true;
        }

        private GUIStyle MakeStyle(int size, FontStyle weight = FontStyle.Normal)
        {
            var style = new GUIStyle
            {
                font = interfaceFont,
                fontSize = size,
                fontStyle = weight,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            style.normal.textColor = White;
            return style;
        }

        private void OnGUI()
        {
            if (Experience == null || Experience.CurrentFrame == null) return;
            EnsureStyles();
            HandleKeyboard();
            HandleMapInput();

            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            int previousDepth = GUI.depth;
            GUI.depth = -10;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                new Vector3(Screen.width / 1440f, Screen.height / 900f, 1f));
            GUI.color = Color.white;

            DrawHeader();
            DrawSceneFrame();
            DrawStoryPanel();
            DrawPlayback();

            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        private void HandleKeyboard()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown || current.alt || current.command || current.control)
                return;

            switch (current.keyCode)
            {
                case KeyCode.Space:
                    Experience.TogglePlayback();
                    break;
                case KeyCode.R:
                    Experience.ResetPlayback();
                    break;
                case KeyCode.LeftArrow:
                    Experience.Seek(Mathf.Max(0f, Experience.Playhead - 3f));
                    break;
                case KeyCode.RightArrow:
                    Experience.Seek(Mathf.Min(StoryTimeline.Duration, Experience.Playhead + 3f));
                    break;
                default:
                    return;
            }
            current.Use();
        }

        private void HandleMapInput()
        {
            Event current=Event.current;
            Vector2 point=new Vector2(current.mousePosition.x*1440f/Screen.width,current.mousePosition.y*900f/Screen.height);
            if(!new Rect(20,170,1030,480).Contains(point))return;
            if(current.type==EventType.ScrollWheel)
            {
                Experience.ZoomView(Mathf.Exp(-current.delta.y*.08f));current.Use();
            }
            else if(current.type==EventType.MouseDrag&&(current.button==1||current.button==2))
            {
                Experience.PanView(current.delta);current.Use();
            }
        }

        private void DrawHeader()
        {
            Fill(new Rect(0, 0, 1440, 120), Ink);
            Text(new Rect(30, 19, 660, 19), "SWARM CHOREOGRAPHY / 01", smallStyle, Cyan);
            Text(new Rect(28, 37, 780, 48), "DRAGON TAIL", titleStyle, White);
            Text(new Rect(30, 85, 720, 24), "A relay chain. A stored plan replayed.", subtitleStyle, Muted);

            Fill(new Rect(1178, 42, 230, 36), Panel);
            Border(new Rect(1178, 42, 230, 36), Rule);
            Fill(new Rect(1194, 56, 7, 7), Cyan);
            Text(new Rect(1211, 43, 186, 34), "INTERACTIVE STORY", smallStyle, White);
        }

        private void DrawSceneFrame()
        {
            StoryFrame frame = Experience.CurrentFrame;
            Border(new Rect(20, 126, 1030, 576), Rule);
            // Compact labels occupy the frame edge, leaving the world itself unobscured.
            Fill(new Rect(36, 141, 129, 27), new Color32(14, 23, 34, 225));
            Fill(new Rect(47, 151, 6, 6), Experience.Playing ? Cyan : Amber);
            Text(new Rect(63, 140, 94, 28), Experience.Playing ? "PLAYING" : "PAUSED", tinyStyle, White);

            if (frame.RelayCount > 0)
            {
                Fill(new Rect(176, 141, 202, 27), new Color32(14, 23, 34, 235));
                Border(new Rect(187, 150, 9, 9), Amber);
                Text(new Rect(205, 140, 168, 28), "RELAYS / SHIELDED", smallStyle, Amber);
            }

            if(Button(new Rect(455,141,35,27),"−",Panel,White))Experience.ZoomView(1f/1.25f);
            if(Button(new Rect(496,141,121,27),"OVERVIEW",Panel,Muted))Experience.ResetView();
            if(Button(new Rect(623,141,35,27),"+",Panel,White))Experience.ZoomView(1.25f);
            if(Button(new Rect(824,141,208,27),Experience.ShowRanges?"RANGES ON":"RANGES OFF",
                new Color32(48,31,37,235),new Color32(230,121,125,255)))Experience.ShowRanges=!Experience.ShowRanges;

            Fill(new Rect(35,661,610,28),new Color32(14,23,34,235));
            Legend(47,"PRIMARY",Cyan,94);
            Legend(171,"RELAY",Amber,80);
            Legend(279,"FOLLOW-UP",Coral,120);
            Legend(421,"COMMS RANGE",new Color32(230,93,103,255),155);
            Text(new Rect(664,663,368,23),"SCROLL TO ZOOM / RIGHT-DRAG TO PAN",tinyStyle,Muted,TextAnchor.MiddleRight);
        }

        private void Legend(float x, string label, Color color, float width)
        {
            Fill(new Rect(x, 672, 7, 7), color);
            Text(new Rect(x + 15, 663, width, 24), label, tinyStyle, Muted);
        }

        private void DrawStoryPanel()
        {
            StoryFrame frame = Experience.CurrentFrame;
            int phase = Mathf.Clamp(frame.PhaseIndex, 0, ChapterNames.Length - 1);
            Color phaseColor = phase >= 3 ? Coral : phase == 1 ? Amber : Cyan;

            Fill(new Rect(1068, 126, 352, 576), Panel);
            Text(new Rect(1088, 145, 306, 21), (phase + 1).ToString("00") + " / " + ChapterNames[phase], smallStyle, phaseColor);
            Text(new Rect(1086, 172, 316, 39), ChapterTitles[phase], phaseTitleStyle, White);
            Text(new Rect(1088, 218, 300, 104), frame.Description, bodyStyle, Muted, TextAnchor.UpperLeft);

            Fill(new Rect(1088, 334, 312, 1), Rule);
            DrawCounter(new Rect(1088, 345, 95, 77), frame.PrimaryCount, "PRIMARY", Cyan);
            DrawCounter(new Rect(1196, 345, 91, 77), frame.RelayCount, "RELAY", Amber);
            DrawCounter(new Rect(1300, 345, 101, 77), frame.FollowUpCount, "FOLLOW-UP", Coral);

            Text(new Rect(1088, 437, 310, 18), "ORIGINAL ORDERS / OBJECTIVES", smallStyle, Quiet);
            for (int i = 0; i < 3; i++)
            {
                ObjectiveState state = frame.Objectives != null && i < frame.Objectives.Length
                    ? frame.Objectives[i] : ObjectiveState.Untouched;
                DrawObjective(new Rect(1088, 467 + i * 47, 312, 39), i, state);
            }

            Fill(new Rect(1088, 620, 312, 57), Ink);
            Color commsColor = frame.CommsActive ? CommsOrange : phase >= 3 ? Coral : Quiet;
            Fill(new Rect(1100, 635, 5, 26), commsColor);
            string commsTitle = phase == 5 ? "ALL THREE TARGETS CLEAR" : frame.CommsActive ? "TWO-WAY LINK" : phase >= 3 ? "STORED ORDERS REUSED" : "LINK STANDBY";
            string commsDetail = frame.RelayCount > 0 ? "Circling high / shields active" :
                phase == 5 ? "Stored attacks replayed on A, B and C." : frame.CommsActive ? "Orders out. Status back." :
                phase >= 3 ? "Repeat A, B and C. No new orders." : "Ready to begin the sequence.";
            Text(new Rect(1117, 626, 275, 23), commsTitle, smallStyle, commsColor);
            Text(new Rect(1117, 648, 275, 22), commsDetail, tinyStyle, Muted);
        }

        private void DrawCounter(Rect bounds, int value, string label, Color color)
        {
            Text(new Rect(bounds.x, bounds.y, bounds.width, 45), value.ToString("00"), numberStyle, color);
            Text(new Rect(bounds.x, bounds.y + 44, bounds.width, 24), label, tinyStyle, Muted);
        }

        private void DrawObjective(Rect bounds, int index, ObjectiveState state)
        {
            bool cleared = state == ObjectiveState.Cleared;
            Color color = cleared ? Cyan : state == ObjectiveState.Damaged ? Coral : Muted;
            Fill(bounds, Card);
            Fill(new Rect(bounds.x, bounds.y, 3, bounds.height), color);
            Text(new Rect(bounds.x + 16, bounds.y, 37, bounds.height), ((char)('A' + index)).ToString(), labelStyle, White);
            Text(new Rect(bounds.x + 56, bounds.y + 2, 210, 19), DragonTailObjectiveSite.Names[index], smallStyle, White);
            Text(new Rect(bounds.x + 56, bounds.y + 20, 180, 17),
                state == ObjectiveState.Untouched ? "UNTOUCHED" : state == ObjectiveState.Damaged ? "DAMAGED" : "CLEARED",
                tinyStyle, color);
            if (cleared)
            {
                Fill(new Rect(bounds.xMax - 33, bounds.y + 16, 11, 7), Cyan);
            }
            else
            {
                Border(new Rect(bounds.xMax - 33, bounds.y + 14, 11, 11), color);
            }
        }

        private void DrawPlayback()
        {
            Fill(new Rect(20, 726, 1400, 148), Panel);
            Border(new Rect(20, 726, 1400, 148), Rule);

            bool complete = Experience.Playhead >= StoryTimeline.Duration - 0.01f;
            string action = Experience.Playing ? "PAUSE" : complete ? "REPLAY" : "PLAY";
            if (Button(new Rect(40, 745, 143, 49), action, Cyan, Ink))
                Experience.TogglePlayback();
            if (Button(new Rect(194, 745, 80, 49), "RESET", Card, Muted))
                Experience.ResetPlayback();

            DrawSpeed(298, 0.5f, "0.5x");
            DrawSpeed(360, 1f, "1x");
            DrawSpeed(422, 2f, "2x");

            bool links = Experience.ShowLinks;
            if (Button(new Rect(505, 745, 114, 49), links ? "LINKS ON" : "LINKS OFF",
                links ? new Color32(65, 42, 24, 255) : Card, links ? CommsOrange : Muted))
                Experience.ShowLinks = !links;

            const float trackX = 646;
            const float trackWidth = 726;
            Text(new Rect(trackX, 737, 90, 22), FormatTime(Experience.Playhead), smallStyle, White);
            Text(new Rect(trackX + trackWidth - 90, 737, 90, 22), FormatTime(StoryTimeline.Duration), smallStyle, Quiet, TextAnchor.MiddleRight);
            Fill(new Rect(trackX, 776, trackWidth, 3), Rule);
            float progress = Mathf.Clamp01(Experience.Playhead / StoryTimeline.Duration);
            Fill(new Rect(trackX, 776, trackWidth * progress, 3), Cyan);
            for (int i = 1; i < ChapterStarts.Length - 1; i++)
            {
                float chapterX = trackX + trackWidth * ChapterStarts[i] / StoryTimeline.Duration;
                Fill(new Rect(chapterX, 771, 1, 13), Quiet);
            }
            Fill(new Rect(trackX + progress * (trackWidth - 12f), 770, 12, 15), White);
            float newPlayhead = GUI.HorizontalSlider(new Rect(trackX, 761, trackWidth, 32),
                Experience.Playhead, 0f, StoryTimeline.Duration, invisibleSlider, invisibleThumb);
            if (Mathf.Abs(newPlayhead - Experience.Playhead) > 0.001f)
                Experience.Seek(newPlayhead);

            Fill(new Rect(40, 809, 1360, 1), Rule);
            int phase = Mathf.Clamp(Experience.CurrentFrame.PhaseIndex, 0, 5);
            for (int i = 0; i < ChapterNames.Length; i++)
            {
                Rect chapterRect = new Rect(40 + i * 229f, 820, i == 5 ? 215 : 221, 35);
                bool current = i == phase;
                Color chapterColor = current ? White : Quiet;
                if (current)
                    Fill(new Rect(chapterRect.x, chapterRect.y, 3, chapterRect.height), i >= 3 ? Coral : Cyan);
                Text(new Rect(chapterRect.x + 12, chapterRect.y, 23, 35), (i + 1).ToString("00"), tinyStyle, current ? Cyan : Quiet);
                Text(new Rect(chapterRect.x + 44, chapterRect.y, chapterRect.width - 46, 35), ChapterNames[i], smallStyle, chapterColor);
                if (GUI.Button(chapterRect, GUIContent.none, GUIStyle.none))
                    Experience.Seek(ChapterStarts[i]);
            }

            Text(new Rect(30, 879, 1380, 19), "SPACE  Play / pause     R  Restart     LEFT / RIGHT  Skip 3s     Click a chapter or drag the timeline to explore",
                tinyStyle, Quiet, TextAnchor.MiddleCenter);
        }

        private void DrawSpeed(float x, float value, string label)
        {
            bool selected = Mathf.Abs(Experience.Speed - value) < 0.01f;
            Rect bounds = new Rect(x, 745, 55, 49);
            if (Button(bounds, label, selected ? new Color32(40, 65, 67, 255) : Card,
                selected ? Cyan : Muted))
                Experience.SetSpeed(value);
            if (selected)
                Fill(new Rect(bounds.x + 12, bounds.yMax - 6, bounds.width - 24, 2), Cyan);
        }

        private bool Button(Rect bounds, string label, Color background, Color foreground)
        {
            Fill(bounds, background);
            buttonStyle.normal.textColor = foreground;
            buttonStyle.hover.textColor = foreground;
            buttonStyle.active.textColor = foreground;
            bool pressed = GUI.Button(bounds, label, buttonStyle);
            if (bounds.Contains(Event.current.mousePosition))
                Border(bounds, new Color(foreground.r, foreground.g, foreground.b, 0.5f));
            return pressed;
        }

        private static string FormatTime(float seconds)
        {
            int wholeSeconds = Mathf.FloorToInt(Mathf.Clamp(seconds, 0f, StoryTimeline.Duration));
            return (wholeSeconds / 60).ToString("00") + ":" + (wholeSeconds % 60).ToString("00");
        }

        private static void Fill(Rect bounds, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void Border(Rect bounds, Color color)
        {
            Fill(new Rect(bounds.x, bounds.y, bounds.width, 1), color);
            Fill(new Rect(bounds.x, bounds.yMax - 1, bounds.width, 1), color);
            Fill(new Rect(bounds.x, bounds.y, 1, bounds.height), color);
            Fill(new Rect(bounds.xMax - 1, bounds.y, 1, bounds.height), color);
        }

        private static void Text(Rect bounds, string value, GUIStyle style, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            style.normal.textColor = color;
            style.alignment = alignment;
            GUI.Label(bounds, value, style);
        }
    }
}
