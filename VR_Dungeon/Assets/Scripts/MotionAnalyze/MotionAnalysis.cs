using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum footIndex
{
    toe,
    heel
}

public struct LimbSampleData
{
    public Vector3 middleVector;
    public Vector3 middleVector2;
    public Vector3 toePos2;
    public Matrix4x4 toeMatrix;
    public Vector3 toePos;
    public Vector3 heelPos2;
    public Matrix4x4 heelMatrix;
    public Vector3 heelPos;
    public Vector3 footBase;
    public Vector3 footBaseNormalized;
    public float balance;
}

public struct LimbInfo
{
    public Vector3 displacement;
    public Vector3 strideDirection;
    public Vector3 toeToHeelVector;
    public Vector3 stancePosition;
    public float strideLength;
    public float stanceTime;
    public float footOffTime;
    public float footStrikeTime;
    public int stanceFrameTime;
    public LimbSampleData[] limbSampleDatas;

    //public LimbInfo(Vector3 dis, Vector3 strideDir, float strideLen)
    //{
    //    displacement = dis;
    //    strideDirection = strideDir;
    //    strideLength = strideLen;
    //}
}

[RequireComponent(typeof(Bone))]
public class MotionAnalysis : MonoBehaviour
{
    Bone _bone;

    [HideInInspector] public LimbInfo[] limbInfos;

    Vector3 pointA;
    Vector3 pointB;
    Vector3 pointC;
    Vector3 footDirection;

    float highestMagnitude;

    int limbCount;
    float normalizeTimeCount = 0;
    int stanceFrameTime = 0;
    int sampleCount = 30;

    int footOffFrameTime;
    int footStrikeFrameTime;

    float footOffTime;
    float footStrikeTime;
    float flightTime;

    Vector3 displacement;

    float strideLength;
    Vector3 strideDirection;

    float cycleDistance = 0f;
    Vector3 cycleDirection = Vector3.zero;
    float cycleSpeed;
    float cycleDuration;

    public int StanceFrameTime => stanceFrameTime;
    public float FootoffTime => footOffTime;
    public float FootstrikeTime => footStrikeTime;
    public float NormalizeTimeCount => normalizeTimeCount;
    public float Speed => cycleSpeed;
    public float CycleDuration => cycleDuration;

    public void Init()
    {
        pointA = pointB = pointC = footDirection = displacement = strideDirection = cycleDirection = Vector3.zero;
        highestMagnitude = normalizeTimeCount = footOffTime = footStrikeTime = flightTime = strideLength = cycleDistance = cycleSpeed = cycleDuration = 0f;
        limbCount = stanceFrameTime = footOffFrameTime = footStrikeFrameTime = 0;
        sampleCount = 30;
    }

