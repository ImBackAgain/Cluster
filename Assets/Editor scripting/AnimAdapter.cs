using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using Consts = Utils.Constants;

public class AnimAdapter : EditorWindow
{
    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

    const string MODEL_PATH = "Assets/Player/FBX Imports/";
    const string ANIMATION_PATH = "Assets/Player/Animations/";
    const string INPUT_SEP = ", "; //Used in the text fields of this window
    string fileName = "";
    string clipName = "";
    string objName = "";

    //When stringifying a perm, the result consists of six separate ‘swaps’ in order.
    //They are separated by this.
    const char SWAP_SEP = ':';
    //Each set consists of a target body part (number), followed by the ‘future position’
    //coordinate values.  They are separated by this.
    const char DATA_SEP = '/';


    class CurveIngredients
    {
        public string relativePath;
        public Type type;
        public string propertyName;
        public AnimationCurve curve;
        public CurveIngredients(string relativePath, Type type, string propertyName, AnimationCurve curve)
        {
            this.relativePath = relativePath;
            this.type = type;
            this.propertyName = propertyName;
            this.curve = curve;
        }
    }

    [MenuItem("Window/UI Toolkit/AnimAdapter")]
    public static void ShowExample()
    {
        AnimAdapter wnd = GetWindow<AnimAdapter>();
        wnd.titleContent = new GUIContent("Animation adapter");
    }


    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // Instantiate UXML
        VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
        root.Add(labelFromUXML);


        root.Q<TextField>("fileNameInput").
            RegisterCallback<ChangeEvent<string>>(OnFileNameInput);
        root.Q<TextField>("clipNameInput").
            RegisterCallback<ChangeEvent<string>>(OnClipNameInput);
        root.Q<TextField>("objNameInput").
            RegisterCallback<ChangeEvent<string>>(OnObjetNameInput);



        root.Q<Button>("buttonSpec")
            .RegisterCallback<ClickEvent>(OnClickCopySpec);

        root.Q<Button>("buttonAllClips")
            .RegisterCallback<ClickEvent>(OnClickCopyAll);

        root.Q<Button>("buttonCont")
            .RegisterCallback<ClickEvent>(OnClickCont);


        root.Q<Button>("button2").RegisterCallback<ClickEvent>(DoNonsense);
        root.Q<Button>("button3").RegisterCallback<ClickEvent>(DoMoreNonsense);

