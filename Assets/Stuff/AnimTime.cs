using System;
using UnityEngine;
using Rand = UnityEngine.Random;

/// <summary>
/// Attach to any object with an AnimatorController that needs to follow
/// TimeKeeper for its animation speed.
/// </summary>
[RequireComponent(typeof(Animator))]
public class AnimTime : MonoBehaviour
{
    Animator animer;

    private void Awake()
    {
        animer = GetComponent<Animator>();
    }

    private void Update()
    {
        if (animer != null)
        {
            animer.speed = TimeKeeper.GetTimeRate();
        }
    }
}