    public void Analyze(GameObject gameObject, AnimationClip animation, bool UsualPos)
    {
        Init();
        Debug.Log(animation.name + "모션 분석 시작");
        
        _bone = GetComponent<Bone>();
        limbCount = _bone.Limbs.Count;
        // 다리 사이클과 샘플링 정보 초기화
        limbInfos = new LimbInfo[limbCount];
        for (int leg = 0; leg < limbCount; leg++)
        {
            limbInfos[leg] = new LimbInfo();
            limbInfos[leg].limbSampleDatas = new LimbSampleData[sampleCount + 1];
            for (int i = 0; i < sampleCount + 1; i++)
            {
                limbInfos[leg].limbSampleDatas[i] = new LimbSampleData();
            }
        }

        for (int leg = 0; leg < limbCount; leg++)
        {
            // 다리 애니메이션 샘플 데이터
            Transform toeTransform = _bone.Limbs[leg].TipBone;
            Transform heelTransform = _bone.Limbs[leg].AnkleBone;
            //Debug.Log(toeTransform.name);

            float toeMax = -0x7FFFFFFF; float heelMax = -0x7FFFFFFF; float highestHeight = 0f;

            for (int i = 0; i < sampleCount + 1; i++)
            {
                animation.SampleAnimation(gameObject, i * 1f / sampleCount * animation.length);

                // 애니메이션의 한 샘플 마다 상대적인 위치값 계산
                limbInfos[leg].limbSampleDatas[i].toePos = SamplePosDataCalculate(toeTransform, heelTransform, gameObject.transform, leg).Item1;
                limbInfos[leg].limbSampleDatas[i].heelPos = SamplePosDataCalculate(toeTransform, heelTransform, gameObject.transform, leg).Item2;
                limbInfos[leg].limbSampleDatas[i].middleVector = (limbInfos[leg].limbSampleDatas[i].toePos + limbInfos[leg].limbSampleDatas[i].heelPos) / 2;
                limbInfos[leg].limbSampleDatas[i].balance = GetBalance(limbInfos[leg].limbSampleDatas[i], leg);

                limbInfos[leg].limbSampleDatas[i].toeMatrix = Util.RelativeMatrix(toeTransform, gameObject.transform);
                limbInfos[leg].limbSampleDatas[i].heelMatrix = Util.RelativeMatrix(heelTransform, gameObject.transform);
                limbInfos[leg].limbSampleDatas[i].toePos2 = limbInfos[leg].limbSampleDatas[i].toeMatrix.MultiplyPoint(_bone.Limbs[leg].ankleToToeVector2);
                limbInfos[leg].limbSampleDatas[i].heelPos2 = limbInfos[leg].limbSampleDatas[i].heelMatrix.MultiplyPoint(_bone.Limbs[leg].ankleToHeelVector2);
                limbInfos[leg].limbSampleDatas[i].middleVector2 = (limbInfos[leg].limbSampleDatas[i].toePos2 + limbInfos[leg].limbSampleDatas[i].heelPos2) / 2;

                toeMax = (toeMax < limbInfos[leg].limbSampleDatas[i].toePos.y ? limbInfos[leg].limbSampleDatas[i].toePos.y : toeMax);
                heelMax = (heelMax < limbInfos[leg].limbSampleDatas[i].heelPos.y ? limbInfos[leg].limbSampleDatas[i].heelPos.y : heelMax);
            }
            highestHeight = Mathf.Max(toeMax, heelMax);

            // 샘플링 된 데이터를 통해 지면에 축을 생성
            highestMagnitude = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                for (int j = 0; j < sampleCount; j++)
                {
                    if (highestMagnitude < (limbInfos[leg].limbSampleDatas[i].middleVector - limbInfos[leg].limbSampleDatas[j].middleVector).magnitude)
                    {
                        highestMagnitude = (limbInfos[leg].limbSampleDatas[i].middleVector - limbInfos[leg].limbSampleDatas[j].middleVector).magnitude;
                        pointA = new Vector3(limbInfos[leg].limbSampleDatas[i].middleVector.x, 0f, limbInfos[leg].limbSampleDatas[i].middleVector.z);
                        pointB = new Vector3(limbInfos[leg].limbSampleDatas[j].middleVector.x, 0f, limbInfos[leg].limbSampleDatas[j].middleVector.z);
                    }
                }
            }

            pointC = (pointA + pointB) / 2;
            //Debug.Log(string.Format("{0:F6}", pointA));
            //Debug.Log(string.Format("{0:F6}", pointB));
            //Debug.Log(string.Format("{0:F6}", pointC));

            if (!UsualPos)
            {
                float lowestCost = 0x7FFFFFFF;
                for (int i = 0; i < sampleCount + 1; i++)
                {
                    if (lowestCost > GetLowestCostSampling(limbInfos[leg].limbSampleDatas[i], highestHeight))
                    {
                        lowestCost = GetLowestCostSampling(limbInfos[leg].limbSampleDatas[i], highestHeight);
                        limbInfos[leg].stanceFrameTime = i;
                    }
                } 
            }
            else
            {
                cycleDirection = Vector3.forward;
                limbInfos[leg].stanceFrameTime = 0;
            }
            //Debug.Log(string.Format("{0:F6}", limbInfos[leg].stanceFrameTime));

            // 스탠스 타임 적용
            limbInfos[leg].stanceTime = limbInfos[leg].stanceFrameTime * 1f / sampleCount;
            //Debug.Log(limbInfos[leg].stanceTime);

            // 스탠스 타임 때 샘플의 발바닥 벡터
            animation.SampleAnimation(gameObject, limbInfos[leg].stanceTime * animation.length);
            //Debug.Log(string.Format("{0:F6}", animation.length));

            limbInfos[leg].limbSampleDatas[limbInfos[leg].stanceFrameTime].toePos = SamplePosDataCalculate(toeTransform, heelTransform, gameObject.transform, leg).Item1;
            limbInfos[leg].limbSampleDatas[limbInfos[leg].stanceFrameTime].heelPos = SamplePosDataCalculate(toeTransform, heelTransform, gameObject.transform, leg).Item2;

            limbInfos[leg].toeToHeelVector = limbInfos[leg].limbSampleDatas[limbInfos[leg].stanceFrameTime].toePos - limbInfos[leg].limbSampleDatas[limbInfos[leg].stanceFrameTime].heelPos;
            limbInfos[leg].toeToHeelVector = limbInfos[leg].limbSampleDatas[limbInfos[leg].stanceFrameTime].toeMatrix.MultiplyPoint(_bone.Limbs[leg].ankleToToeVector2)
                - limbInfos[leg].limbSampleDatas[limbInfos[leg].stanceFrameTime].heelMatrix.MultiplyPoint(_bone.Limbs[leg].ankleToHeelVector2);

            limbInfos[leg].toeToHeelVector = Vector3.ProjectOnPlane(limbInfos[leg].toeToHeelVector, Vector3.up);
            limbInfos[leg].toeToHeelVector = limbInfos[leg].toeToHeelVector.normalized * _bone.Limbs[leg].footLength;
            footDirection = limbInfos[leg].toeToHeelVector;
            //Debug.Log(string.Format("{0:F6}", footDirection));

            // 비행 경로 계산
            for (int i = 0; i < sampleCount + 1; i++)
            {
                limbInfos[leg].limbSampleDatas[i].footBase = GetFootbase(limbInfos[leg].limbSampleDatas[i], leg);
                //Debug.Log(string.Format("{0:F6}", limbInfos[leg].limbSampleDatas[i].footBase));
            }

            limbInfos[leg].stancePosition = limbInfos[leg].limbSampleDatas[limbInfos[leg].stanceFrameTime].footBase;
            limbInfos[leg].stancePosition = Vector3.ProjectOnPlane(limbInfos[leg].stancePosition, Vector3.up);

            if (!UsualPos)
            {
                // Foot Off Time 과 Foot Strike Time을 계산하여 각 다리의 보폭과 방향을 계산한다
                footOffFrameTime = GetLegKeyTime(limbInfos[leg].limbSampleDatas, limbInfos[leg].stanceFrameTime, highestHeight).Item1;
                footStrikeFrameTime = GetLegKeyTime(limbInfos[leg].limbSampleDatas, limbInfos[leg].stanceFrameTime, highestHeight).Item2;

                limbInfos[leg].footOffTime = footOffFrameTime * 1f / sampleCount;
                limbInfos[leg].footStrikeTime = footStrikeFrameTime * 1f / sampleCount;
                //Debug.Log(string.Format("{0:F6}", footOffFrameTime));
                //Debug.Log(string.Format("{0:F6}", footStrikeFrameTime)); 
            }
            else
            {
                cycleDirection = Vector3.zero;
                cycleDistance = 0;
            }

            displacement = limbInfos[leg].limbSampleDatas[(footOffFrameTime + limbInfos[leg].stanceFrameTime) % sampleCount].footBase - limbInfos[leg].limbSampleDatas[(footStrikeFrameTime + limbInfos[leg].stanceFrameTime) % sampleCount].footBase;
            displacement.y = 0f;
            //Debug.Log(string.Format("{0:F6}", footOffFrameTime));
            //Debug.Log(string.Format("{0:F6}", footStrikeFrameTime));
            strideLength = displacement.magnitude / (limbInfos[leg].footOffTime - limbInfos[leg].footStrikeTime + 1);
            strideDirection = -displacement.normalized;
            cycleDistance += strideLength;
            cycleDirection += strideDirection;
            Debug.Log($"{(_bone.Limbs[leg].Left == true ? "왼쪽" : "오른쪽")} 다리의 보폭은 {strideLength}, 방향은 {strideDirection}입니다.");
        }