        fileName = "Art";
    }

    private void OnFileNameInput(ChangeEvent<string> evt)
    {
        fileName = evt.newValue;
    }
    private void OnClipNameInput(ChangeEvent<string> evt)
    {
        clipName = evt.newValue;
    }
    private void OnObjetNameInput(ChangeEvent<string> evt)
    {
        objName = evt.newValue;
    }


    private void OnClickCopyAll(ClickEvent evt)
    {
        CopyAnimsFromFile(true);
    }

    private void OnClickCopySpec(ClickEvent evt)
    {
        CopyAnimsFromFile(false);
    }


    void CopyAnimsFromFile(bool allClips)
    {
        //TODO: we loop through every clip in the file for every clipName in the input
        foreach (string fName in fileName.Split(INPUT_SEP))
        {
            foreach (string cName in clipName.Split(INPUT_SEP))
            {
                string newName = cName;
                foreach (string oName in objName.Split(INPUT_SEP))
                {
                    string filePath = MODEL_PATH + fName + ".fbx";

                    string fullClipName = oName == "" ? cName : (oName + "|" + cName);

                    Debug.Log("Copying from file ‘" + filePath + "’ clip ‘" + fullClipName + "’?");

                    Thingy(filePath);

                    //Remember to double-check that the file isn’t a scene asset when you start looping through files.

                    AnimationClip[] clipAssets = AssetDatabase.LoadAllAssetsAtPath(filePath).OfType<AnimationClip>().ToArray();

                    foreach (AnimationClip clip in clipAssets)
                    {
                        if (allClips && !clip.name.StartsWith("__preview__") && !clip.name.EndsWith("Builder"))
                        {
                            CreatePermutantCopy(clip);
                        }
                        else if (clip.name == fullClipName)
                        {
                            CreatePermutantCopy(clip);
                            break;
                        }
                    }
                    //End of loops
                }
            }
        }
    }

    
    /// <summary>
    /// Doesn’t work
    /// </summary>
    /// <param name="filePath"></param>
    private void Thingy(string filePath)
    {
        ModelImporter i = AssetImporter.GetAtPath(filePath) as ModelImporter;

        Debug.Log(i);

        i.bakeAxisConversion = true;
        i.SaveAndReimport();
    }
    

    void CreatePermutantCopy(AnimationClip source)
    {
        Debug.Log("Found clip " + source);

        string newFilePath = ANIMATION_PATH + source.name.Split("|").Last() + ".anim";

        if (AssetDatabase.AssetPathExists(newFilePath))
        {
            Debug.Log("Aborting because clip already exists: " + newFilePath);
        }
        else
        {
            AnimationClip copy = new();

            // We have four tasks, sort of.  Three and a half, maybe.
            //
            // First, we’ll recreate every curve with the same binding in the new clip.
            //
            // Then we will record every ‘Perm’ keyframe.  While doing so, we will
            // read the Perm’s position curves to find the times and permutations for
            // each permutation event.  There’s more, but we’ll get to it.
            //
            // The next task is to find the ‘future positions’ for each permutation.
            // This is the bone positions on the frame after the event, for the bone
            // that a sphere will be following after the permutation is complete.
            //
            // This means recording all the bones’ key frames on our first pass
            // through so we can refer to them again when setting up the events.
            //
            // Finally, we want to try adding a very tightly spaced key on the pre-
            // transition side that goes all the way up to the post-transition position.

            //This holds the times and permutations for the events
            Dictionary<float, Vector3> permFrames = new();

            //This will hold all the main bone’s positions, to get us the
            //future positions for the events
            AnimationCurve[,] boneCurves = new AnimationCurve[6, 3];


            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(source);
            //curveData = new CurveIngredients[bindings.Length];

            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding binding = bindings[i];
                AnimationCurve curve = new AnimationCurve();
                curve.CopyFrom(AnimationUtility.GetEditorCurve(source, binding));

                string boneName = binding.path;
                string channelName = binding.propertyName;

                //curveData[i] = new(boneName, binding.type, channelName, curve);

                //All permutation things onnly care about m_LocalPosition curves

                if (channelName.StartsWith("m_LocalPosition"))
                {
                    int axis = channelName.Last() - 'x';
                    //Debug.Log("Perm curve? ‘" + boneName + "’ prop ‘" + channelName + "’");
                    //Debug.Log("Last character was " + channelName.Last() + "; selector is " + axis);

                    //Get permutations and times
                    if (boneName == "Perm")
                    {
                        //Debug.Log(binding.type);
                        foreach (Keyframe key in curve.keys)
                        {
                            if (permFrames.TryGetValue(key.time, out Vector3 p))
                            {
                                p[axis] = key.value;
                                permFrames[key.time] = p;
                            }
                            else
                            {
                                p[axis] = key.value;
                                permFrames.Add(key.time, p);
                            }
                        }
                    }
                    else
                    {
                        for (int j = 0; j < 6; j++)
                        {
                            if (channelName.StartsWith("m_LocalPosition") && boneName == ((BodyPart)j).ToString())
                            {
                                //Debug.Log("Core bone curve. ‘" + boneName + "’ prop ‘" + channelName + "’");
                                boneCurves[j, axis] = curve;
                                break;
                            }
                        }
                    }
                }
                //End of loop over curves.
            }

            List<AnimationEvent> permutationEvents = new();

            foreach (float time in permFrames.Keys)
            {
                Vector3 coords = permFrames[time];
                if (coords.sqrMagnitude > 0)
                {
                    AnimationEvent evt = AddPermutationStuff(coords, time, boneCurves);
                    permutationEvents.Add(evt);
                }
            }

            AnimationUtility.SetAnimationEvents(copy, permutationEvents.ToArray());

            copy.ClearCurves();
            for (int i = 0; i < 6; i++)
            {
                string path = ((BodyPart)i).ToString();

                //EditorCurveBinding binder = new EditorCurveBinding();
                //binder.path = Cluster.boneNames[i];
                
                for (int j = 0; j < 3; j++)
                {
                    AnimationCurve curve = boneCurves[i, j];

                    string prop = "m_LocalPosition." + (char)('x' + j);

                    copy.SetCurve(path, typeof(Transform), prop, curve);

                    //binder.propertyName = "m_LocalPosition." + (char)('x' + j);
                    //binder.type = typeof(Transform);

                    //AnimationUtility.SetEditorCurve(copy, binder, curve);
                }
            }


            if (source.name.EndsWith("Cyc"))
            {
                AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(source);
                s.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(copy, s);
            }

            AssetDatabase.CreateAsset(copy, newFilePath);
            Debug.Log("Copy created at " + newFilePath);

        }
    }

    /// <summary>
    /// Modifies all permuted curves to include an ‘almost’ keyframe very close to
    /// the permutation moment.  Also creates an animation event with the same time
    /// value.
    /// </summary>
    /// <param name="permPos"></param>
    /// <param name="time"></param>
    /// <param name="boneCurves"></param>
    /// <returns></returns>
    AnimationEvent AddPermutationStuff(Vector3 permPos, float time, AnimationCurve[,] boneCurves)
    {
        //First we figure out what parts are actually swapping.
        //Remember that Unity’s axes are weird and Blender’s aren’t.
        string dests = string.Format("{0:D2}{1:D2}{2:D2}", (int)permPos.x, (int)permPos.z, (int)permPos.y);

        //Debug.Log(dests);

        float almostTime = time - Consts.ALMOST_TIME;


        //Time to start adding the ‘almost’ frames and constructing the string argument
        //for the event handler.  Same loop because uhhh they can.
        string fullArg = "";

        for (int i = 0; i < dests.Length; i++)
        {
            if (i != 0) fullArg += SWAP_SEP;

            int targ = dests[i] - '0';

            if (targ != 0)
            {
                targ = (targ + i) % 6;
                fullArg += targ;

                for (int j = 0; j < 3; j++)
                {
                    AnimationCurve sc = boneCurves[i, j];
                    AnimationCurve dc = boneCurves[targ, j];

                    float futureCoordinate = dc.Evaluate(time);

                    fullArg += DATA_SEP;
                    fullArg += futureCoordinate;

                    //And now all the weird stuff.
                    //We’ll only affect the source curve here.  We’ll do everything
                    InsertBrokenKeyframe(sc, almostTime, futureCoordinate);
                }
            }
        }

        //TODO: instead of this, try adding an almost keyframe at the transition moment.
        AnimationEvent evt = new();
        evt.time = almostTime;
        evt.functionName = "PermEvent";
        evt.stringParameter = fullArg;

        return evt;
    }

    private void InsertBrokenKeyframe(AnimationCurve curve, float time, float value)
    {
        FindKeyframeIndex(time, curve, out int prInd, out int neInd);

        Keyframe before = curve.keys[prInd];
        Keyframe after = curve.keys[neInd];
        Keyframe between = new Keyframe(time, value);

        float slope = (value - before.value) / (time - before.time);
        before.outTangent = slope;
        between.inTangent = slope;
        curve.RemoveKey(prInd);
        curve.AddKey(before);

        slope = (after.value - value) / (after.time - time);
        after.inTangent = slope;
        between.outTangent = Mathf.Infinity;
        curve.RemoveKey(neInd);
        curve.AddKey(after);

        curve.AddKey(between);
    }

    static int FindKeyframeIndex(float time, AnimationCurve c, out int prInd, out int neInd)
    {
        float closestTimeBefore = Mathf.NegativeInfinity, closestTimeAfter = Mathf.Infinity;
        int beforeInd = -1, afterInd = -1, thisInd = -1;

        for (int i = 0; i < c.keys.Length; i++)
        {
            float t = c.keys[i].time;

            if (t == time)
            {
                thisInd = i;
            }
            else if (t < time && t > closestTimeBefore)
            {
                closestTimeBefore = t;
                beforeInd = i;
            }
            else if (t > time && t < closestTimeAfter)
            {
                closestTimeAfter = t;
                afterInd = i;
            }
        }

        prInd = beforeInd;
        neInd = afterInd;
        return thisInd;
    }

    /// <summary>
    /// Inverts the process of CreateAnimationEvent.
    /// </summary>
    /// <param name="asSent"></param>
    /// <param name="permData"></param>
    /// <exception cref="ArgumentException"></exception>
    public static List<Cluster.PermData> DestringifyPermEvent(string asSent)
    {
        List<Cluster.PermData> output = new();

        Debug.Log("Destringify " + asSent);
        string[] split = asSent.Split(SWAP_SEP);

        if (split.Length != 6)
        {
            throw new ArgumentException("Permutation in bad format: " + asSent +
                "\nSplt into " + split[split.Length] + " parts: " + split);
        }

        for (int i = 0; i < split.Length; i++)
        {
            if (split[i] != "")
            {
                string[] splitter = split[i].Split(DATA_SEP);

                int targNum = int.Parse(splitter[0]);

                if (targNum == i) Debug.LogError("Permutation event says to permute with self.  Good luck.");

                Cluster.PermData data = new()
                {
                    src = i,
                    dst = targNum,
                    fPos = new Vector3(
                        float.Parse(splitter[1]),
                        float.Parse(splitter[2]),
                        float.Parse(splitter[3])
                        )
                };

                output.Add(data);
            }
        }

        return output;
    }

    private void OnClickCont(ClickEvent evt)
    {
        AnimatorController controller = 
            AssetDatabase.LoadMainAssetAtPath(ANIMATION_PATH + "Player AniCon.controller")
            as AnimatorController;

        foreach(AnimatorControllerLayer l in controller.layers)
        {
            Debug.Log("Layer " + l.name);

            foreach(ChildAnimatorState s in l.stateMachine.states)
            {
                Debug.Log("State name " + s.state.name);

                if (s.state.motion == null)
                {
                    string n = s.state.name;

                    AnimationClip clip =
                        AssetDatabase.LoadMainAssetAtPath(ANIMATION_PATH + n + ".anim")
                        as AnimationClip;


                    if (clip != null)
                    {
                        Debug.Log("Found clip " + clip);
                        s.state.motion = clip;
                    }
                    else
                    {
                        Debug.Log("Failed to find clip " + n);
                    }
                }
                else
                {
                    Debug.Log("Motion present " + s.state.motion);
                }
            }
        }
    }


    #region Testing
    void DoNonsense(ClickEvent _)
    {
        var l = AssetDatabase.LoadMainAssetAtPath("Assets/FBX Imports/Thingy.anim") as AnimationClip;
        var b = AnimationUtility.GetCurveBindings(l)[0];
        var c = AnimationUtility.GetEditorCurve(l, b);

        var k1 = c.keys[1];
        var k2 = c.keys[2];


        c.RemoveKey(1);
        k1.outTangent = 1;
        c.AddKey(k1);

        c.RemoveKey(2);
        k2.inTangent = 1;
        c.AddKey(k2);

        var k = new Keyframe(150, -50);
        k.inTangent = k.outTangent = 1;
        c.AddKey(k);

        PrintKeys(c);

        l.SetCurve("", typeof(Transform), "m_LocalPosition.x", c);
    }

    void PrintKeys(AnimationCurve c)
    {
        string form = "Key #{7} at time {0}, value {1}\n" +
            "Tangents {2}/{3}\n" +
            "Weights {4}/{5}\n" +
            "WeightedMode {6}";

        for (int i = 0; i < c.keys.Length; i++)
        {
            Keyframe k = c.keys[i];
            Debug.Log(string.Format(form,
                k.time, k.value,
                k.inTangent, k.outTangent,
                k.inWeight, k.outWeight,
                k.weightedMode, i
                ));
        }
    }

    void DoMoreNonsense(ClickEvent _)
    {
        var l = AssetDatabase.LoadMainAssetAtPath("Assets/FBX Imports/Thingy.anim") as AnimationClip;
        var b = AnimationUtility.GetCurveBindings(l)[0];
        var c = AnimationUtility.GetEditorCurve(l, b);

        PrintKeys(c);
    }

    #endregion
}
