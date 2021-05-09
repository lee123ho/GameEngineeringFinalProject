using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IKFootSolver : MonoBehaviour
{
    private Bone _bone;
    [SerializeField] private LayerMask _layer;
    [SerializeField] private float stepDistance;
    [SerializeField] private float stepHeight;
    [SerializeField] private float speed;
    [SerializeField] private IKFootSolver otherFoot;
    [SerializeField] private Vector3 footOffset;

    private Vector3 currentPosition;
    private Vector3 newPosition, oldPosition;

    private float footSpacing;
    private float lerpTime;
    private float flightTime;

    private void Awake()
    {
        _bone = GetComponentInParent<Bone>();
        footSpacing = transform.localPosition.x;
        currentPosition = newPosition = oldPosition = transform.position + footOffset;
        lerpTime = flightTime = 1f;
    }

    void Update()
    {
        transform.position = currentPosition;

        Ray ray = new Ray(_bone.RootBone.transform.position + (-_bone.RootBone.transform.forward * footSpacing), Vector3.down);
        if(Physics.Raycast(ray, out RaycastHit info, 5, _layer))
        {
            if(Vector3.Distance(newPosition, info.point) > stepDistance / 2 && !otherFoot.isMoving() && lerpTime >= flightTime)
            {
                lerpTime = 0;
                newPosition = info.point * stepDistance / 2 + footOffset;
            }
        }

        if(lerpTime < flightTime)
        {
            Vector3 footPosition = Vector3.Lerp(oldPosition, newPosition, Time.deltaTime * flightTime);
            footPosition.y += Mathf.Sin(lerpTime * Mathf.PI) * stepHeight;

            currentPosition = footPosition;
            lerpTime += Time.deltaTime * speed;
        }
        else
        {
            currentPosition = oldPosition = newPosition;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(newPosition, 0.2f);
    }

    public bool isMoving()
    {
        return lerpTime < flightTime;
    }
}