        // 다리 주기 별로 계산 된 값들을 합쳐 하나의 다리 애니메이션 사이클의 방향, 보폭, 속도를 구한다
        cycleDistance /= limbInfos.Length;
        cycleDirection /= limbInfos.Length;

        cycleDuration = animation.length;
        cycleSpeed = cycleDistance / cycleDuration;
        Debug.Log($"다리의 보폭은 {cycleDistance}, 방향은 {cycleDirection}입니다. 걸음 속도는 {cycleSpeed}입니다.");

        for (int leg = 0; leg < _bone.Limbs.Count; leg++)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                int j = Util.Mod(i + limbInfos[leg].stanceFrameTime, sampleCount);
                float time = i * 1f / sampleCount;
                limbInfos[leg].limbSampleDatas[j].footBaseNormalized = limbInfos[leg].limbSampleDatas[j].footBase;

                // Calculate normalized foot flight path
                // based on the cycle distance of the whole motion
                // (the calculated average cycle distance)
                int getTime = Util.Mod((int)((limbInfos[leg].footOffTime * 0 + limbInfos[leg].stanceTime) * sampleCount + 0.5), sampleCount);
                Vector3 reference = (
                    -cycleDistance * cycleDirection * (time - limbInfos[leg].footOffTime * 0)
                    // FIXME: Is same as stance position:
                    + limbInfos[leg].limbSampleDatas[getTime].footBase
                );

