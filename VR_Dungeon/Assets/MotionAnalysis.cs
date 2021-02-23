using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct NotePosInfo
{
    public Vector3 middleVector;
    public Vector3 toePos;
    public Vector3 heelPos;

    public NotePosInfo(Vector3 Vector, Vector3 toe, Vector3 heel)
    {
        middleVector = Vector;
        toePos = toe;
        heelPos = heel;
    }
}

public struct LimbInfoList
{
    public Vector3 displacement;
    public Vector3 strideDirection;
    public float strideLength;

    public LimbInfoList(Vector3 dis, Vector3 strideDir, float strideLen)
    {
        displacement = dis;
        strideDirection = strideDir;
        strideLength = strideLen;
    }
}

public class MotionAnalysis : MonoBehaviour
{
    [ReadOnly] public Transform _toe;
    [ReadOnly] public Transform _heel;

    GameObject middlePoint;

    Animator _animator;

    List<NotePosInfo> notePosInfoList = new List<NotePosInfo>();
    List<LimbInfoList> limInfoList = new List<LimbInfoList>();

    Vector3 pointA;
    Vector3 pointB;
    Vector3 pointC;
    Vector3 footDirection;

    float highestValue;
    float highestMagnitude;
    float lowestCost = 0x7FFFFFFF;
    float footLength;

    float normalizeTimeCount = 0;
    int stanceTime = 0;
    float sampleCount = 30;

    int footOffFrameTime;
    int footStrikeFrameTime;

    float footOffTime;
    float footStrikeTime;

    Vector3 displacement;
    float strideLength;
    Vector3 strideDirection;

    float cycleDistance;
    Vector3 cycleDirection;
    float cycleSpped;
    float cycleDuration;

