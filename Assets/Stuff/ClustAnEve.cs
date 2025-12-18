using UnityEngine;

/// <summary>
/// Container for the Cluster’s animation event handlers.
/// </summary>
public class ClustAnEve : MonoBehaviour
{
    Cluster parent;
    public void SetParent(Cluster parent)
    {
        this.parent = parent;
    }
    public void PermEvent(string s)
    {
        //Debug.Log("Sorry, my bad. " + s);
        //Debug.Log("Hand R position: " + GameObject.Find("Hand R").transform.position);
        //Debug.Log("Event called");
        //Debug.Log(s);

        parent.DoPermutations(s);
    }

    //public void TestEventHandler(float f, int i, string s)
    //{
    //    Debug.Log("Float " + f + ", int " + i + ", string " + s);
    //}
}
