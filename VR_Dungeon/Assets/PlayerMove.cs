using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class PlayerMove : MonoBehaviour
{
    public Transform _cam;
    private NavMeshAgent _agent;
    private CharacterController _controller;
    public float _speed;
    // Start is called before the first frame update
    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        //_controller = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
        var camVector = (transform.position - _cam.position).normalized;
        var direction = (camVector * Input.GetAxis("Vertical") + Input.GetAxis("Horizontal") * _cam.right) * (_speed * Time.deltaTime);
        var faceDirection = (camVector * Input.GetAxis("Vertical2") + Input.GetAxis("Horizontal2") * _cam.right);
        direction.y = 0f;
        _agent.Move(direction);
        //_controller.Move(direction * Time.deltaTime);

        if (direction.magnitude >= Mathf.Epsilon)
            transform.rotation = Quaternion.LookRotation(direction.normalized);
    }
}