    public Animator Animator => _animator;
    public int StanceTime => stanceTime;
    public float NormalizeTimeCount => normalizeTimeCount;
    public float Speed => cycleSpped;
    public List<LimbInfoList> LimbInfoList => limInfoList;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        middlePoint = new GameObject();
        middlePoint.AddComponent<TrailRenderer>();
        middlePoint.GetComponent<TrailRenderer>().startWidth = 0.01f;
        middlePoint.GetComponent<TrailRenderer>().endWidth = 0.01f;
        middlePoint.GetComponent<TrailRenderer>().time = 1f;
        middlePoint.transform.position = (_toe.position + _heel.position) / 2;
    }

    private void Update()
    {
        if (normalizeTimeCount == sampleCount)
        {
            limInfoList.Add(SamplingAndAnalysis(notePosInfoList));
            Debug.Log($"{footOffFrameTime}, {footStrikeFrameTime}");
            var newList = CalOtherLeg(notePosInfoList, 15, new Vector3(-0.4f, 0f, 0f));
            limInfoList.Add(SamplingAndAnalysis(newList));

            float tempLength = 0f;
            Vector3 tempDirection = new Vector3();
            foreach (var limbInfo in limInfoList)
            {
                tempLength += limbInfo.strideLength;
                tempDirection += limbInfo.strideDirection;
            }
            cycleDistance = tempLength / limInfoList.Count;
            cycleDirection = tempDirection / limInfoList.Count;
            cycleSpped = cycleDistance / cycleDuration;

            normalizeTimeCount++;
        }
        else if ((normalizeTimeCount / sampleCount) <= _animator.GetCurrentAnimatorStateInfo(0).normalizedTime && _animator.GetCurrentAnimatorStateInfo(0).normalizedTime <= 1f)
        {
            notePosInfoList.Add(new NotePosInfo(middlePoint.transform.position, _toe.position, _heel.position));

            if (Math.Max(_toe.position.y, _heel.position.y) > highestValue)
                highestValue = Math.Max(_toe.position.y, _heel.position.y);

            normalizeTimeCount++;
            cycleDuration += Time.deltaTime;
        }

        middlePoint.transform.position = (_toe.position + _heel.position) / 2;
    }

    private LimbInfoList SamplingAndAnalysis(List<NotePosInfo> notePosInfos)
    {
        for (int i = 0; i < sampleCount; i++)
        {
            for (int j = 0; j < sampleCount; j++)
            {
                if (highestMagnitude < (notePosInfos[i].middleVector - notePosInfos[j].middleVector).magnitude)
                {
                    highestMagnitude = (notePosInfos[i].middleVector - notePosInfos[j].middleVector).magnitude;
                    pointA = new Vector3(notePosInfos[i].middleVector.x, 0f, notePosInfos[i].middleVector.z);
                    pointB = new Vector3(notePosInfos[j].middleVector.x, 0f, notePosInfos[j].middleVector.z);
                }
            }
        }

        pointC = (pointA + pointB) / 2;

        for (int i = 0; i < sampleCount; i++)
        {
            if (lowestCost > GetLowestCostSampling(notePosInfos[i]))
            {
                lowestCost = GetLowestCostSampling(notePosInfos[i]);
                stanceTime = i;
            }
        }

        var tempToe = notePosInfos[stanceTime].toePos;
        var tempHeel = notePosInfos[stanceTime].heelPos;

        footLength = (tempHeel - tempToe).magnitude;
        footDirection = (tempHeel - tempToe).normalized * footLength;

        footOffFrameTime = GetLegKeyTime(notePosInfos).Item1;
        footStrikeFrameTime = GetLegKeyTime(notePosInfos).Item2;

        footOffTime = ReCalculateKeyTime(footOffFrameTime, stanceTime);
        footStrikeTime = ReCalculateKeyTime(footStrikeFrameTime, stanceTime);

        displacement = Getfootbase(notePosInfos[footOffFrameTime]) - Getfootbase(notePosInfos[footStrikeFrameTime]);
        strideLength = Math.Abs(displacement.magnitude / (footOffTime - footStrikeTime));
        strideDirection = -displacement.normalized;

        return new LimbInfoList(displacement, strideDirection, strideLength);
    }
    
    private float GetLowestCostSampling(NotePosInfo posInfo)
    {
        var highestValueFps = Math.Max(posInfo.toePos.y, posInfo.heelPos.y);
        var normalizeHeightValue = highestValueFps / highestValue;

        var result = Math.Abs(Vector3.Dot((posInfo.middleVector - pointC), (pointB - pointA).normalized)) / highestMagnitude;

        var cost = normalizeHeightValue + result;

        return cost;
    }

    private Vector3 Getfootbase(NotePosInfo posInfo)
    {
        var balance = Mathf.Atan((posInfo.heelPos.y - posInfo.toePos.y) / footLength * 20f) / (float)Math.PI + 0.5f;

        var footbase = posInfo.heelPos * balance + (posInfo.toePos - footDirection) * (1 - balance);

        return footbase;
    }

    private Tuple<int, int> GetLegKeyTime(List<NotePosInfo> posInfos)
    {
        float lowest = 0x7FFFFFFF;
        List<int> toeTimes = new List<int>();
        List<int> heelTimes = new List<int>();
        int time = 0;

        foreach (var position in posInfos)
        {
            if (lowest > position.middleVector.y)
                lowest = position.middleVector.y;
        }

        lowest += 0.1f;

        foreach (var position in posInfos)
        {
            if (lowest > position.toePos.y)
                toeTimes.Add(time);
            if (lowest > position.heelPos.y)
                heelTimes.Add(time);
            ++time;
        }

        var offTime = Math.Max(toeTimes.Max(), heelTimes.Max());
        toeTimes.Clear(); heelTimes.Clear(); time = 0;

        foreach (var position in posInfos)
        {
            if (lowest > position.toePos.y)
                toeTimes.Add(time);
            if (lowest > position.heelPos.y)
                heelTimes.Add(time);
            ++time;
        }

        var strikeTime = Math.Min(toeTimes.Min(), heelTimes.Min());

        var result = new Tuple<int, int>(offTime, strikeTime);

        return result;
    }

    private List<NotePosInfo> CalOtherLeg(List<NotePosInfo> posInfos, int frameOffset, Vector3 positionOffset)
    {
        var newPosInfos = new List<NotePosInfo>();

        for(int i = frameOffset; i < posInfos.Count; ++i)
        {
            var toePos = posInfos[i].toePos + positionOffset;
            var heelPos = posInfos[i].heelPos + positionOffset;
            var newMiddleVector = (toePos + heelPos) / 2;
            newPosInfos.Add(new NotePosInfo(newMiddleVector, toePos, heelPos));
        }

        for(int i = 0; i < frameOffset; ++i)
        {
            var toePos = posInfos[i].toePos + positionOffset;
            var heelPos = posInfos[i].heelPos + positionOffset;
            var newMiddleVector = (toePos + heelPos) / 2;
            newPosInfos.Add(new NotePosInfo(newMiddleVector, toePos, heelPos));
        }

        return newPosInfos;
    }

    private float ReCalculateKeyTime(int frameTime, int stanceTime)
    {
        float result = 0f;
        if (frameTime - stanceTime < 0)
            result = (float)(sampleCount + (frameTime - stanceTime));
        else if (frameTime - stanceTime > 0)
            result = frameTime - stanceTime;
        
        return result / 30f ;
    }
}   