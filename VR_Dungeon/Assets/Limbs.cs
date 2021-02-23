using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Limbs
{
    [Header("Limb Type")]
    public bool Left;
    public bool Right;
    public bool Leg;
    public bool Arm;

    [Header("Limb Bone")]
    public GameObject RootBone;
    public GameObject TipBone;
    public Axis ApproximatedAxis;
    [Range(0f, 1f)]
    public float ConstraintStiffness = 1f;

    [HideInInspector] public List<LimbInfoList> _notePosInfoList = new List<LimbInfoList>();

    private float nextFootPrintTime;
    private float cycleTime;
}
