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

public class MotionInfo
{
    public Vector3[] toeToHeelVector = new Vector3[2];
    public Vector3[] standPosition = new Vector3[2];
    public float cycleDuration;
    public float speed;
}

public class Bone : MonoBehaviour
{
    [Header("Animation")]
    public AnimationClip UsualPos;
    public AnimationClip[] motions;
    [Header("Bone Object")]
    public GameObject MainBone;
    [SerializeField] private Axis BoneForwardDir;
    [Header("Bone Chains")]
    public GameObject RootBone;
    public GameObject TipBone;
    [SerializeField] private Axis ForwardDir;
    [SerializeField] private Axis UpDir;

    [Space]

    [SerializeField] private List<Limbs> limbs = new List<Limbs>(); 
    [SerializeField] private Constraints Constraints;

    public MotionAnalysis _motionAnalysis;

    private int _limbsCount;
    private int _motionCount;
    private bool analyzed;

    private MotionInfo[] _motionInfo;

    public int LimbsCount => _limbsCount;
    public int MotionCount => _motionCount;
    public List<Limbs> Limbs => limbs;
    public MotionInfo[] MotionInfo => _motionInfo;

    public void Init()
    {
        analyzed = false;
        Debug.Log($"{name}�� ��� �м��� �����մϴ�");

        for (int leg = 0; leg < limbs.Count; leg++)
        {
            limbs[leg].legChain = GetTransformChain(limbs[leg].RootBone, limbs[leg].AnkleBone);
        }

        InitFoot();

        _motionInfo = new MotionInfo[2];
        _motionInfo[0] = new MotionInfo();
        _motionInfo[1] = new MotionInfo();

        // ��� �м� �ǽ�
        for (int i = 0; i < motions.Length; i++)
        {
            _motionAnalysis.Analyze(gameObject, motions[i], i == 0);
            for (int leg = 0; leg < limbs.Count; leg++)
            {
                _motionInfo[i].toeToHeelVector[leg] = _motionAnalysis.limbInfos[leg].toeToHeelVector; 
                _motionInfo[i].standPosition[leg] = _motionAnalysis.limbInfos[leg].stancePosition; 
                _motionInfo[i].cycleDuration = _motionAnalysis.CycleDuration; 
                _motionInfo[i].speed = _motionAnalysis.Speed; 
            }
        }
        UsualPos.SampleAnimation(gameObject, 0);

        analyzed = true;

        Debug.Log("��� �м��� �Ϸ� �Ͽ����ϴ�.");
    }

    public void InitFoot()
    {
        // �� ���� �ʱ�ȭ �۾�
        for (int leg = 0; leg < limbs.Count; leg++)
        {
            // �ִϸ��̼� �ʱ�ȭ - ���� ��ġ ����
            UsualPos.SampleAnimation(gameObject, 0);

            // ���� ������Ʈ���� ����� ��ġ ���
            Vector3 toePos = RelativePosTr(limbs[leg].TipBone, gameObject.transform);
            Vector3 heelPos = RelativePosTr(limbs[leg].AnkleBone, gameObject.transform);
            limbs[leg].firstY.x = toePos.y;
            limbs[leg].firstY.y = heelPos.y;
            toePos.y = heelPos.y = 0f;

            // �߰��� ���� ����
            Vector3 footMiddle = (toePos + heelPos) / 2;
            Vector3 footVector = (toePos - heelPos).normalized;

            // Toe�� Heel�� ��� (Heel�� ankle�� �� ���̰� ��� ankle�� �صξ����� offset �߰��Ͽ� ���� ���� �ؾ���)
            limbs[leg].ankleToToeVector = footMiddle + (limbs[leg].footLength / 2 + limbs[leg].footOffset.y) * footVector + limbs[leg].footOffset.x * Vector3.Cross(Vector3.up, footVector);
            limbs[leg].ankleToHeelVector = footMiddle + (-limbs[leg].footLength / 2 + limbs[leg].footOffset.y) * footVector + limbs[leg].footOffset.x * Vector3.Cross(Vector3.up, footVector);
            //Debug.Log(string.Format("{0:F6}", limbs[leg].ankleToHeelVector));

            Matrix4x4 ankleMatrix = Util.RelativeMatrix(limbs[leg].AnkleBone, gameObject.transform);
            Vector3 anklePosition = ankleMatrix.MultiplyPoint(Vector3.zero);
            Vector3 heelPosition = anklePosition;
            heelPosition.y = 0f;

            Matrix4x4 toeMatrix = Util.RelativeMatrix(limbs[leg].TipBone, gameObject.transform);
            Vector3 toePosition = toeMatrix.MultiplyPoint(Vector3.zero);
            Vector3 toetipPosition = toePosition;
            toetipPosition.y = 0f;

            footMiddle = (heelPosition + toetipPosition) / 2;
            footVector = (toetipPosition - heelPosition).normalized;

            limbs[leg].ankleToHeelVector2 = (
            footMiddle
            + (-limbs[leg].footLength / 2 + limbs[leg].footOffset.y) * footVector
            + limbs[leg].footOffset.x * Vector3.Cross(footVector, Vector3.up)
        );
            limbs[leg].ankleToHeelVector2 = ankleMatrix.inverse.MultiplyVector(limbs[leg].ankleToHeelVector2 - anklePosition);

            limbs[leg].ankleToToeVector2 = (
            footMiddle
            + (limbs[leg].footLength / 2 + limbs[leg].footOffset.y) * footVector
            + limbs[leg].footOffset.x * Vector3.Cross(footVector, Vector3.up)
        );
            limbs[leg].ankleToToeVector2 = toeMatrix.inverse.MultiplyVector(limbs[leg].ankleToToeVector2 - toePosition);
        }
    }

    private void Awake()
    {
        //if (!analyzed) Debug.LogError("��� �м��� �ȵǾ����ϴ�. ��� �м��� �Ͻʼ�.");

        Constraints.ParentBone = RootBone;
        Constraints.ChildBone = TipBone;
        Constraints.BoneChain.Add(Constraints.ChildBone);
        Constraints.setBoneChain(Constraints.ChildBone);

        _limbsCount = limbs.Count;
        //_motionAnalysis.enabled = true;
    }

    public Vector3 RelativePosTr(Transform target, Transform Origin)
    {
        Vector3 result = target.position - Origin.localPosition;

        return result;
    }

    public Transform[] GetTransformChain(Transform upper, Transform lower)
    {
        Transform t = lower;
        int chainLength = 1;
        while (t != upper)
        {
            t = t.parent;
            chainLength++;
        }
        Transform[] chain = new Transform[chainLength];
        t = lower;
        for (int j = 0; j < chainLength; j++)
        {
            chain[chainLength - 1 - j] = t;
            t = t.parent;
        }
        return chain;
    }
}
