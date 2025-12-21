using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Rand = UnityEngine.Random;

/// <summary>
/// Represents a position in the body
/// </summary>
public enum BodyPart
{
    A, B, X, Y, T, H
}

/// <summary>
/// Represents a sphere state, or (for the
/// first six) a specific sphere instance
/// </summary>
public enum SphereColour
{
    Waves, Hills, Zig, Snarl, Sun, Spin, Default
}

public class Cluster : MonoBehaviour
{
    [SerializeField]
    Sphere[] spheresByColour;

    [SerializeField]
    Sphere[] spheresByPosition;
    
    [SerializeField]
    Transform[] bones;
    /*public static readonly string[] boneNames = 
        new string[] { "Foot L", "Foot R", "Hand L", "Hand R", "Torso", "Head" };*/


    void Start()
    {
        Debug.Log("Hi!  Look over here for some comments about immediate tasks.");
        /* The idle animation has indecipherable problems.
         * The exported clip works fine.  When I make a copy of it, the result is
         * bugged in some inscrutable way that makes it refuse to play.
         */
        spheresByColour = new Sphere[6];
        spheresByPosition = new Sphere[6];

        ClustAnEve model = GetComponentInChildren<ClustAnEve>();
        model.SetParent(this);

        PopulateBoneArray();

        GameObject partPref = 
            AssetDatabase.LoadMainAssetAtPath("Assets/Player/Prefabs/Part.prefab")
            as GameObject;

        for (int i = 0; i < spheresByColour.Length; i++)
        {

            GameObject partObj = Instantiate(partPref);
            spheresByColour[i] = spheresByPosition[i]
                = partObj.GetComponent<Sphere>();

            partObj.transform.SetParent(bones[i], false);
            partObj.transform.localPosition = Vector3.zero;

            spheresByColour[i].Init((SphereColour)i, Rand.Range(0, 2) == 1, bones, model.transform);
        }

    }

    public class PermData
    {
        public int src, dst;
        public Vector3 fPos;
    }

    public void DoPermutations(string permData)
    {
        var perms = AnimAdapter.DestringifyPermEvent(permData);

        Sphere[] newByPosition = new Sphere[6];
        Array.Copy(spheresByPosition, newByPosition, 6);

        //Debug.Log("Destringification successful.  Perm " + perm);

        foreach (PermData perm in perms)
        {
            spheresByPosition[perm.src].BeginTransition((BodyPart)perm.dst, perm.fPos);
            
            newByPosition[perm.dst] = spheresByPosition[perm.src];
        }

        spheresByPosition = newByPosition;
    }


    public void PopulateBoneArray()
    {
        if (bones == null || bones.Length != 6 || bones[0] == null)
        {
            bones = new Transform[6];
            foreach (var childObj in transform.GetComponentsInChildren<Transform>())
            {
                if (Enum.TryParse<BodyPart>(childObj.name, out var p))
                {
                    int i = (int)p;
                    bones[i] = childObj;
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        PopulateBoneArray();

        foreach(Transform bone in bones)
        {
            Gizmos.DrawWireSphere(bone.position, 0.2f);
        }
    }
}
