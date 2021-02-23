using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawGraph : MonoBehaviour
{
    public Transform toe;
    public Transform heel;
    public GameObject toeLinePrefab;
    public GameObject HeelLinePrefab;

    MotionAnalysis _motionAnalysis;
    LineRenderer _toeLineRenderer;
    LineRenderer _heelLineRenderer;

    List<Vector3> toePos = new List<Vector3>();
    List<Vector3> heelPos = new List<Vector3>();

    float sampleCount = 30;

    float offSet = 0.01f;

    float toeTemp;
    float heelTemp;
    float normalizeTimeCount = 0f;

    private void Start()
    {
        _motionAnalysis = GetComponent<MotionAnalysis>();
        GameObject toeLine = Instantiate(toeLinePrefab);
        _toeLineRenderer = toeLine.GetComponent<LineRenderer>();
        GameObject heelLine = Instantiate(HeelLinePrefab);
        _heelLineRenderer = heelLine.GetComponent<LineRenderer>();
        toeTemp = heelTemp = normalizeTimeCount;
    }

    private void Update()
    {
        if((normalizeTimeCount / sampleCount) <= _motionAnalysis.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime)
        {
            normalizeTimeCount++;
        }
        DrawLine(_toeLineRenderer, toe, toePos, ref toeTemp);
        DrawLine(_heelLineRenderer, heel, heelPos, ref heelTemp);
    }

    private void DrawLine(LineRenderer _lineRenderer, Transform transform, List<Vector3> pos, ref float temp)
    {
        if (_lineRenderer.positionCount == 90 && temp != normalizeTimeCount)
        {
            _lineRenderer.positionCount--;
            for (int i = 0; i < _lineRenderer.positionCount - 1; i++)
            {
                _lineRenderer.SetPosition(i, pos[i] = pos[i + 1] + new Vector3(offSet * 8, 0f));
                pos[_lineRenderer.positionCount - 1] = pos[pos.Count - 1];

                normalizeTimeCount = 0;
                temp = normalizeTimeCount;
            }
        }

        if (_lineRenderer.positionCount < 90 && temp != normalizeTimeCount)
        {
            Vector3 position = new Vector3(5f, transform.position.y * 2);
            pos.Add(position);
            if (_lineRenderer.positionCount > 1)
            {
                for (int i = 0; i < _lineRenderer.positionCount - 1; i++)
                {
                    _lineRenderer.SetPosition(i, pos[i] += new Vector3(offSet * 8, 0f));
                } 
            }
            _lineRenderer.positionCount++;
            temp = normalizeTimeCount;
        }
    }
}
