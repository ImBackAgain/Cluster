using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Consts = Utils.Constants;

public class AnimAdapter : EditorWindow
{
    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

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

        root.Q<Button>("button1")
            .RegisterCallback<ClickEvent>(OtherOnButtonClick);


        root.Q<TextField>("fileNameInput").
            RegisterCallback<ChangeEvent<string>>(OnFileNameInput);
        root.Q<TextField>("clipNameInput").
            RegisterCallback<ChangeEvent<string>>(OnClipNameInput);
        root.Q<TextField>("objNameInput").
            RegisterCallback<ChangeEvent<string>>(OnObjetNameInput);


        root.Q<Button>("button2").RegisterCallback<ClickEvent>(DoNonsense);
        root.Q<Button>("button3").RegisterCallback<ClickEvent>(DoMoreNonsense);

        fileName = "Art";
    }

    const string MODEL_PATH = "Assets/FBX Imports/";
    string fileName = "";
    string clipName = "";
    string objName = "";

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

    const string INPUT_SEP = ", ";

    private void OtherOnButtonClick(ClickEvent evt)
    {
        //TODO: we loop through every clip in the file for every clipName in the input
        foreach (string fName in fileName.Split(INPUT_SEP))
        {
            foreach (string cName in clipName.Split(INPUT_SEP))
            {
                foreach (string oName in objName.Split(INPUT_SEP))
                {
                    string filePath = MODEL_PATH + fName + ".fbx";

                    string fullClipName = oName + "|" + cName;

                    Debug.Log("So file ‘" + filePath + "’ clip ‘" + fullClipName + "’?");

                    //Remember to double-check that the file isn’t a scene asset when you start looping through files.

                    AnimationClip[] clipAssets = AssetDatabase.LoadAllAssetsAtPath(filePath).OfType<AnimationClip>().ToArray();

                    foreach (AnimationClip clip in clipAssets)
                    {
                        if (clip.name == fullClipName)
                        {
                            Debug.Log("Found clip " + fullClipName);

                            string newFilePath = MODEL_PATH + cName + ".anim";

                            if (AssetDatabase.AssetPathExists(newFilePath))
                            {
                                Debug.Log("Aborting because clip already exists: " + newFilePath);
                            }
                            else
                            {
                                AnimationClip copy = CreateCopyWithPerms(clip);

                                if (cName.EndsWith("Cyc"))
                                {
                                    AnimationUtility.SetAnimationClipSettings(copy, new AnimationClipSettings() { loopTime = true });
                                }


                                AssetDatabase.CreateAsset(copy, newFilePath);
                                Debug.Log("Copy created");
                            }

                            break;
                        }
                    }
                    //End of loops
                }
            }
        }
    }

    /// <summary>
    /// Returns an Animation clip that contains all the data as the given clip,
    /// plus events correponding to permutations.  Look here if the function that
    /// shuold be called by said events has changed in any way.
    /// </summary>
    /// <param name="originalClip"></param>
    /// <returns></returns>
    AnimationClip CreateCopyWithPerms(AnimationClip originalClip)
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

        // This is for recreating the existing curves
        //CurveIngredients[] curveData;

        //This holds the times and permutations for the events
        Dictionary<float, Vector3> permFrames = new();

        //This will hold all the main bone’s positions, to get us the
        //future positions for the events
        AnimationCurve[,] boneCurves = new AnimationCurve[6, 3];


        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(originalClip);
        //curveData = new CurveIngredients[bindings.Length];

        for (int i = 0; i < bindings.Length; i++)
        {
            EditorCurveBinding binding = bindings[i];
            AnimationCurve curve = new AnimationCurve();
            curve.CopyFrom(AnimationUtility.GetEditorCurve(originalClip, binding));

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
                    Debug.Log(binding.type);
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
                    for (int j = 0; j < Cluster.boneNames.Length; j++)
                    {
                        if (channelName.StartsWith("m_LocalPosition") && boneName == Cluster.boneNames[j])
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

        {
            foreach (float time in permFrames.Keys)
            {
                Vector3 coords = permFrames[time];
                if (coords.sqrMagnitude > 0)
                {
                    AnimationEvent evt = AddPermutationStuff(coords, time, boneCurves);
                    permutationEvents.Add(evt);
                }
            }
        }

        AnimationUtility.SetAnimationEvents(copy, permutationEvents.ToArray());

        //foreach (var c in curveData)
        //{
        //    copy.SetCurve(c.relativePath, c.type, c.propertyName, c.curve);
        //}

        copy.ClearCurves();
        for (int i = 0; i < 6; i++)
        {
            EditorCurveBinding binder = new EditorCurveBinding();
            binder.path = Cluster.boneNames[i];
            //copy.SetCurve(path, typeof(Transform), "m_LocalPosition", new AnimationCurve());
            for (int j = 0; j < 3; j++)
            {
                AnimationCurve curve = boneCurves[i, j];
                binder.propertyName = "m_LocalPosition." + (char)('x' + j);
                binder.type = typeof(Transform);

                AnimationUtility.SetEditorCurve(copy, binder, curve);
                //copy.SetCurve(path, typeof(Transform), prop, curve);
            }
        }

        return copy;
    }

    //The string consists of six sets of arguments for the BeginTransition method
    //separated by this
    const char SWAP_SEP = ':';

    //Each set consists of 
    const char DATA_SEP = '/';

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
    /*
    void OnButtonClick(ClickEvent evt)
    {
        string msg = "Loading assets at " + fileName + "\n";

        string[] allGUIDs = AssetDatabase.FindAssets("t:object", new[] { "Assets/" + fileName });

        foreach (string gUID in allGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(gUID);
            //Debug.Log("Opening file at path " + path);
            //ModelImporterStuff(path);

            msg += "\nFile path" + path + LoadAssetStuff(path);
        }

        Debug.Log(msg);
    }

    void ModelImporterStuff(string path)
    {
        ModelImporter mimp = AssetImporter.GetAtPath(path) as ModelImporter;

        if (mimp != null)
        {
            foreach (var clip in mimp.clipAnimations)
            {
                Debug.Log(clip.name);
                clip.name = "Boo " + clip.name;
            }
        }
    }

    string LoadAssetStuff(string path)
    {
        string msg = "";

        if (AssetDatabase.LoadMainAssetAtPath(path) is SceneAsset)
        {
            return "Loaded scene at " + path;
        }

        var clipAssets = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToArray();

        foreach (AnimationClip clip in clipAssets)
        {
            if (clip.name.StartsWith("__preview__"))
            {
                msg += clip.name + " is a preview\n";
            }
            else
            {
                msg += clip.name + " is not a preview\n";

                var bindings = AnimationUtility.GetCurveBindings(clip);

                foreach (EditorCurveBinding binding in bindings)
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);

                    //Keyframe x = curve.keys[0];
                    //
                    //x.value = 32;
                    //
                    //curve.MoveKey(0, x);

                    Debug.Log($"Binding: {binding}\nPath: {binding.path}\nProp: {binding.propertyName}");

                    //AnimationUtility.SetEditorCurve(clip, binding, curve);
                }
            }
        }

        return msg;
    }
    */

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
