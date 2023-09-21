using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnimationCurveEditor
{
    class AnimationCurveEditor : MonoBehaviour
    {
        public const string version = "1.0.0";

        private bool isInit = false;
        public AnimationCurve curve { get; private set; }
        public int samplingRate = 50;

        private Material mat;
        public Rect rect { get => _rect; set => changeRect(value); }
        private Rect _rect;

        // B C
        // A D
        private Vector2 A { get => rect.position; }
        private Vector2 B { get => new Vector2(rect.position.x, rect.position.y + rect.height); }
        private Vector2 C { get => rect.position + rect.size; }
        private Vector2 D { get => new Vector2(rect.position.x + rect.width, rect.position.y); }

        public Color backgroundColor = Color.white;
        public Color headerColor = Color.gray;
        public Color graphColor = Color.green;
        public Color referenceLinesColor = Color.gray;
        public Color keyframeKeyColor = Color.blue;
        public Color keyframeTangentLineColor = Color.black;
        public Color keyframeTangentHandleColor = Color.red;

        public float max;
        public float min;
        public float increment;
        private float incrementScreenCoordinate { get => (increment / (max - min)) * (rect.height); }

        // windowControls
        Vector2? delta;
        private bool draggingWindow = false;
        private bool draggingHandle = false;
        private KeyframeCtrl.handleKind draggingHandleKind;
        private bool justRemovedkey = false;
        private bool hoverExpand = false;
        private bool draggingExpand = false;
        private List<KeyframeCtrl> keyframeCtrls = new List<KeyframeCtrl>();
        KeyframeCtrl draggingKeyframeCtrl;

        // IMGUI ui elements
        private bool drawTooltip;
        private KeyframeCtrl.handleKind tooltipKind;
        private KeyframeCtrl tooltipKeyframeCtrl;
        private float inputTime = 0;
        private float inputValue = 0;

        /// <summary>
        /// Initialize the Editor for an AnimationCurve
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="mat">Render material</param>
        /// <param name="pos">Inital position and size of the editor graph</param>
        /// <param name="max">maximum value the graph displays</param>
        /// <param name="min">minimum value the graph displays</param>
        /// <param name="lineIncrement">increment between horzontal lines</param>
        /// <returns></returns>
        public AnimationCurveEditor Init(AnimationCurve curve, Material mat, Rect pos, float max, float min, float lineIncrement)
        {
            if (curve.keys.Length < 2)
            {
                throw new Exception("AnimationCurve passed to AnimationCurveEditor need to have at least two keyframes");
            }

            this.rect = pos;
            this.curve = curve;
            this.mat = mat;
            this.max = max;
            this.min = min;
            this.increment = lineIncrement;
            isInit = true;
            updateKeyFrameControlList();
            return this;
        }

        /// <summary>
        /// Initialize the Editor for an AnimationCurve
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="pos">Inital position and size of the editor graph</param>
        /// <param name="max">maximum value the graph displays</param>
        /// <param name="min">minimum value the graph displays</param>
        /// <param name="lineIncrement">increment between horzontal lines</param>
        /// <returns></returns>
        public AnimationCurveEditor Init(AnimationCurve curve, Rect pos, float max, float min, float lineIncrement)
        {
            return Init(curve, new Material(Shader.Find("Hidden/Internal-Colored")), pos, max, min, lineIncrement);
        }


        /// <summary>
        /// Initialize the Editor for an AnimationCurve
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="pos">Inital position and size of the editor graph</param>
        /// <returns></returns>
        public AnimationCurveEditor Init(AnimationCurve curve, Rect pos)
        {
            float highest = 0;
            float lowest = 0;
            float increment = 0.5f;
            foreach (Keyframe k in curve.keys)
            {
                if (k.value > highest) highest = k.value;
                if (k.value < lowest) lowest = k.value;
            }
            while ((highest - lowest) / increment > 8) increment *= 2;
            highest += increment;
            lowest -= increment;

            return Init(curve, pos, highest, lowest, increment);
        }


        /// <summary>
        /// Initialize the Editor for an AnimationCurve
        /// </summary>
        /// <param name="curve"></param>
        /// <returns></returns>
        public AnimationCurveEditor Init(AnimationCurve curve)
        {
            return Init(curve, new Rect(200f, 200f, 500f, 500f));
        }

        public float getAnimationCurveLength()
        {
            return curve.keys[curve.keys.Length - 1].time;
        }

        private void changeRect(Rect v)
        {
            this._rect = v;
            // redraw
        }

        private void updateKeyFrameControlList()
        {
            keyframeCtrls.Clear();
            for (int i = 0; i < curve.keys.Length; i++)
            {
                keyframeCtrls.Add(new KeyframeCtrl(i, this));
            }
        }

        public void AddKeyframe(Keyframe key)
        {
            curve.AddKey(key);
            updateKeyFrameControlList();
        }

        public void RemoveKeyframe(int index)
        {
            curve.RemoveKey(index);
            updateKeyFrameControlList();
        }

        void Update()
        {
            Rect headerRect = new Rect(B.x, B.y + 10, rect.width, 25);
            Rect eatInputArea = new Rect(A.x - 10, A.y - 10, rect.width + 20, rect.height + 50);

            bool mouseOverHeader = headerRect.Contains(Input.mousePosition);
            bool mouseOver = rect.Contains(Input.mousePosition);

            if (mouseOverHeader
                && Event.current.type == EventType.MouseDrag
                && !(draggingHandle)
                && Event.current.button == 0)
            {
                if (!delta.HasValue) delta = (Vector2)Input.mousePosition - rect.position;
                draggingWindow = true;
            }
            if (Event.current.type == EventType.MouseUp)
            {
                draggingWindow = false;
                justRemovedkey = false;
                draggingHandle = false;
                draggingExpand = false;
                draggingKeyframeCtrl = null;
                delta = null;
            }
            if (draggingWindow)
            {
                if (!delta.HasValue) return;
                Vector2 m = (Vector2)Input.mousePosition - delta.Value;
                rect = new Rect(m, rect.size);
            }

            bool tooltip = false;
            // handles
            foreach (KeyframeCtrl ctrl in keyframeCtrls)
            {
                // # keys
                if (ctrl.getHandleRectKey().Contains(Input.mousePosition))
                {
                    drawTooltip = tooltip = true;
                    tooltipKind = KeyframeCtrl.handleKind.keyframe;
                    tooltipKeyframeCtrl = ctrl;
                    // key drag
                    if (Event.current.type == EventType.MouseDrag)
                    {
                        draggingKeyframeCtrl = ctrl;
                        draggingHandle = true;
                        draggingHandleKind = KeyframeCtrl.handleKind.keyframe;
                    }
                    // remove key on middle click
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 2)
                    {
                        RemoveKeyframe(ctrl.keyframeIndex);
                        justRemovedkey = true;
                        break;
                    }
                }
                // # tangents
                else if (ctrl.getHandleRectIn().HasValue && ctrl.getHandleRectIn().Value.Contains(Input.mousePosition))
                {
                    drawTooltip = tooltip = true;
                    tooltipKind = KeyframeCtrl.handleKind.intangent;
                    tooltipKeyframeCtrl = ctrl;
                    if (Event.current.type == EventType.MouseDrag && (Event.current.button == 1 || Event.current.button == 0))
                    {
                        draggingKeyframeCtrl = ctrl;
                        draggingHandle = true;
                        draggingHandleKind = KeyframeCtrl.handleKind.intangent;
                    }
                    else if (Event.current.type == EventType.MouseDown && Event.current.button == 2)
                    {
                        justRemovedkey = true;
                        ctrl.resetInTangent();
                    }
                }
                else if (ctrl.getHandleRectOut().HasValue && ctrl.getHandleRectOut().Value.Contains(Input.mousePosition))
                {
                    drawTooltip = tooltip = true;
                    tooltipKind = KeyframeCtrl.handleKind.outtangent;
                    tooltipKeyframeCtrl = ctrl;
                    if (Event.current.type == EventType.MouseDrag && (Event.current.button == 1 || Event.current.button == 0))
                    {
                        draggingKeyframeCtrl = ctrl;
                        draggingHandle = true;
                        draggingHandleKind = KeyframeCtrl.handleKind.outtangent;
                    }
                    else if (Event.current.type == EventType.MouseDown && Event.current.button == 2)
                    {
                        justRemovedkey = true;
                        ctrl.resetOutTangent();
                    }
                }
                else if (!tooltip)
                {
                    drawTooltip = false;
                    tooltipKeyframeCtrl = null;
                }

            }
            // move key on left drag
            if (draggingHandle && draggingHandleKind == KeyframeCtrl.handleKind.keyframe && Event.current.button == 0 && draggingKeyframeCtrl != null)
            {
                draggingKeyframeCtrl.setKeyHandle(Input.mousePosition);
            }

            // add tangent on right drag
            if (draggingHandle && draggingHandleKind == KeyframeCtrl.handleKind.keyframe && Event.current.button == 1 && draggingKeyframeCtrl != null)
            {
                if (Input.mousePosition.x < draggingKeyframeCtrl.getKeyHandleScreenCoordinate().x)
                {
                    draggingKeyframeCtrl.setInHandle(Input.mousePosition);
                }
                else if (Input.mousePosition.x > draggingKeyframeCtrl.getKeyHandleScreenCoordinate().x)
                {
                    draggingKeyframeCtrl.setOutHandle(Input.mousePosition);
                }
            }

            // move tangent handles on left drag
            if (draggingKeyframeCtrl != null)
            {
                if (draggingHandle && draggingHandleKind == KeyframeCtrl.handleKind.intangent) draggingKeyframeCtrl.setInHandle(Input.mousePosition);
                else if (draggingHandle && draggingHandleKind == KeyframeCtrl.handleKind.outtangent) draggingKeyframeCtrl.setOutHandle(Input.mousePosition);
            }


            // add key on middle mouseclick
            if (Event.current.type == EventType.MouseDown && Event.current.button == 2 && mouseOver && !justRemovedkey)
            {
                Vector2 screenPos = Input.mousePosition;
                float time = ((screenPos - rect.position).x / rect.width) * getAnimationCurveLength();
                float value = getCurveValueForScreenY(screenPos.y);
                AddKeyframe(new Keyframe(time, value));
            }

            if ((new Rect(D.x - 10, D.y - 10, 20, 20).Contains(Input.mousePosition)))
            {
                hoverExpand = true;
                if (Event.current.type == EventType.MouseDrag && Event.current.button == 0) draggingExpand = true;
            }
            else hoverExpand = false;

            if (draggingExpand)
            {
                rect = new Rect(new Vector2(B.x, Input.mousePosition.y), new Vector2(Input.mousePosition.x - B.x, B.y - Input.mousePosition.y));
            }

            // eat input for rest of frame
            if (eatInputArea.Contains(Input.mousePosition))
            {
                Input.ResetInputAxes();
            }
        }

        void OnGUI()
        {
            if (!isInit) return;

            GUIStyle textBlack = new GUIStyle(GUI.skin.label);
            textBlack.normal.textColor = Color.black;
            textBlack.alignment = TextAnchor.MiddleLeft;
            GUIStyle textWhiteSmall = new GUIStyle(GUI.skin.label);
            textWhiteSmall.normal.textColor = Color.white;
            textWhiteSmall.alignment = TextAnchor.MiddleLeft;
            textWhiteSmall.normal.background = Texture2D.blackTexture;
            textWhiteSmall.fontSize = 10;
            GUIStyle textWhite = new GUIStyle(GUI.skin.label);
            textWhite.normal.textColor = Color.white;
            textWhite.normal.background = Texture2D.blackTexture;
            textWhite.alignment = TextAnchor.MiddleRight;
            GUIStyle textWhiteRight = new GUIStyle(GUI.skin.label);
            textWhiteRight.normal.textColor = Color.white;
            textWhiteRight.alignment = TextAnchor.MiddleRight;
            textWhiteRight.normal.background = Texture2D.blackTexture;
            GUIStyle tooltipStyle = new GUIStyle(GUI.skin.box);
            tooltipStyle.alignment = TextAnchor.MiddleCenter;
            tooltipStyle.normal.textColor = Color.yellow;

            // draw ineractive UI
            if (GUI.Button(new Rect(C.x - 47, Screen.height - (C.y + 10 + 22), 45, 20), "EXIT"))
            {
                isInit = false;
            }
            GUI.Label(new Rect(B.x + 5, Screen.height - (B.y + 10 + 25), 200, 25), $"Animation Curve Editor v{version}", textBlack);

            GUI.Label(new Rect(A.x, Screen.height - (A.y - 12), 60, 20), "Time:", textWhiteRight);
            inputTime = float.Parse(GUI.TextField(new Rect(A.x + 65, Screen.height - (A.y - 10), 60, 25), inputTime.ToString("0.000")));

            GUI.Label(new Rect(A.x + 130, Screen.height - (A.y - 12), 60, 20), "Value:", textWhiteRight);
            inputValue = float.Parse(GUI.TextField(new Rect(A.x + 195, Screen.height - (A.y - 10), 60, 25), inputValue.ToString("0.000")));

            if (GUI.Button(new Rect(A.x + 260, Screen.height - (A.y - 10), 120, 25), "Add Keyframe"))
            {
                AddKeyframe(new Keyframe(inputTime, inputValue));
            }

            // draw graph value reference
            for (int i = 0; i <= ((max - min) / increment); i++)
            {
                GUI.Label(new Rect(D.x + 3, Screen.height - (D.y + incrementScreenCoordinate * i + 10), 30, 20), (increment * i).ToString("0.000"), textWhiteSmall);
            }
            GUI.Label(new Rect(D.x - 55, Screen.height - (D.y - 5), 50, 20), getAnimationCurveLength().ToString("0.0000"), textWhite);

            // info tooltips
            if (drawTooltip)
            {
                switch (tooltipKind)
                {
                    case KeyframeCtrl.handleKind.keyframe:
                        GUI.Label(new Rect(tooltipKeyframeCtrl.getKeyHandleScreenCoordinate().x - 105,
                                Screen.height - (tooltipKeyframeCtrl.getKeyHandleScreenCoordinate().y + 10 + 20),
                                100, 20),
                            $"Value: {tooltipKeyframeCtrl.keyframe.value.ToString("0.000")}",
                            tooltipStyle);
                        GUI.Label(new Rect(tooltipKeyframeCtrl.getKeyHandleScreenCoordinate().x - 105,
                                Screen.height - (tooltipKeyframeCtrl.getKeyHandleScreenCoordinate().y - 10),
                                100, 20),
                            $"Time: {tooltipKeyframeCtrl.keyframe.time.ToString("0.000")}",
                            tooltipStyle);
                        break;
                    case KeyframeCtrl.handleKind.intangent:
                        if (!tooltipKeyframeCtrl.getInHandleScreenCoordinate().HasValue) break;
                        GUI.Label(new Rect(tooltipKeyframeCtrl.getInHandleScreenCoordinate().Value.x - 105,
                                Screen.height - (tooltipKeyframeCtrl.getInHandleScreenCoordinate().Value.y + 10 + 20),
                                100, 20),
                            $"Slope: {tooltipKeyframeCtrl.keyframe.inTangent.ToString("0.000")}",
                            tooltipStyle);
                        GUI.Label(new Rect(tooltipKeyframeCtrl.getInHandleScreenCoordinate().Value.x - 105,
                                Screen.height - (tooltipKeyframeCtrl.getInHandleScreenCoordinate().Value.y - 10),
                                100, 20),
#if NEW
                            $"Weigth: {tooltipKeyframeCtrl.keyframe.inWeight.ToString("0.000")}",
#else
                            "Weigth: N/A",
#endif
                            tooltipStyle);
                        break;
                    case KeyframeCtrl.handleKind.outtangent:
                        if (!tooltipKeyframeCtrl.getOutHandleScreenCoordinate().HasValue) break;
                        GUI.Label(new Rect(tooltipKeyframeCtrl.getOutHandleScreenCoordinate().Value.x - 105,
                                Screen.height - (tooltipKeyframeCtrl.getOutHandleScreenCoordinate().Value.y + 10 + 20),
                                100, 20),
                            $"Slope: {tooltipKeyframeCtrl.keyframe.outTangent.ToString("0.000")}",
                            tooltipStyle);
                        GUI.Label(new Rect(tooltipKeyframeCtrl.getOutHandleScreenCoordinate().Value.x - 105,
                                Screen.height - (tooltipKeyframeCtrl.getOutHandleScreenCoordinate().Value.y - 10),
                                100, 20),
#if NEW
                            $"Weigth: {tooltipKeyframeCtrl.keyframe.outWeight.ToString("0.000")}",
#else
                            "Weigth: N/A",
#endif
                            tooltipStyle);
                        break;
                    default:
                        break;
                }
            }
        }

        void OnPostRender()
        {
            if (!isInit) return;

            // init GL
            GL.PushMatrix();
            mat.SetPass(0);
            GL.LoadOrtho();

            // draw header
            GL.Begin(GL.QUADS);
            GL.Color(headerColor);
            GL.Vertex(translate(B + new Vector2(0, 10)));
            GL.Vertex(translate(B + new Vector2(0, 35)));
            GL.Vertex(translate(C + new Vector2(0, 35)));
            GL.Vertex(translate(C + new Vector2(0, 10)));
            GL.End();

            // draw background
            GL.Begin(GL.QUADS);
            GL.Color(backgroundColor);
            GL.Vertex(translate(A));
            GL.Vertex(translate(B));
            GL.Vertex(translate(C));
            GL.Vertex(translate(D));
            GL.End();

            // draw reference lines
            GL.Begin(GL.LINES);
            GL.Color(referenceLinesColor);
            for (int i = 1; i < ((max - min) / increment); i++)
            {
                GL.Vertex(translate(new Vector3(A.x, A.y + incrementScreenCoordinate * i, 0)));
                GL.Vertex(translate(new Vector3(D.x, D.y + incrementScreenCoordinate * i, 0)));
            }
            GL.End();

            // draw animation curve graph
            List<Vector3> graphPoints = new List<Vector3>();
            for (int i = 0; i < samplingRate; i++)
            {
                graphPoints.Add(evaluateCurveScreenCoord(getAnimationCurveLength() * ((float)i / ((float)samplingRate - 1))));
            }
            GL.Begin(GL.QUADS);
            GL.Color(graphColor);
            for (int j = 0; j < graphPoints.Count - 1; j++)
            {
                drawLineWithWidth(graphPoints[j], graphPoints[j + 1], 3f);
            }
            GL.End();

            // draw keyframeHandles
            foreach (KeyframeCtrl ctrl in keyframeCtrls)
            {
                Vector3? _in = ctrl.getInHandleScreenCoordinate();
                Vector3? _out = ctrl.getOutHandleScreenCoordinate();

                // draw connetion lines
                if (_in.HasValue || _out.HasValue)
                {
                    GL.Begin(GL.LINES);
                    GL.Color(keyframeTangentLineColor);
                    if (_out.HasValue)
                    {
                        GL.Vertex(translate(ctrl.getKeyHandleScreenCoordinate()));
                        GL.Vertex(translate(_out.Value));
                    }
                    if (_in.HasValue)
                    {
                        GL.Vertex(translate(ctrl.getKeyHandleScreenCoordinate()));
                        GL.Vertex(translate(_in.Value));
                    }
                    GL.End();
                }

                // draw key

                if (ctrl.keyframeIndex == 0)
                {
                    GL.Begin(GL.TRIANGLES);
                    GL.Color(keyframeKeyColor);
                    GL.Vertex(translate(ctrl.getKeyHandleScreenCoordinate() + new Vector3(0, -ctrl.handleRadius, 0)));
                    GL.Vertex(translate(ctrl.getKeyHandleScreenCoordinate() + new Vector3(0, ctrl.handleRadius, 0)));
                    GL.Vertex(translate(ctrl.getKeyHandleScreenCoordinate() + new Vector3(ctrl.handleRadius, 0, 0)));
                }
                else if (ctrl.keyframeIndex == curve.length - 1)
                {
                    GL.Begin(GL.TRIANGLES);
                    GL.Color(keyframeKeyColor);
                    GL.Vertex(translate(ctrl.getKeyHandleScreenCoordinate() + new Vector3(0, -ctrl.handleRadius, 0)));
                    GL.Vertex(translate(ctrl.getKeyHandleScreenCoordinate() + new Vector3(-ctrl.handleRadius, 0, 0)));
                    GL.Vertex(translate(ctrl.getKeyHandleScreenCoordinate() + new Vector3(0, ctrl.handleRadius, 0)));
                }
                else
                {
                    GL.Begin(GL.QUADS);
                    GL.Color(keyframeKeyColor);
                    drawHandle(ctrl.getKeyHandleScreenCoordinate(), ctrl.handleRadius);
                }
                GL.End();

                // draw tangent handles
                if (_in.HasValue || _out.HasValue)
                {
                    GL.Begin(GL.QUADS);
                    GL.Color(keyframeTangentHandleColor);
                    if (_in.HasValue)
                    {
                        drawHandle(_in.Value, ctrl.handleRadius);
                    }
                    if (_out.HasValue)
                    {
                        drawHandle(_out.Value, ctrl.handleRadius);
                    }
                    GL.End();
                }
            }

            // draw expand
            if (hoverExpand || draggingExpand)
            {
                GL.Begin(GL.QUADS);
                GL.Color(headerColor);
                drawLineWithWidth(new Vector3(D.x - 10, D.y + 10, 0), new Vector3(D.x + 10, D.y - 10, 0), 10);
                GL.End();
            }

            // end GL
            GL.PopMatrix();
        }

        private void drawHandle(Vector3 screenCoord, float radius)
        {
            GL.Vertex(translate(screenCoord + new Vector3(-radius, 0, 0)));
            GL.Vertex(translate(screenCoord + new Vector3(0, radius, 0)));
            GL.Vertex(translate(screenCoord + new Vector3(radius, 0, 0)));
            GL.Vertex(translate(screenCoord + new Vector3(0, -radius, 0)));
        }

        private void drawLineWithWidth(Vector3 start, Vector3 end, float lineWidth)
        {
            Vector3 line = (end - start);
            Vector3 offsetV = (new Vector3(-line.y, line.x, 0)).normalized * lineWidth / 2;
            ;
            GL.Vertex(translate(start - offsetV));
            GL.Vertex(translate(start + offsetV));
            GL.Vertex(translate(end + offsetV));
            GL.Vertex(translate(end - offsetV));
        }

        private Vector3 translate(Vector3 s)
        {
            return translate((Vector2)s);
        }

        private Vector3 translate(Vector2 screenCoord)
        {
            return new Vector3(translateX(screenCoord.x), translateY(screenCoord.y), 0);
        }

        private float translateX(float screenX)
        {
            return screenX / Screen.width;
        }

        private float translateY(float screenY)
        {
            return screenY / Screen.height;
        }

        private Vector3 evaluateCurveScreenCoord(float time)
        {
            float v = curve.Evaluate(time);
            return new Vector3((A.x + (time / getAnimationCurveLength() * rect.width)),
                // y position = bottom in screenspace + ((value - lowest value) / increment) * increment in screenspace
                getScreenYForCurveValue(v)
                , 0);
        }

        private float getScreenYForCurveValue(float value)
        {
            return rect.y + (((value - min) / increment) * incrementScreenCoordinate);
        }

        private float getCurveValueForScreenY(float screenY)
        {
            return min + (((screenY - rect.y) / incrementScreenCoordinate) * increment);
        }

        private Vector2 screenToGUI(Vector2 screen)
        {
            return new Vector2(screen.x, Screen.height - screen.y);
        }

        class KeyframeCtrl
        {
            public enum handleKind
            {
                keyframe,
                intangent,
                outtangent
            }

            public readonly int keyframeIndex;
            public Keyframe keyframe { get => editor.curve.keys[keyframeIndex]; }
            private readonly AnimationCurveEditor editor;
            public float handleRadius = 10f;

            private int tangentWeightVectorLengthBasis { get => (int)editor.rect.width / 2; }

            public bool hasInTangent { get => keyframeIndex != 0; }
            public bool hasOutTangent { get => keyframeIndex != editor.curve.length - 1; }

            public KeyframeCtrl(int keyframeIndex, AnimationCurveEditor editor)
            {
                this.keyframeIndex = keyframeIndex;
                this.editor = editor;
            }

            // key
            public Vector3 getKeyHandleScreenCoordinate()
            {
                // V3(left + width * (keyTime / totalTime) | bottom + (value - lowest value) * increment in screenspace | 0)
                return new Vector3(editor.rect.x + (keyframe.time / editor.getAnimationCurveLength()) * editor.rect.width,
                    editor.getScreenYForCurveValue(keyframe.value)
                    , 0);
            }

            public void setKeyHandle(Vector2 screenPos)
            {
                if (!editor.rect.Contains(screenPos)) return;
                float time = keyframe.time;
                if (time != 0 || time == editor.getAnimationCurveLength()) time = ((screenPos - editor.rect.position).x / editor.rect.width) * editor.getAnimationCurveLength();
                float value = editor.getCurveValueForScreenY(screenPos.y);
#if NEW
                editor.curve.MoveKey(keyframeIndex,new Keyframe(time, value, keyframe.inTangent, keyframe.outTangent, keyframe.inWeight, keyframe.outWeight));
#else
                editor.curve.MoveKey(keyframeIndex, new Keyframe(time, value, keyframe.inTangent, keyframe.outTangent));
#endif
            }
            public Rect getHandleRectKey()
            {
                return new Rect((Vector2)getKeyHandleScreenCoordinate() - new Vector2(handleRadius, handleRadius), 2 * new Vector2(handleRadius, handleRadius));
            }

            // in
            public Vector3? getInHandleScreenCoordinate()
            {
#if NEW
                if (!hasInTangent || keyframe.inWeight == 0) return null;
                Vector2 handleV = new Vector2(1, keyframe.inTangent).normalized * tangentWeightVectorLengthBasis * keyframe.inWeight;
#else
                if (!hasInTangent) return null;
                Vector2 handleV = new Vector2(1, keyframe.inTangent).normalized * tangentWeightVectorLengthBasis;
#endif
                return getKeyHandleScreenCoordinate() - (Vector3)handleV;
            }

            public void setInHandle(Vector2 screenPos)
            {
                if (!hasInTangent) return;
                Vector2 handleV = screenPos - (Vector2)getKeyHandleScreenCoordinate();
                float tangent = handleV.y / handleV.x;
#if NEW
                float weight = handleV.magnitude / tangentWeightVectorLengthBasis;
                editor.curve.MoveKey(keyframeIndex, new Keyframe(keyframe.time, keyframe.value, tangent, keyframe.outTangent, weight, keyframe.outWeight));
#else
                editor.curve.MoveKey(keyframeIndex, new Keyframe(keyframe.time, keyframe.value, tangent, keyframe.outTangent));
#endif
            }
            public Rect? getHandleRectIn()
            {
                if (!getInHandleScreenCoordinate().HasValue) return null;
                return new Rect((Vector2)getInHandleScreenCoordinate() - new Vector2(handleRadius, handleRadius), 2 * new Vector2(handleRadius, handleRadius));
            }
            public void resetInTangent()
            {
#if NEW
                editor.curve.MoveKey(keyframeIndex, new Keyframe(keyframe.time, keyframe.value, keyframe.inTangent, keyframe.outTangent, 0, keyframe.outWeight));
#else
                editor.curve.MoveKey(keyframeIndex, new Keyframe(keyframe.time, keyframe.value, keyframe.inTangent, keyframe.outTangent));
#endif
            }

            // out
            public Vector3? getOutHandleScreenCoordinate()
            {
#if NEW
                if (keyframe.outWeight == 0 || !hasOutTangent) return null;
                Vector2 handleV = new Vector2(1, keyframe.outTangent).normalized * tangentWeightVectorLengthBasis * keyframe.outWeight;
#else
                if (!hasOutTangent) return null;
                Vector2 handleV = new Vector2(1, keyframe.outTangent).normalized * tangentWeightVectorLengthBasis;
#endif
                return getKeyHandleScreenCoordinate() + (Vector3)handleV;
            }

            public void setOutHandle(Vector2 screenPos)
            {
                if (!hasOutTangent) return;
                Vector2 handleV = screenPos - (Vector2)getKeyHandleScreenCoordinate();
                float weight = handleV.magnitude / tangentWeightVectorLengthBasis;
                float tangent = handleV.y / handleV.x;
#if NEW
                editor.curve.MoveKey(keyframeIndex, new Keyframe(keyframe.time, keyframe.value, keyframe.inTangent, tangent, keyframe.inWeight, weight));
#else
                editor.curve.MoveKey(keyframeIndex, new Keyframe(keyframe.time, keyframe.value, keyframe.inTangent, tangent));
#endif
            }
            public Rect? getHandleRectOut()
            {
                if (!getOutHandleScreenCoordinate().HasValue) return null;
                return new Rect((Vector2)getOutHandleScreenCoordinate() - new Vector2(handleRadius, handleRadius), 2 * new Vector2(handleRadius, handleRadius));
            }
            public void resetOutTangent()
            {
#if NEW
                editor.curve.MoveKey(keyframeIndex, new Keyframe(keyframe.time, keyframe.value, keyframe.inTangent, keyframe.outTangent, keyframe.inWeight, 0));
#else
                editor.curve.MoveKey(keyframeIndex, new Keyframe(keyframe.time, keyframe.value, keyframe.inTangent, keyframe.outTangent));
#endif
            }
        }
    }
}
