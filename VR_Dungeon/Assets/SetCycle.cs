using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetCycle : MonoBehaviour
{
    [SerializeField] private GameObject _toe;

    List<Vector3> toePosList = new List<Vector3>();

    float frameCount;
    float updateCount;

    Animator _animator;
    Animation _animation;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _animation = GetComponent<Animation>();
    }

    private void Update()
    {
        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        _animator.Play(stateInfo.shortNameHash, 0, 0.25f);
        _animator.Update(0f);

        //if (!toePosList.Contains(_toe.transform.position))
        //{
        //    toePosList.Add(_toe.transform.position);
        //    frameCount++;
        //}
        //else if (toePosList.Contains(_toe.transform.position))
        //{
        //    Destroy(_animator);
        //    this.enabled = false;
        //}
    }
}