                limbInfos[leg].limbSampleDatas[j].footBaseNormalized = (limbInfos[leg].limbSampleDatas[j].footBaseNormalized - reference);
                if (cycleDirection != Vector3.zero)
                {
                    limbInfos[leg].limbSampleDatas[j].footBaseNormalized = Quaternion.Inverse(
                        Quaternion.LookRotation(cycleDirection)
                    ) * limbInfos[leg].limbSampleDatas[j].footBaseNormalized;
                }

                limbInfos[leg].limbSampleDatas[j].footBaseNormalized.z /= cycleDistance;

                limbInfos[leg].limbSampleDatas[j].footBaseNormalized.y = limbInfos[leg].limbSampleDatas[j].footBase.y;
            }
        }

        //// Calculate normalized foot flight path
        //for (int leg = 0; leg < _bone.Limbs.Count; leg++)
        //{
        //    if (motionType == MotionType.WalkCycle)
        //    {
        //        for (int j = 0; j < sampleCount; j++)
        //        {
        //            int i = Util.Mod(j + limbInfos[leg].stanceIndex, samples);
        //            LimbSampleData s = limbInfos[leg].limbSampleDatas[i];
        //            float time = GetTimeFromIndex(j);
        //            s.footBaseNormalized = s.footBase;

        //            if (fixFootSkating)
        //            {
        //                // Calculate normalized foot flight path
        //                // based on the calculated cycle distance of each individual foot
        //                Vector3 reference = (
        //                    -limbInfos[leg].cycleDistance * limbInfos[leg].cycleDirection * (time - limbInfos[leg].liftoffTime)
        //                    + limbInfos[leg].limbSampleDatas[
        //                        GetIndexFromTime(limbInfos[leg].liftoffTime + limbInfos[leg].stanceTime)
        //                    ].footBase
        //                );

        //                s.footBaseNormalized = (s.footBaseNormalized - reference);
        //                if (limbInfos[leg].cycleDirection != Vector3.zero)
        //                {
        //                    s.footBaseNormalized = Quaternion.Inverse(
        //                        Quaternion.LookRotation(limbInfos[leg].cycleDirection)
        //                    ) * s.footBaseNormalized;
        //                }

        //                s.footBaseNormalized.z /= limbInfos[leg].cycleDistance;
        //                if (time <= limbInfos[leg].liftoffTime) { s.footBaseNormalized.z = 0; }
        //                if (time >= limbInfos[leg].strikeTime) { s.footBaseNormalized.z = 1; }

        //                s.footBaseNormalized.y = s.footBase.y - legC.groundPlaneHeight;
        //            }
        //            else
        //            {
        //                // Calculate normalized foot flight path
        //                // based on the cycle distance of the whole motion
        //                // (the calculated average cycle distance)
        //                Vector3 reference = (
        //                    -m_cycleDistance * m_cycleDirection * (time - limbInfos[leg].liftoffTime * 0)
        //                    // FIXME: Is same as stance position:
        //                    + limbInfos[leg].samples[
        //                        GetIndexFromTime(limbInfos[leg].liftoffTime * 0 + limbInfos[leg].stanceTime)
        //                    ].footBase
        //                );

        //                s.footBaseNormalized = (s.footBaseNormalized - reference);
        //                if (limbInfos[leg].cycleDirection != Vector3.zero)
        //                {
        //                    s.footBaseNormalized = Quaternion.Inverse(
        //                        Quaternion.LookRotation(m_cycleDirection)
        //                    ) * s.footBaseNormalized;
        //                }

        //                s.footBaseNormalized.z /= m_cycleDistance;

        //                s.footBaseNormalized.y = s.footBase.y - legC.groundPlaneHeight;
        //            }
        //        }
        //        //limbInfos[leg].samples[limbInfos[leg].stanceIndex].footBaseNormalized.z = 0;
        //        limbInfos[leg].samples[samples] = limbInfos[leg].samples[0];
        //    }
        //    else
        //    {
        //        for (int j = 0; j < samples; j++)
        //        {
        //            int i = Util.Mod(j + limbInfos[leg].stanceIndex, samples);
        //            LimbSampleData s = limbInfos[leg].samples[i];
        //            s.footBaseNormalized = s.footBase - limbInfos[leg].stancePosition;
        //        }
        //    }
        //}
    }

    //private void Update()
    //{
    //    if (normalizeTimeCount == sampleCount)
    //    {
    //        initLimInfoList.Add(SamplingAndAnalysis(notePosInfoList));
    //        Debug.Log($"{footOffFrameTime}, {footStrikeFrameTime}, {stanceTime}");
    //        var newList = CalOtherLeg(notePosInfoList, 15, new Vector3(-0.4f, 0f, 0f));
    //        initLimInfoList.Add(SamplingAndAnalysis(newList));

    //        float tempLength = 0f;
    //        Vector3 tempDirection = new Vector3();
    //        foreach (var limbInfo in initLimInfoList)
    //        {
    //            tempLength += limbInfo.strideLength;
    //            tempDirection += limbInfo.strideDirection;
    //        }
    //        cycleDistance = tempLength / initLimInfoList.Count;
    //        cycleDirection = tempDirection / initLimInfoList.Count;
    //        cycleSpped = cycleDistance / cycleDuration;

    //        normalizeTimeCount++;
    //    }
    //    else if ((normalizeTimeCount / sampleCount) <= _animator.GetCurrentAnimatorStateInfo(0).normalizedTime && _animator.GetCurrentAnimatorStateInfo(0).normalizedTime <= 1f)
    //    {
    //        notePosInfoList.Add(new NotePosInfo(middlePoint.transform.position, _toe.position, _heel.position));

    //        if (Math.Max(_toe.position.y, _heel.position.y) > highestValue)
    //            highestValue = Math.Max(_toe.position.y, _heel.position.y);

    //        normalizeTimeCount++;
    //    }

    //    if (_animator.GetCurrentAnimatorStateInfo(0).normalizedTime <= 1f)
    //    {
    //        if (notePosInfoList.Count >= 16 && notePosInfoList.Count <= 26)
    //            flightTime += Time.deltaTime;

    //        cycleDuration += Time.deltaTime; 
    //    }

    //    middlePoint.transform.position = (_toe.position + _heel.position) / 2;
    //}

    //private LimbInfo SamplingAndAnalysis(List<NotePosInfo> notePosInfos)
    //{
    //    for (int i = 0; i < sampleCount; i++)
    //    {
    //        for (int j = 0; j < sampleCount; j++)
    //        {
    //            if (highestMagnitude < (notePosInfos[i].middleVector - notePosInfos[j].middleVector).magnitude)
    //            {
    //                highestMagnitude = (notePosInfos[i].middleVector - notePosInfos[j].middleVector).magnitude;
    //                pointA = new Vector3(notePosInfos[i].middleVector.x, 0f, notePosInfos[i].middleVector.z);
    //                pointB = new Vector3(notePosInfos[j].middleVector.x, 0f, notePosInfos[j].middleVector.z);
    //            }
    //        }
    //    }

    //    pointC = (pointA + pointB) / 2;

    //    for (int i = 0; i < sampleCount; i++)
    //    {
    //        if (lowestCost > GetLowestCostSampling(notePosInfos[i]))
    //        {
    //            lowestCost = GetLowestCostSampling(notePosInfos[i]);
    //            stanceTime = i;
    //        }
    //    }

    //    var tempToe = notePosInfos[stanceTime].toePos;
    //    var tempHeel = notePosInfos[stanceTime].heelPos;

    //    footLength = (tempHeel - tempToe).magnitude;
    //    footDirection = (tempHeel - tempToe).normalized * footLength;

    //    footOffFrameTime = GetLegKeyTime(notePosInfos).Item1;
    //    footStrikeFrameTime = GetLegKeyTime(notePosInfos).Item2;

    //    footOffTime = ReCalculateKeyTime(footOffFrameTime, stanceTime);
    //    footStrikeTime = ReCalculateKeyTime(footStrikeFrameTime, stanceTime);

    //    displacement = Getfootbase(notePosInfos[footOffFrameTime]) - Getfootbase(notePosInfos[footStrikeFrameTime]);
    //    strideLength = Math.Abs(displacement.magnitude / (footOffTime - footStrikeTime));
    //    strideDirection = -displacement.normalized;

    //    return new LimbInfo(displacement, strideDirection, strideLength);
    //}

    public Tuple<Vector3, Vector3> SamplePosDataCalculate(Transform toeTr, Transform heelTr, Transform OriginTr, int leg)
    {
        Vector3 toePos = _bone.RelativePosTr(toeTr, OriginTr);
        Vector3 heelPos = _bone.RelativePosTr(heelTr, OriginTr);
        toePos.y -= _bone.Limbs[leg].firstY.x;
        heelPos.y -= _bone.Limbs[leg].firstY.y;

        Vector3 footMiddle = (toePos + heelPos) / 2;
        Vector3 footVector = (toePos - heelPos).normalized;

        Vector3 toeVector = footMiddle + (_bone.Limbs[leg].footLength / 2 + _bone.Limbs[leg].footOffset.y) * footVector;
        Vector3 heelVector = footMiddle + (-_bone.Limbs[leg].footLength / 2 + _bone.Limbs[leg].footOffset.y) * footVector;

        return new Tuple<Vector3, Vector3>(toeVector, heelVector);
    }

    private float GetLowestCostSampling(LimbSampleData sampleData, float highestValue)
    {
        var highestValueFps = Math.Max(sampleData.toePos.y, sampleData.heelPos.y);
        var normalizeHeightValue = highestValueFps / highestValue;

        var result = Math.Abs(Vector3.ProjectOnPlane((sampleData.middleVector - pointC), Vector3.up).magnitude) / highestMagnitude;

        var cost = normalizeHeightValue + result;

        return cost;
    }

    private float GetBalance(LimbSampleData posInfo, int leg)
    {
        var balance = Mathf.Atan((posInfo.heelPos.y - posInfo.toePos.y) / _bone.Limbs[leg].footLength * 20f) / (float)Math.PI + 0.5f;

        return balance;
    }

    private Vector3 GetFootbase(LimbSampleData posInfo, int leg)
    {
        var footbase = posInfo.heelPos * (1 - posInfo.balance) + (posInfo.toePos - footDirection) * posInfo.balance;

        return footbase;
    }

    private Tuple<int, int> GetLegKeyTime(LimbSampleData[] posInfos, int standIndex, float range)
    {
        float lowest = range * 0.1f;
        List<int> toeTimes = new List<int>();
        List<int> heelTimes = new List<int>();
        int time = 0;

        for (int i = standIndex; i < sampleCount + standIndex; i++)
        {
            i = (i >= sampleCount ? i - sampleCount : i);

            if (lowest < posInfos[i].middleVector.y) break;
            if (lowest > posInfos[i].toePos.y)
                toeTimes.Add(time);
            if (lowest > posInfos[i].heelPos.y)
                heelTimes.Add(time);
            ++time;
        }

        var offTime = Math.Max(toeTimes.Max(), heelTimes.Max());
        toeTimes.Clear(); heelTimes.Clear(); time = 0;

        for (int i = standIndex; i > -sampleCount; i--)
        {
            i = (i >= 0 ? i : sampleCount + i);

            if (lowest < posInfos[i].middleVector.y) break;

            if (lowest > posInfos[i].toePos.y)
                toeTimes.Add(time);
            if (lowest > posInfos[i].heelPos.y)
                heelTimes.Add(time);
            --time;
        }

        var strikeTime = (Math.Min(toeTimes.Min(), heelTimes.Min()) >= 0 ? Math.Min(toeTimes.Min(), heelTimes.Min()) : Math.Min(toeTimes.Min(), heelTimes.Min()) + sampleCount);

        var result = new Tuple<int, int>(offTime, strikeTime);

        return result;
    }

    //private List<LimbSampleData> CalOtherLeg(List<LimbSampleData> posInfos, int frameOffset, Vector3 positionOffset)
    //{
    //    var newPosInfos = new List<LimbSampleData>();

    //    for(int i = frameOffset; i < posInfos.Count; ++i)
    //    {
    //        var toePos = posInfos[i].toePos + positionOffset;
    //        var heelPos = posInfos[i].heelPos + positionOffset;
    //        var newMiddleVector = (toePos + heelPos) / 2;
    //        newPosInfos.Add(new LimbSampleData(newMiddleVector, toePos, heelPos));
    //    }

    //    for(int i = 0; i < frameOffset; ++i)
    //    {
    //        var toePos = posInfos[i].toePos + positionOffset;
    //        var heelPos = posInfos[i].heelPos + positionOffset;
    //        var newMiddleVector = (toePos + heelPos) / 2;
    //        newPosInfos.Add(new LimbSampleData(newMiddleVector, toePos, heelPos));
    //    }

    //    return newPosInfos;
    //}

    private float ReCalculateKeyTime(int frameTime, int stanceTime)
    {
        float result = 0f;
        if (frameTime - stanceTime < 0)
            result = (float)(sampleCount + (frameTime - stanceTime));
        else if (frameTime - stanceTime > 0)
            result = frameTime - stanceTime;
        
        return result / 30f ;
    }

    public Vector3 GetFlightFootPosition(int leg, float flightTime, int phase, int type)
    {
        if (type == 0)
        {
            //Debug.Log("ground");
            if (phase == 0) return Vector3.zero;
            if (phase == 1) return (-Mathf.Cos(flightTime * Mathf.PI) / 2 + 0.5f) * Vector3.forward;
            if (phase == 2) return Vector3.forward; 
        }

        //Debug.Log(string.Format(leg + " {0:F6}", limbInfos[leg].footStrikeTime));

        float cycleTime = 0;
        if (phase == 0) cycleTime = Mathf.Lerp(0, limbInfos[leg].footOffTime, flightTime);
        else if (phase == 1) cycleTime = Mathf.Lerp(limbInfos[leg].footOffTime, limbInfos[leg].footStrikeTime, flightTime);
        else cycleTime = Mathf.Lerp(limbInfos[leg].footStrikeTime, 1, flightTime);
        //return GetVector3AtTime(leg,cycleTime,FootPositionNormalized);
        //flightTime = Mathf.Clamp01(flightTime);
        int index = (int)(cycleTime * sampleCount);
        float weight = cycleTime * sampleCount - index;
        if (index >= sampleCount - 1)
        {
            index = sampleCount - 1;
            weight = 0;
        }
        //index = (index + limbInfos[leg].stanceFrameTime) % sampleCount >= 0 ? (index + limbInfos[leg].stanceFrameTime) % sampleCount : (index + limbInfos[leg].stanceFrameTime) % sampleCount + sampleCount;
        index = Util.Mod(index + limbInfos[leg].stanceFrameTime, sampleCount);
        //Debug.Log(string.Format(leg + " {0:F6}", limbInfos[leg].limbSampleDatas[index].footBaseNormalized));
        return (
            limbInfos[leg].limbSampleDatas[index].footBaseNormalized * (1 - weight)
            + limbInfos[leg].limbSampleDatas[(index + 1) % sampleCount >= 0 ? (index + 1) % sampleCount : (index + 1) % sampleCount + 1].footBaseNormalized * (weight)
        );
    }

    public static Vector3 GetHeelOffset(
        Transform ankleT, Vector3 ankleHeelVector,
        Transform toeT, Vector3 toeToetipVector,
        Vector3 stanceFootVector,
        Quaternion footBaseRotation
    )
    {
        // Given the ankle and toe transforms,
        // the heel and toetip positions are calculated.
        Vector3 heel = ankleT.localToWorldMatrix.MultiplyPoint(ankleHeelVector);
        Vector3 toetip = toeT.localToWorldMatrix.MultiplyPoint(toeToetipVector);

        // From this the balance is calculated,
        // relative to the current orientation of the foot base.
        float balance = GetFootBalance(
            (Quaternion.Inverse(footBaseRotation) * heel).y,
            (Quaternion.Inverse(footBaseRotation) * toetip).y,
            stanceFootVector.magnitude
        );

        // From the balance, the heel offset can be calculated.
        Vector3 heelOffset = balance * ((footBaseRotation * stanceFootVector) + (heel - toetip));

        return heelOffset;
    }

    public static float GetFootBalance(float heelElevation, float toeElevation, float footLength)
    {
        // For any moment in time we want to know if the heel or toe is closer to the ground.
        // Rather than a binary value, we need a smooth curve with 0 = heel is closer and 1 = toe is closer.
        // We use the inverse tangens for this as it maps arbritarily large positive or negative values into a -1 to 1 range.
        return Mathf.Atan((
            // Difference in height between heel and toe.
            heelElevation - toeElevation
        ) / footLength * 20) / Mathf.PI + 0.5f;
        // The 20 multiplier is found by trial and error. A rapid but still slightly smooth change of weight is wanted.
    }

    public static Vector3 GetAnklePosition(
        Transform ankleT, Vector3 ankleHeelVector,
        Transform toeT, Vector3 toeToetipVector,
        Vector3 stanceFootVector,
        Vector3 footBasePosition, Quaternion footBaseRotation
    )
    {
        // Get the heel offset
        Vector3 heelOffset = GetHeelOffset(
            ankleT, ankleHeelVector, toeT, toeToetipVector,
            stanceFootVector, footBaseRotation
        );

        // Then calculate the ankle position.
        Vector3 anklePosition = (
            footBasePosition
            + heelOffset
            + ankleT.localToWorldMatrix.MultiplyVector(ankleHeelVector * -1)
        );

        return anklePosition;
    }
}   