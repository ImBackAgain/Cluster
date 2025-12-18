using UnityEngine;

public class TimeKeeper : MonoBehaviour
{
    [Range(0, 2)]
    [SerializeField]
    float globalMult;
    
    static TimeKeeper instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static float GetDeltaTime()
    {
        return Time.deltaTime * instance.globalMult;
    }

    public static float GetTimeRate()
    {
        return instance.globalMult;
    }
}
