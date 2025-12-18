using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Utils;

public class Sphere : MonoBehaviour
{
    #region Static
    public static Material[] mats;

    static void GetMatsIfNeeded()
    {
        //Debug.Log("Hello");
        if (mats == null || mats.Length != 7 || mats[0] == null)
        {
            //Debug.Log("Hello yourself");
            mats = new Material[7];
            for (int i = 0; i < mats.Length; i++)
            {
                string matName = ((SphereColour)i).ToString();

                mats[i] = AssetDatabase.LoadMainAssetAtPath("Assets/Mats/Mat " + matName + ".mat") as Material;

                //Debug.Log(matName + " found to be " +  mats[i]);
            }
        }
    }
    #endregion


    //When a transition is ordered, how long should the sphere remain in ‘homing’ mode?
    const float HOMING_TIME = Constants.ALMOST_TIME;

    //If the target transform gets this close to the target position, snap to it and exit the transition state.
    const float HOMING_QUIT_RANGE = 0.25f;

    //In transform-homing mode, how much should the distance to the target be cut every frame?
    const float HOMING_RATE = 0.001f;
    

    /// <summary>
    /// This colour identifies the sphere.  There should always be six spheres
    /// corresponding to the six non-default colours, and this is the identifier.
    /// </summary>
    SphereColour intrinsicColour;

    /// <summary>
    /// The intrinsic colour can be overridden.  Use cases include the player
    /// having neither permanent colours nor a colour charge, and when a Squirgle
    /// ability briefly changes all spheres’ colours to the ability’s.
    /// </summary>
    SphereColour currentColour;

    [SerializeField] Renderer rend;
    Transform[] bones;
    Transform tempParent;


    [SerializeField]
    bool inTransit = false;
    float transitionTimer = 0;
    Transform targetTransform;
    Vector3 targetPosition;

    private void Update()
    {
        if (inTransit) Transit();
    }

    private void Transit()
    {
        if (transitionTimer <= 0 || (targetPosition - targetTransform.localPosition).sqrMagnitude < HOMING_QUIT_RANGE)
        {
            transform.SetParent(targetTransform);
            transform.localPosition = Vector3.zero;
            inTransit = false;
        }
        else
        {
            //HOMING_RATE works per frame if going at 60 frames per ‘second’.
            //Else (i.e. seconds are longer because of time slow), exponentiate.
            float scalar = Mathf.Pow(HOMING_RATE, TimeKeeper.GetTimeRate());

            transform.localPosition = Vector3.Lerp(targetPosition, transform.localPosition, scalar);
        }
        transitionTimer -= TimeKeeper.GetDeltaTime();
    }
    

    public void Init(SphereColour col, bool colress, Transform[] bones, Transform tempParent)
    {
        intrinsicColour = col;
        currentColour = colress ? SphereColour.Default : col;
        this.bones = bones;
        this.tempParent = tempParent;

        gameObject.name = col + " sphere";  

        GetMatsIfNeeded();

        UpdateMat();
    }
    
    public void SetOverrideColour(SphereColour col)
    {
        currentColour = col;
    }

    public void ClearOverrideColour(bool colress)
    {
        currentColour = colress ? SphereColour.Default : intrinsicColour;
    }

    private void UpdateMat()
    {
        rend.SetMaterials(new List<Material>() { mats[(int)currentColour] });
    }


    
    public void BeginTransition(BodyPart newParent, Vector3 targetPosition)
    {
        targetTransform = bones[(int)newParent];
        Debug.Log(name + " ordered to chase " + targetTransform);

        if (TimeKeeper.GetDeltaTime() >= 2 * HOMING_TIME)
        {
            transform.SetParent(targetTransform);
            transform.localPosition = Vector3.zero;

            Debug.Log(name + " skipping transition phase");
        }
        else
        {
            transitionTimer = HOMING_TIME;
            inTransit = true;

            //Vector3 p = transform.position;
            transform.SetParent(tempParent, true);
            //transform.position = p;

            //if ((targetPosition - transform.localPosition).sqrMagnitude < HOME_POS_RANGE_SQ)
            {
                //position-homing mode
                this.targetPosition = targetPosition;
                Debug.Log(name + " beginning position chase to " + targetPosition);
            }
            //else
            //{
            //    //transform-homing mode
            //    this.targetPosition = null;
            //    Debug.Log(name + " beginning transform chase of " + targetTransform + " due to sqdist " + 
            //        (targetPosition - transform.localPosition).sqrMagnitude);
            //}
        }
    }
    
}
