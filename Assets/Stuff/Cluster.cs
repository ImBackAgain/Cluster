using System;
using System.Collections.Generic;
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
    Sphere[] spheresByColour;

    [SerializeField]
    Sphere[] spheresByPosition;
    
    Transform[] bones;
    public static readonly string[] boneNames = 
        new string[] { "Foot L", "Foot R", "Hand L", "Hand R", "Torso", "Head" };


    void Start()
    {
        spheresByColour = new Sphere[boneNames.Length];
        spheresByPosition = new Sphere[boneNames.Length];

        ClustAnEve model = GetComponentInChildren<ClustAnEve>();
        model.SetParent(this);

        PopulateBoneArray();

        for (int i = 0; i < spheresByColour.Length; i++)
        {
            GameObject partPref = Resources.Load<GameObject>("Part");

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
            
            newByPosition[perm.src] = spheresByPosition[perm.dst];
        }

        spheresByPosition = newByPosition;
    }


    public void PopulateBoneArray()
    {
        if (bones == null || bones.Length != 6 || bones[0] == null)
        {
            bones = new Transform[6];
            foreach (var bone in transform.GetComponentsInChildren<Transform>())
            {
                int i = Array.IndexOf(boneNames, bone.name);
                if (i != -1)
                {
                    bones[i] = bone;
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
            Gizmos.DrawWireSphere(bone.position, 0.1f);
        }
    }
}
