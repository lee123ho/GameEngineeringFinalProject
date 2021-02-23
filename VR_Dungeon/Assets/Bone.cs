using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Axis
{
    X,
    Y,
    Z
}

public class Bone : MonoBehaviour
{
    [Header("Bone Object")]
    public GameObject MainBone;
    [SerializeField] private Axis BoneForwardDir;
    [Header("Bone Chains")]
    public GameObject RootBone;
    public GameObject TipBone;
    [SerializeField] private Axis ForwardDir;
    [SerializeField] private Axis UpDir;

    [SerializeField] private List<Limbs> Limbs = new List<Limbs>(); 
    [SerializeField] private Constraints Constraints;

    private MotionAnalysis _motionAnalysis;

    private int _limbsCount;
    public int LimbsCount => _limbsCount;

    private void Awake()
    {
        Constraints.ParentBone = RootBone;
        Constraints.ChildBone = TipBone;
        Constraints.BoneChain.Add(Constraints.ChildBone);
        Constraints.setBoneChain(Constraints.ChildBone);

        _limbsCount = Limbs.Count;

        _motionAnalysis = GetComponent<MotionAnalysis>();
        _motionAnalysis._toe = Limbs[0].TipBone.transform.Find("toe");
        _motionAnalysis._heel = Limbs[0].TipBone.transform.Find("heel");
        _motionAnalysis.enabled = true;
    }
}
