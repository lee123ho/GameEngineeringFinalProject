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
    public Transform RootBone;
    public Transform AnkleBone;
    public Transform TipBone;
    public float footWidth;
    public float footLength;
    public Vector2 footOffset;
    [HideInInspector] public Transform[] legChain;
    [HideInInspector] public Vector3 ankleToHeelVector;
    [HideInInspector] public Vector3 ankleToHeelVector2;
    [HideInInspector] public Vector3 ankleToToeVector;
    [HideInInspector] public Vector3 ankleToToeVector2;
    [HideInInspector] public Vector2 firstY;

    [Space]

    public Axis ApproximatedAxis;
    [Range(0f, 1f)]
    public float ConstraintStiffness = 1f;
}
