using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Constraints
{
    [Header("Angle Constraint")]
    // must in parent
    [ReadOnly] public GameObject ParentBone;
    // must in bottom child
    [ReadOnly] public GameObject ChildBone;
    [Range(0f, 90f)]
    public float SwingPitchLimitAngle;
    [Range(0f, 90f)]
    public float SwingYawLimitAngle;

    [ReadOnly] public List<GameObject> BoneChain = new List<GameObject>();

    public void setBoneChain(GameObject obj)
    {
        var parentObject = obj.transform.parent;

        BoneChain.Add(parentObject.gameObject);

        if (parentObject.gameObject != ParentBone)
            setBoneChain(parentObject.gameObject);
        else return;

    }
}
