using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LegState
{
    public Vector3 nextStepPosition;
    public Vector3 prevNextStepPosition;
    public Vector3 nextStepPositionGoal;
    public float nextFootPrintTime;
    public float prevNextFootPrintTime;
    public Matrix4x4 nextFootRotateMatrix;
    public Matrix4x4 prevNextFootRotateMatrix;

    public Vector3 hipReference;
    public Vector3 ankleReference;
    public Vector3 footBase;
    public Quaternion footBaseRotation;
    public Vector3 ankle;

    public float stanceTime = 0;
    public float footOffTime = 0.2f;
    public float footStrikeTime = 0.8f;
    public float liftTime = 0.1f;
    public float landTime = 0.9f;
    public float postliftTime = 0.3f;
    public float prelandTime = 0.7f;

    public float cycleTime = 1;
    public float designatedCycleTimePrev = 0.9f;

    public Vector3 standPosition;
    public Vector3 toeToHeelVector;

    public bool parked;

    public float GetFootGrounding(float time)
    {
        if ((time <= liftTime) || (time >= landTime)) return 0;
        if ((time >= postliftTime) && (time <= prelandTime)) return 1;
        if (time < postliftTime)
        {
            return (time - liftTime) / (postliftTime - liftTime);
        }
        else
        {
            return 1 - (time - prelandTime) / (landTime - prelandTime);
        }
    }

}

public class MotionAnimator : MonoBehaviour
{
    public float maxFootRotationAngle = 45f;
    public float maxIKAdjustmentDistance = 0.5f;

    public float minStepDistance = 0.2f;
    public float maxStepDuration = 1.5f;
    public float maxStepRotation = 160f;
    public float maxStepAcceleration = 5.0f;
    public float maxStepHeight = 1.0f;
    public float maxSlopeAngle = 60f;

    private Animator _animator;
    private AnimatorClipInfo[] _animatorClips;
    private AnimationClip[] _animationClips;

    private Bone _bone;
    private LegState[] _legState;
    private AlignmentTracker tr;
    private PlayerMove _playerMove;
    private Rigidbody _rigidbody;
    private Transform _transform;

    private AnimatorStateInfo[] _cycleMotionStates;

    private Dictionary<string, float> _motionNameAndValue = new Dictionary<string, float>();

    private Vector3 _position;
    private Vector3 _prePosition;
    private Quaternion _rotation;
    private Quaternion _preRotation;

    private Vector3 _up;
    private Vector3 _forward;
    private Vector3 _velocity;
    private Vector3 _baseUpGround;
        
    private Vector3 _objectVelocity;
    private Vector3 _usedObjectVelocity;
    private Vector3 _preVelocity = Vector3.zero;
    private Vector3 _velocitySmooth = Vector3.zero;
    private Vector3 _angularVelocitySmooth = Vector3.zero;
    private Vector3 _acceleSmooth = Vector3.zero;
    private Vector3 _angularVelocity;
    private Vector3 _accleration;

    public LayerMask groundLayers = 1;

    private float speed;
    private float _hSpeedSmooth;
    private float normalizeTime;
    private float[] _motionWeights;
    private float[] _cycleMotionWeights;
    private float locomotionWeight;

    private float _cycleDuration;
    private float _cycleDistance;
    private float _currentTime;

    private bool enableParking = true;
    private bool firstFrame = true;

    public float groundHugX = 0; // Sensible for humanoids
    public float groundHugZ = 0; // Sensible for humanoids
    public float climbTiltAmount = 0.5f; // Sensible default value
    public float climbTiltSensitivity = 0.0f; // None as default
    public float accelerateTiltAmount = 0.02f; // Sensible default value
    public float accelerateTiltSensitivity = 0.0f; // None as default;

    private Vector3 bodyUp;
    private Vector3 legsUp;
    private float accelerationTiltX;
    private float accelerationTiltZ;

    public bool footMarker = false;
    Vector3 a, b = Vector3.zero;
    void Start()
    {
        _bone = GetComponent<Bone>();
        _bone.Init();
        tr = GetComponent<AlignmentTracker>();
        _rigidbody = GetComponent<Rigidbody>();
        _transform = GetComponent<Transform>();
        _animator = GetComponent<Animator>();
        _playerMove = GetComponent<PlayerMove>();
        _animationClips = _animator.runtimeAnimatorController.animationClips;
        //_position = _prePosition = transform.position;

        for (int i = 0; i < _animationClips.Length; i++)
        {
            _motionNameAndValue.Add(_animationClips[i].name, 0f);
        }

        _legState = new LegState[_bone.LimbsCount];
        firstFrame = true;
        ResetWeight();
        ResetValue();
    }

    private void ResetWeight()
    {
        _motionWeights = new float[_animationClips.Length];
        _cycleMotionWeights = new float[_animationClips.Length];

        for (int leg = 0; leg < _bone.LimbsCount; leg++)
        {
            _legState[leg] = new LegState();
        }
    }

    private void ResetValue()
    {
        //_transform = gameObject.transform;
        _up = transform.up;
        _forward = transform.forward;
        _baseUpGround = _up;
        legsUp = _up;
        accelerationTiltX = 0;
        accelerationTiltZ = 0;
        bodyUp = _up;

        tr.Reset();

        //_position = _prePosition = transform.position;
        //tr.rotation = _preRotation = transform.rotation;
        //tr.velocity = Vector3.zero;
        //_preVelocity = Vector3.zero;
        //tr.velocitySmoothed = Vector3.zero;
        //tr.angularVelocitySmoothed = Vector3.zero;
        //tr.accelerationSmoothed = Vector3.zero;
        //_angularVelocity = Vector3.zero;
        //_accleration = Vector3.zero;

        for (int leg = 0; leg < _bone.Limbs.Count; leg++)
        {
            _legState[leg].prevNextFootPrintTime = Time.time - 0.01f;
            _legState[leg].nextFootPrintTime = Time.time;
            //Debug.Log(string.Format("{0:F6}", _legState[leg].nextFootPrintTime));

            _legState[leg].prevNextFootRotateMatrix = FindGroundedBase(
                transform.TransformPoint(_legState[leg].standPosition),
                transform.rotation,
                _legState[leg].toeToHeelVector,
                false
            );
            _legState[leg].prevNextStepPosition = _legState[leg].prevNextFootRotateMatrix.GetColumn(3);
            //Debug.Log(string.Format("{0:F6}", _legState[leg].prevNextStepPosition));

            _legState[leg].nextStepPosition = _legState[leg].prevNextStepPosition;
            _legState[leg].nextFootRotateMatrix = _legState[leg].prevNextFootRotateMatrix;
        }
        normalizeTime = 0;

        _cycleDuration = maxStepDuration;
        _cycleDistance = 0;

        //Debug.Log(_transform.position);
    }

    // Update is called once per frame
    void Update()
    {
        if (Time.time == 0 || Time.timeScale == 0) return;

        Vector3 velocityClamp = Vector3.ProjectOnPlane(tr.velocity, _up);
        speed = velocityClamp.magnitude;
        velocityClamp = velocityClamp + _up * Mathf.Clamp(Vector3.Dot(tr.velocity, _up), -speed, speed);
        speed = velocityClamp.magnitude;

        _hSpeedSmooth = Vector3.ProjectOnPlane(tr.velocitySmoothed, _up).magnitude;

        _objectVelocity = transform.InverseTransformPoint(tr.velocitySmoothed) - transform.InverseTransformPoint(Vector3.zero);

        bool newVelocity = false;
        if ((_objectVelocity - _usedObjectVelocity).magnitude > 0.002f * Mathf.Min(_objectVelocity.magnitude, _usedObjectVelocity.magnitude) || firstFrame)
        {
            newVelocity = true;
            _usedObjectVelocity = _objectVelocity;
        }

        //float speedRate = (speed / _playerMove._speed > 1 ? 1 : speed / _playerMove._speed);
        float speedRate = Mathf.Clamp01(speed / _playerMove._speed);
        _animator.SetFloat("moveSpeed", speedRate);

        // 메카님을 통한 애니메이션 가중치 출력
        _animatorClips = _animator.GetCurrentAnimatorClipInfo(0);

        for (int i = 0; i < _animationClips.Length; i++)
            _motionNameAndValue[_animator.runtimeAnimatorController.animationClips[i].name] = 0f;

        for (int a = 0; a < _animatorClips.Length; a++)
        {
            _motionNameAndValue[_animatorClips[a].clip.name] = _animatorClips[a].weight;
        }
        float sumCycleWeight = 0f;
        for (int i = 0; i < _motionNameAndValue.Count; i++)
        {
            _motionWeights[i] = _motionNameAndValue[_animationClips[i].name];
            //if (i == 0)
            //    _motionWeights[i] = Mathf.Clamp(_motionNameAndValue[_animationClips[i].name], 0.010813f, 1f);
            //else if (i == 1)
            //    _motionWeights[i] = Mathf.Clamp(_motionNameAndValue[_animationClips[i].name], 0f, 0.987530f);
            if (i > 0)
            {
                _cycleMotionWeights[i] = _motionWeights[i];
                sumCycleWeight =+ _cycleMotionWeights[i];
            }
            //Debug.Log(string.Format(i + " {0:F6}", _motionWeights[i]));
        }
        //Debug.Log(string.Format("{0:F6}", _cycleMotionWeights[0]));
        for (int leg = 0; leg < _bone.LimbsCount; leg++)
        {
            _legState[leg].standPosition = Vector3.zero;
            _legState[leg].toeToHeelVector = Vector3.zero;
        }
        for (int i = 0; i < _motionWeights.Length; i++)
        {
            if(_motionWeights[i] > 0)
            {
                for (int leg = 0; leg < _bone.LimbsCount; leg++)
                {
                    //if (leg == 0)
                        _legState[leg].standPosition += _bone.MotionInfo[i].standPosition[leg] * _motionWeights[i];
                    //else
                    //    _legState[leg].standPosition += new Vector3(-0.097001f, 0.000000f, -0.202225f) * _motionWeights[i];
                    _legState[leg].toeToHeelVector += _bone.MotionInfo[i].toeToHeelVector[leg] * _motionWeights[i];
                }
            }
        }
        //Debug.Log(string.Format("{0:F6}", _legState[1].standPosition));
        //Debug.Log(string.Format("{0:F6}", _bone.MotionInfo[1].standPosition[0]));
        //Debug.Log(string.Format("{0:F6}", _bone.MotionInfo[1].standPosition[1]));

        if (sumCycleWeight > 0)
        {
            for (int leg = 0; leg < _bone.LimbsCount; leg++)
            {
                _legState[leg].footOffTime = 0f;
                _legState[leg].footStrikeTime = 0f;
            }
            for (int i = 0; i < _cycleMotionWeights.Length; i++)
            {
                if (_cycleMotionWeights[i] > 0)
                {
                    for (int leg = 0; leg < _bone.LimbsCount; leg++)
                    {
                        //_legState[leg].footOffTime += _bone._motionAnalysis.limbInfos[leg].footOffTime * _cycleMotionWeights[i];
                        _legState[leg].footOffTime += 0.2f;
                        //_legState[leg].footStrikeTime += _bone._motionAnalysis.limbInfos[leg].footStrikeTime * _cycleMotionWeights[i];
                        _legState[leg].footStrikeTime += 0.8f;
                        //Debug.Log(string.Format("{0:F6}", _cycleMotionWeights[1]));
                    }
                }
            } 
        }

        if (sumCycleWeight > 0)
        {
            for (int leg = 0; leg < _bone.LimbsCount; leg++)
            {
                Vector2 standTimeVector = Vector2.zero;
                for (int i = 0; i < _cycleMotionWeights.Length; i++)
                {
                    if (_cycleMotionWeights[i] > 0)
                    {
                        standTimeVector += new Vector2(Mathf.Cos(_bone._motionAnalysis.limbInfos[leg].stanceTime * 2 * Mathf.PI),
                            Mathf.Sin(_bone._motionAnalysis.limbInfos[leg].stanceTime * 2 * Mathf.PI)) * _cycleMotionWeights[i];
                    }
                }
                _legState[leg].stanceTime = Util.Mod(
                        Mathf.Atan2(standTimeVector.y, standTimeVector.x) / 2 / Mathf.PI);
                //Debug.Log(string.Format(leg + "{0:F6}", _legState[leg].stanceTime));
            } 
        }

        locomotionWeight = Mathf.Clamp01(_motionWeights[0] + _motionWeights[1]);
        if (firstFrame)
        {
            ResetValue();
        }

        float cycleFrequency = 0f;
        float animatedCycleSpeed = 0f;
        for (int i = 0; i < _motionWeights.Length; i++)
        {
            if(_motionWeights[i] > 0)
            {
                cycleFrequency += (1 / _bone.MotionInfo[i].cycleDuration) * _cycleMotionWeights[i];
                //UnityEngine.Debug.Log(i + " : " + _bone._motionAnalysis.CycleDuration);
            }
            animatedCycleSpeed += _bone.MotionInfo[i].speed * _motionWeights[i];
            //UnityEngine.Debug.Log(i + " : " + animatedCycleSpeed);
        }

        float desiredCycleDuration = maxStepDuration;
        if (cycleFrequency > 0) desiredCycleDuration = 1 / cycleFrequency;

        float speedMultiplier = 1;
        if (speed != 0) speedMultiplier = animatedCycleSpeed / speed;
        if (speedMultiplier > 0) desiredCycleDuration *= speedMultiplier;

        float verticalAngularVelocity = Vector3.Project(tr.rotation * tr.angularVelocitySmoothed, _up).magnitude;
        if (verticalAngularVelocity > 0)
        {
            desiredCycleDuration = Mathf.Min(maxStepRotation / verticalAngularVelocity, desiredCycleDuration);
        }

        float groundAccelerationMagnitude = Vector3.ProjectOnPlane(tr.accelerationSmoothed, _up).magnitude;
        if (groundAccelerationMagnitude > 0)
        {
            desiredCycleDuration = Mathf.Clamp(maxStepAcceleration / groundAccelerationMagnitude, desiredCycleDuration / 2, desiredCycleDuration);
        }

        desiredCycleDuration = Mathf.Min(desiredCycleDuration, maxStepDuration);
        _cycleDuration = desiredCycleDuration;
        //Debug.Log(string.Format("First  {0:F6}", _cycleDuration));
        _cycleDistance = _cycleDuration * speed;

        //Debug.Log(string.Format("First  {0:F6}", _cycleDuration));
        bool allParked = false;
        if (enableParking)
        {
            allParked = true;
            for (int leg = 0; leg < _bone.Limbs.Count; leg++)
            {
                if (!_legState[leg].parked)
                    allParked = false;
            }
        }
        //Debug.Log(string.Format("Update  {0:F6}", _cycleDuration));

        if (!allParked)
        {
            //Debug.Log(string.Format("{0:F6}", normalizeTime));
            //normalizeTime = ((normalizeTime + (1 / _cycleDuration) * Time.deltaTime) % 1 >= 0
            //    ? (normalizeTime + (1 / _cycleDuration) * Time.deltaTime) % 1
            //    : ((normalizeTime + (1 / _cycleDuration) * Time.deltaTime) % 1) + 1);
            //normalizeTime = Util.Mod((normalizeTime + (1 / _cycleDuration) * Time.deltaTime));
            normalizeTime = Util.Mod(_animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
        }
        //Debug.Log(_cycleDuration);

        firstFrame = false;

        _currentTime = Time.time;
    }

    private void LateUpdate()
    {
        if (Time.time == 0 || Time.timeScale == 0) return;
        //updateVelocity();
        tr.ControlledLateUpdate();
        _position = tr.position;
        _rotation = tr.rotation;

        _up = _rotation * Vector3.up;
        _forward = _rotation * Vector3.forward;
        Vector3 _right = _rotation * Vector3.right;

        if (_currentTime != Time.time) return;

        for (int leg = 0; leg < _bone.Limbs.Count; leg++)
        {
            //Debug.Log(string.Format(leg + "{0:F6}", _legState[leg].stanceTime));
            float designatedCycleTime = Cyclic(normalizeTime, _legState[leg].stanceTime, 1, false);
            //Debug.Log(designatedCycleTime);

            bool newStep = false;
            if (designatedCycleTime < _legState[leg].designatedCycleTimePrev - 0.5f)
            {
                newStep = true;
                if (!_legState[leg].parked)
                {
                    _legState[leg].prevNextFootPrintTime = _legState[leg].nextFootPrintTime;
                    _legState[leg].prevNextStepPosition = _legState[leg].nextStepPosition;
                    _legState[leg].prevNextFootRotateMatrix = _legState[leg].nextFootRotateMatrix;
                    _legState[leg].cycleTime = designatedCycleTime;
                }
                _legState[leg].parked = false;
            }
            _legState[leg].designatedCycleTimePrev = designatedCycleTime;

            _legState[leg].nextFootPrintTime = Time.time + (_cycleDuration * (1 - designatedCycleTime));

            float predictedStrikeTime = (_legState[leg].footStrikeTime - designatedCycleTime) * _cycleDuration;
            //Debug.Log(string.Format("{0:F6}", predictedStrikeTime));

            if (designatedCycleTime >= _legState[leg].footStrikeTime)
                _legState[leg].cycleTime = designatedCycleTime;
            else
            {
                //Debug.Log(string.Format("check"));
                _legState[leg].cycleTime += (_legState[leg].footStrikeTime - _legState[leg].cycleTime) * Time.deltaTime / predictedStrikeTime;
            }

            if (_legState[leg].cycleTime >= designatedCycleTime)
                _legState[leg].cycleTime = designatedCycleTime;
            //Debug.Log(string.Format("{0:F6}", _legState[leg].cycleTime));
            //Debug.Log(string.Format("{0:F6}", _legState[leg].footStrikeTime));
            if (_legState[leg].cycleTime < _legState[leg].footStrikeTime)
            {
                float flightTime = Mathf.InverseLerp(_legState[leg].footOffTime, _legState[leg].footStrikeTime, _legState[leg].cycleTime);
                //UnityEngine.Debug.Log(string.Format("{0:F6}", _legState[leg].footOffTime));
                Quaternion newPredictedRotation = Quaternion.AngleAxis(tr.angularVelocitySmoothed.magnitude * (_legState[leg].nextFootPrintTime - Time.time),
                    tr.angularVelocitySmoothed) * tr.rotation;

                Quaternion predictedRotation;
                if (_legState[leg].cycleTime <= _legState[leg].footOffTime)
                    predictedRotation = newPredictedRotation;
                else
                {
                    Quaternion oldPredictedRotation = QuaternionFromMatrix(_legState[leg].nextFootRotateMatrix);

                    oldPredictedRotation = Quaternion.FromToRotation(oldPredictedRotation * Vector3.up, _up) * oldPredictedRotation;

                    float rotationSeekSpeed = Mathf.Max(tr.angularVelocitySmoothed.magnitude * 3, maxStepRotation / maxStepDuration);
                    float maxRotateAngle = rotationSeekSpeed / flightTime * Time.deltaTime;
                    predictedRotation = ConstantSlerp(oldPredictedRotation, newPredictedRotation, maxRotateAngle);
                }

                Vector3 newStepPosition;

                float turnSpeed = Vector3.Dot(tr.angularVelocitySmoothed, _up);
                //Debug.Log(string.Format("{0:F6}", _legState[leg].standPosition));
                if (turnSpeed * _cycleDuration < 5)
                {
                    // Linear prediction if no turning
                    newStepPosition = (
                        tr.position
                        + predictedRotation * _legState[leg].standPosition
                        + tr.velocity * (_legState[leg].nextFootPrintTime - Time.time)
                    );
                    //Debug.Log(string.Format("{0:F6}", _legState[leg].standPosition));
                    //Debug.Log(string.Format("2 : {0:F6}", (Time.time)));
                }
                else
                {
                    // If character is turning, assume constant turning
                    // and do circle-based prediction
                    Vector3 turnCenter = Vector3.Cross(_up, tr.velocity) / (turnSpeed * Mathf.PI / 180);
                    Vector3 predPos = turnCenter + Quaternion.AngleAxis(
                        turnSpeed * (_legState[leg].nextFootPrintTime - Time.time),
                        _up
                    ) * -turnCenter;
                    //Debug.Log(string.Format("{0:F6}", predPos));
                    newStepPosition = (
                        tr.position
                        + predictedRotation * _legState[leg].standPosition
                        + predPos
                    );
                }
                newStepPosition = SetHeight(
                    newStepPosition, _position + 0f * _up, _up
                );
                //UnityEngine.Debug.Log(string.Format(leg + " {0:F6}", newStepPosition));

                // Get position and orientation projected onto the ground
                Matrix4x4 groundedBase = FindGroundedBase(
                    newStepPosition,
                    predictedRotation,
                    _legState[leg].toeToHeelVector,
                    true
                );
                //UnityEngine.Debug.Log(string.Format(leg + "{0:F6}", newStepPosition));
                newStepPosition = groundedBase.GetColumn(3);
                //UnityEngine.Debug.Log(string.Format("{0:F6}", newStepPosition));
                // Apply smoothing of predicted step position
                if (newStep)
                {
                    // No smoothing if foot hasn't lifted off the ground yet
                    _legState[leg].nextStepPosition = newStepPosition;
                    _legState[leg].nextStepPositionGoal = newStepPosition;
                }
                else
                {
                    float stepSeekSpeed = Mathf.Max(
                        speed * 3 + tr.accelerationSmoothed.magnitude / 10,
                        _bone.Limbs[leg].footLength * 3
                    );
                    //UnityEngine.Debug.Log(string.Format("{0:F6}", _bone.Limbs[leg].footLength));
                    float towardStrike = _legState[leg].cycleTime / _legState[leg].footStrikeTime;
                    //UnityEngine.Debug.Log(string.Format("{0:F6}", towardStrike));

                    // Evaluate if new potential goal is within reach
                    if (
                        (newStepPosition - _legState[leg].nextStepPosition).sqrMagnitude
                        < Mathf.Pow(stepSeekSpeed * ((1 / towardStrike) - 1), 2)
                    )
                    {
                        _legState[leg].nextStepPositionGoal = newStepPosition;
                    }
                    // Move towards goal - faster initially, then slower
                    Vector3 moveVector = _legState[leg].nextStepPositionGoal - _legState[leg].nextStepPosition;
                    //Debug.Log(string.Format(leg + "{0:F6}", moveVector));
                    //Debug.Log(string.Format("{0:F6}", moveVector));
                    if (moveVector != Vector3.zero && predictedStrikeTime > 0)
                    {
                        float moveVectorMag = moveVector.magnitude;
                        float moveDist = Mathf.Min(
                            moveVectorMag,
                            Mathf.Max(
                                stepSeekSpeed / Mathf.Max(0.1f, flightTime) * Time.deltaTime,
                                (1 + 2 * Mathf.Pow(towardStrike - 1, 2))
                                    * (Time.deltaTime / predictedStrikeTime)
                                    * moveVectorMag
                            )
                        );
                        _legState[leg].nextStepPosition += (
                            (_legState[leg].nextStepPositionGoal - _legState[leg].nextStepPosition)
                            / moveVectorMag * moveDist
                        );
                        //Debug.Log(string.Format(leg + "{0:F6}", moveVectorMag));
                    }
                }

                groundedBase.SetColumn(3, _legState[leg].nextStepPosition);
                groundedBase[3, 3] = 1;
                _legState[leg].nextFootRotateMatrix = groundedBase;
            }

            if (enableParking)
            {

                // Check if old and new footstep has
                // significant difference in position or rotation
                float distToNextStep = Vector3.ProjectOnPlane(
                    _legState[leg].nextStepPosition - _legState[leg].prevNextStepPosition, _up
                ).magnitude;
                //Debug.Log(string.Format(leg + "{0:F6}", distToNextStep));
                bool significantStepDifference = (
                    distToNextStep > minStepDistance
                    ||
                    Vector3.Angle(
                        _legState[leg].nextFootRotateMatrix.GetColumn(2),
                        _legState[leg].prevNextFootRotateMatrix.GetColumn(2)
                    ) > maxStepRotation / 2
                );

                // Park foot's cycle if the step length/rotation is below threshold
                if (newStep && !significantStepDifference)
                {
                    _legState[leg].parked = true;
                }

                // Allow unparking during first part of cycle if the
                // step length/rotation is now above threshold
                if (
                    _legState[leg].parked
                    //&& ( _legState[leg].cycleTime < 0.5f )
                    && (designatedCycleTime < 0.67f)
                    && significantStepDifference
                )
                {
                    _legState[leg].parked = false;
                }

                if (_legState[leg].parked)
                {
                _legState[leg].cycleTime = 0;
                    //UnityEngine.Debug.Log("check");
                }
            }
        }

        // Calculate base point
        Vector3 tangentDir = Quaternion.Inverse(tr.rotation) * tr.velocity;
        // This is in object space, so OK to set y to 0
        tangentDir.y = 0;
        if (tangentDir.sqrMagnitude > 0) tangentDir = tangentDir.normalized;

        Vector3[] basePointFoot = new Vector3[_bone.Limbs.Count];
        Vector3 basePoint = Vector3.zero;
        Vector3 baseVel = Vector3.zero;
        Vector3 avgFootPoint = Vector3.zero;
        float baseSummedWeight = 0.0f;
        for (int leg = 0; leg < _bone.Limbs.Count; leg++)
        {
            // Calculate base position (starts and ends in tangent to surface)

            // weight goes 1 -> 0 -> 1 as cycleTime goes from 0 to 1
            float weight = Mathf.Cos(_legState[leg].cycleTime * 2 * Mathf.PI) / 2.0f + 0.5f;
            baseSummedWeight += weight + 0.001f;

            // Value from 0.0 at lift time to 1.0 at land time
            float strideTime = 0f;
            float strideSCurve = -Mathf.Cos(strideTime * Mathf.PI) / 2f + 0.5f;

            Vector3 stepBodyPoint = transform.TransformDirection(-_legState[leg].standPosition);

            basePointFoot[leg] = (
                (
                    _legState[leg].prevNextStepPosition
                    + _legState[leg].prevNextFootRotateMatrix.MultiplyVector(tangentDir)
                        * _cycleDistance * _legState[leg].cycleTime
                ) * (1 - strideSCurve)
                + (
                    _legState[leg].nextStepPosition
                    + _legState[leg].nextFootRotateMatrix.MultiplyVector(tangentDir)
                        * _cycleDistance * (_legState[leg].cycleTime - 1)
                ) * strideSCurve
            );

            if (System.Single.IsNaN(basePointFoot[leg].x) || System.Single.IsNaN(basePointFoot[leg].y) || System.Single.IsNaN(basePointFoot[leg].z))
            {
                UnityEngine.Debug.LogError("_legState[leg].cycleTime=" + _legState[leg].cycleTime + ", strideSCurve=" + strideSCurve + ", tangentDir=" + tangentDir + ", cycleDistance=" + _cycleDistance + ", _legState[leg].stepFromPosition=" + _legState[leg].prevNextStepPosition + ", _legState[leg].stepToPosition=" + _legState[leg].nextStepPosition + ", _legState[leg].stepToMatrix.MultiplyVector(tangentDir)=" + _legState[leg].nextFootRotateMatrix.MultiplyVector(tangentDir) + ", _legState[leg].stepFromMatrix.MultiplyVector(tangentDir)=" + _legState[leg].prevNextFootRotateMatrix.MultiplyVector(tangentDir));
            }

            basePoint += (basePointFoot[leg] + stepBodyPoint) * (weight + 0.001f);
            avgFootPoint += basePointFoot[leg];

            baseVel += (_legState[leg].nextStepPosition - _legState[leg].prevNextStepPosition) * (1f - weight + 0.001f);
        }
        avgFootPoint /= _bone.Limbs.Count;
        basePoint /= baseSummedWeight;
        if (
            System.Single.IsNaN(basePoint.x)
            || System.Single.IsNaN(basePoint.y)
            || System.Single.IsNaN(basePoint.z)
        ) basePoint = _position;

        Vector3 groundBasePoint = basePoint + _up * 0f;

        // Calculate base up vector
        Vector3 baseUp = _up;
        if (groundHugX >= 0 || groundHugZ >= 0)
        {

            // Ground-based Base Up Vector
            Vector3 baseUpGroundNew = _up * 0.1f;
            for (int leg = 0; leg < _bone.Limbs.Count; leg++)
            {
                Vector3 vec = (basePointFoot[leg] - avgFootPoint);
                baseUpGroundNew += Vector3.Cross(Vector3.Cross(vec, _baseUpGround), vec);
                //UnityEngine.Debug.DrawLine(basePointFoot[leg], avgFootPoint);
            }

            //Assert(up.magnitude>0, "up has zero length");
            //Assert(baseUpGroundNew.magnitude>0, "baseUpGroundNew has zero length");
            //Assert(Vector3.Dot(baseUpGroundNew,up)!=0, "baseUpGroundNew and up are perpendicular");
            float baseUpGroundNewUpPart = Vector3.Dot(baseUpGroundNew, _up);
            if (baseUpGroundNewUpPart > 0)
            {
                // Scale vector such that vertical element has length of 1
                baseUpGroundNew /= baseUpGroundNewUpPart;
                _baseUpGround = baseUpGroundNew;
            }

            if (groundHugX >= 1 && groundHugZ >= 1)
            {
                baseUp = _baseUpGround.normalized;
            }
            else
            {
                baseUp = (
                    _up
                    + groundHugX * Vector3.Project(_baseUpGround, _right)
                    + groundHugZ * Vector3.Project(_baseUpGround, _forward)
                ).normalized;
            }
        }

        // Velocity-based Base Up Vector
        Vector3 baseUpVel = _up;
        if (baseVel != Vector3.zero) baseUpVel = Vector3.Cross(baseVel, Vector3.Cross(_up, baseVel));
        // Scale vector such that vertical element has length of 1
        baseUpVel /= Vector3.Dot(baseUpVel, _up);

        // Calculate acceleration direction in local XZ plane
        Vector3 accelerationDir = Vector3.zero;
        if (accelerateTiltAmount * accelerateTiltSensitivity != 0)
        {
            float accelX = Vector3.Dot(
                tr.accelerationSmoothed * accelerateTiltSensitivity * accelerateTiltAmount,
                _right
            ) * (1 - groundHugX);
            float accelZ = Vector3.Dot(
                tr.accelerationSmoothed * accelerateTiltSensitivity * accelerateTiltAmount,
                _forward
            ) * (1 - groundHugZ);
            accelerationTiltX = Mathf.Lerp(accelerationTiltX, accelX, Time.deltaTime * 10);
            accelerationTiltZ = Mathf.Lerp(accelerationTiltZ, accelZ, Time.deltaTime * 10);
            accelerationDir = (
                (accelerationTiltX * _right + accelerationTiltZ * _forward)
                // a curve that goes towards 1 as speed goes towards infinity:
                * (1 - 1 / (_hSpeedSmooth * accelerateTiltSensitivity + 1))
            );
        }

        // Calculate tilting direction in local XZ plane
        Vector3 tiltDir = Vector3.zero;
        if (climbTiltAmount * climbTiltAmount != 0)
        {
            tiltDir = (
                (
                    Vector3.Project(baseUpVel, _right) * (1 - groundHugX)
                    + Vector3.Project(baseUpVel, _forward) * (1 - groundHugZ)
                ) * -climbTiltAmount
                // a curve that goes towards 1 as speed goes towards infinity:
                * (1 - 1 / (_hSpeedSmooth * climbTiltSensitivity + 1))
            );
        }

        // Up vector and rotations for the torso
        bodyUp = (baseUp + accelerationDir + tiltDir).normalized;
        Quaternion bodyRotation = Quaternion.AngleAxis(
            Vector3.Angle(_up, bodyUp),
            Vector3.Cross(_up, bodyUp)
        );

        // Up vector and rotation for the legs
        legsUp = (_up + accelerationDir).normalized;
        Quaternion legsRotation = Quaternion.AngleAxis(
            Vector3.Angle(_up, legsUp),
            Vector3.Cross(_up, legsUp)
        );

        for (int leg = 0; leg < _bone.Limbs.Count; leg++)
        {
            // Value from 0.0 at liftoff time to 1.0 at strike time
            float flightTime = Mathf.InverseLerp(
                _legState[leg].footOffTime, _legState[leg].footStrikeTime, _legState[leg].cycleTime);

            // Value from 0.0 at lift time to 1.0 at land time
            float strideTime = Mathf.InverseLerp(
                0.1f, 0.9f, _legState[leg].cycleTime);

            int phase;
            float phaseTime = 0;
            if (_legState[leg].cycleTime < _legState[leg].footOffTime)
            {
                phase = 0; phaseTime = Mathf.InverseLerp(
                    0, _legState[leg].footOffTime, _legState[leg].cycleTime
                );
            }
            else if (_legState[leg].cycleTime > _legState[leg].footStrikeTime)
            {
                phase = 2; phaseTime = Mathf.InverseLerp(
                    _legState[leg].footStrikeTime, 1, _legState[leg].cycleTime
                );
            }
            else
            {
                phase = 1; phaseTime = flightTime;

            }

            // Calculate foot position on foot flight path from old to new step
            Vector3 flightPos = Vector3.zero;
            for (int m = 0; m < _animationClips.Length; m++)
            {
                if (_motionWeights[m] > 0)
                {
                    flightPos += _bone._motionAnalysis.GetFlightFootPosition(leg, phaseTime, phase, m);
                }
            }
            //Debug.Log(string.Format(leg + " {0:F6}", phaseTime));

            // Start and end point at step from and step to positions
            Vector3 pointFrom = _legState[leg].prevNextStepPosition;
            Vector3 pointTo = _legState[leg].nextStepPosition;
            Vector3 normalFrom = _legState[leg].prevNextFootRotateMatrix.MultiplyVector(Vector3.up);
            Vector3 normalTo = _legState[leg].nextFootRotateMatrix.MultiplyVector(Vector3.up);

            float flightProgressionLift = Mathf.Sin(flightPos.z * Mathf.PI);
            float flightTimeLift = Mathf.Sin(flightTime * Mathf.PI);

            // Calculate horizontal part of flight paths
            _legState[leg].footBase = pointFrom * (1 - flightPos.z) + pointTo * flightPos.z;
            //Debug.Log(string.Format("{0:F6}", _legState[leg].footBase));

            Vector3 offset =
                tr.position + tr.rotation * _legState[leg].standPosition
                - Vector3.Lerp(pointFrom, pointTo, _legState[leg].cycleTime);

            _legState[leg].footBase += Vector3.ProjectOnPlane(offset * flightProgressionLift, legsUp);

            // Calculate vertical part of flight paths
            Vector3 midPoint = (pointFrom + pointTo) / 2;
            float tangentHeightFrom = (
                Vector3.Dot(normalFrom, pointFrom - midPoint)
                / Vector3.Dot(normalFrom, legsUp)
            );
            float tangentHeightTo = (
                Vector3.Dot(normalTo, pointTo - midPoint)
                / Vector3.Dot(normalTo, legsUp)
            );
            float heightMidOffset = Mathf.Max(tangentHeightFrom, tangentHeightTo) * 2 / Mathf.PI;

            _legState[leg].footBase += Mathf.Max(0, heightMidOffset * flightProgressionLift - flightPos.y) * legsUp;

            // Footbase rotation

            Quaternion footBaseRotationFromSteps = Quaternion.Slerp(
                QuaternionFromMatrix(_legState[leg].prevNextFootRotateMatrix),
                QuaternionFromMatrix(_legState[leg].nextFootRotateMatrix),
                flightTime
            );

            if (strideTime < 0.5)
            {
                _legState[leg].footBaseRotation = Quaternion.Slerp(
                    QuaternionFromMatrix(_legState[leg].prevNextFootRotateMatrix),
                    _rotation,
                    strideTime * 2
                );
                //Debug.Log("1" + _legState[leg].footBaseRotation);
            }
            else
            {
                _legState[leg].footBaseRotation = Quaternion.Slerp(
                    _rotation,
                    QuaternionFromMatrix(_legState[leg].nextFootRotateMatrix),
                    strideTime * 2 - 1
                );
                //Debug.Log("2" + QuaternionFromMatrix(_legState[leg].nextFootRotateMatrix));
            }

            float footRotationAngle = Quaternion.Angle(_rotation, _legState[leg].footBaseRotation);
            if (footRotationAngle > maxFootRotationAngle)
            {
                _legState[leg].footBaseRotation = Quaternion.Slerp(
                    _rotation,
                    _legState[leg].footBaseRotation,
                    maxFootRotationAngle / footRotationAngle
                );
                //Debug.Log("3" + _legState[0].footBaseRotation);
            }

            _legState[leg].footBaseRotation = Quaternion.FromToRotation(
                _legState[leg].footBaseRotation * Vector3.up,
                footBaseRotationFromSteps * Vector3.up
            ) * _legState[leg].footBaseRotation;
            //Debug.Log("4" + _legState[0].footBaseRotation);

            // Elevate feet according to flight pas from keyframed animation
            _legState[leg].footBase += flightPos.y * legsUp;

            // Offset feet sideways according to flight pas from keyframed animation
            Vector3 stepRight = Vector3.Cross(legsUp, pointTo - pointFrom).normalized;
            _legState[leg].footBase += flightPos.x * stepRight;

            // Smooth lift that elevates feet in the air based on height of feet on the ground.
            Vector3 footBaseElevated = Vector3.Lerp(
                _legState[leg].footBase,
                SetHeight(_legState[leg].footBase, groundBasePoint, legsUp),
                flightTimeLift
            );

            if (Vector3.Dot(footBaseElevated, legsUp) > Vector3.Dot(_legState[leg].footBase, legsUp))
            {
                _legState[leg].footBase = footBaseElevated;
            }
        }

        for (int leg = 0; leg < _bone.Limbs.Count; leg++)
        {
            Vector3 footBaseReference = (
                -MotionAnalysis.GetHeelOffset(
                    _bone.Limbs[leg].AnkleBone, _bone.Limbs[leg].ankleToHeelVector2,
                    _bone.Limbs[leg].TipBone, _bone.Limbs[leg].ankleToToeVector2,
                    _legState[leg].toeToHeelVector,
                    _legState[leg].footBaseRotation
                )
                + _bone.Limbs[leg].AnkleBone.TransformPoint(_bone.Limbs[leg].ankleToHeelVector2)
            );

            if (locomotionWeight < 1)
            {
                _legState[leg].footBase = Vector3.Lerp(
                    footBaseReference,
                    _legState[leg].footBase,
                    locomotionWeight
                );
                _legState[leg].footBaseRotation = Quaternion.Slerp(
                    _rotation,
                    _legState[leg].footBaseRotation,
                    locomotionWeight
                );
                //Debug.Log("5" + _legState[0].footBaseRotation);
            }

            _legState[leg].footBase = Vector3.MoveTowards(
                footBaseReference,
                _legState[leg].footBase,
                maxIKAdjustmentDistance
            );
        }

        // Apply body rotation
        _bone.MainBone.transform.rotation = (
            tr.rotation * Quaternion.Inverse(transform.rotation)
            * bodyRotation
            * _bone.MainBone.transform.rotation
        );
        for (int leg = 0; leg < _bone.Limbs.Count; leg++)
        {
            _bone.Limbs[leg].RootBone.rotation = legsRotation * Quaternion.Inverse(bodyRotation) * _bone.Limbs[leg].RootBone.rotation;
        }

        // Apply root offset based on body rotation
        Vector3 rootPoint = _bone.MainBone.transform.position;
        Vector3 hipAverage = transform.TransformPoint(Vector3.zero);
        Vector3 hipAverageGround = transform.TransformPoint(Vector3.zero);
        Vector3 rootPointAdjusted = rootPoint;
        rootPointAdjusted += bodyRotation * (rootPoint - hipAverage) - (rootPoint - hipAverage);
        rootPointAdjusted += legsRotation * (hipAverage - hipAverageGround) - (hipAverage - hipAverageGround);
        _bone.MainBone.transform.position = rootPointAdjusted + _position - transform.position;

        for (int leg = 0; leg < _bone.Limbs.Count; leg++)
        {
            _legState[leg].hipReference = _bone.Limbs[leg].RootBone.position;
            _legState[leg].ankleReference = _bone.Limbs[leg].AnkleBone.position;
        }
        
        // Adjust legs in two passes
        // First pass is to find approximate place of hips and ankles
        // Second pass is to adjust ankles based on local angles found in first pass
        for (int pass = 1; pass <= 2; pass++)
        {
            // Find the ankle position for each leg
            for (int leg = 0; leg < _bone.Limbs.Count; leg++)
            {
                _legState[leg].ankle = MotionAnalysis.GetAnklePosition(
                    _bone.Limbs[leg].AnkleBone, _bone.Limbs[leg].ankleToHeelVector2,
                    _bone.Limbs[leg].TipBone, _bone.Limbs[leg].ankleToToeVector2,
                    _legState[leg].toeToHeelVector,
                    _legState[leg].footBase, _legState[leg].footBaseRotation
                );
            }
            //Debug.Log(string.Format("{0:F6}", _legState[0].footBase));

            // Find and apply the hip offset
            //FindHipOffset();
            //Debug.Log("6" + _legState[0].footBaseRotation);
            //// Adjust the legs according to the found ankle and hip positions
            for (int leg = 0; leg < _bone.Limbs.Count; leg++) { AdjustLeg(leg, _legState[leg].ankle, pass == 2); }
        }
    }

    private Vector3 CalculateAngular(Quaternion pre, Quaternion now)
    {
        Quaternion deltaRotation = Quaternion.Inverse(pre) * now;
        float angle = 0.0f;
        Vector3 axis = Vector3.zero;
        deltaRotation.ToAngleAxis(out angle, out axis);
        if (axis == Vector3.zero || axis.x == Mathf.Infinity || axis.x == Mathf.NegativeInfinity)
            return Vector3.zero;
        if (angle > 180) angle -= 360;
        angle = angle / Time.deltaTime;
        return axis.normalized * angle;
    }

    private float Cyclic(float high, float low, float period, bool skipWarp)
    {
        if(!skipWarp)
        {
            high = high % period >= 0 ? high % period : high % period + period;
            low = low % period >= 0 ? low % period : low % period + period;
        }
        return (high >= low ? high - low : high + period - low);
    }

    private static Quaternion ConstantSlerp(Quaternion from, Quaternion to, float angle)
    {
        float value = Mathf.Min(1, angle / Quaternion.Angle(from, to));
        return Quaternion.Slerp(from, to, value);
    }

    public static Quaternion QuaternionFromMatrix(Matrix4x4 m)
    {
        // Adapted from: http://www.euclideanspace.com/maths/geometry/rotations/conversions/matrixToQuaternion/index.htm
        Quaternion q = new Quaternion();
        q.w = Mathf.Sqrt(Mathf.Max(0, 1 + m[0, 0] + m[1, 1] + m[2, 2])) / 2;
        q.x = Mathf.Sqrt(Mathf.Max(0, 1 + m[0, 0] - m[1, 1] - m[2, 2])) / 2;
        q.y = Mathf.Sqrt(Mathf.Max(0, 1 - m[0, 0] + m[1, 1] - m[2, 2])) / 2;
        q.z = Mathf.Sqrt(Mathf.Max(0, 1 - m[0, 0] - m[1, 1] + m[2, 2])) / 2;
        q.x *= Mathf.Sign(q.x * (m[2, 1] - m[1, 2]));
        q.y *= Mathf.Sign(q.y * (m[0, 2] - m[2, 0]));
        q.z *= Mathf.Sign(q.z * (m[1, 0] - m[0, 1]));
        return q;
    }

    public static Vector3 SetHeight(Vector3 originalVector, Vector3 referenceHeightVector, Vector3 upVector)
    {
        Vector3 originalOnPlane = Vector3.ProjectOnPlane(originalVector, upVector);
        Vector3 referenceOnAxis = Vector3.Project(referenceHeightVector, upVector);
        return originalOnPlane + referenceOnAxis;
    }

    public Matrix4x4 FindGroundedBase(
        Vector3 pos, Quaternion rot, Vector3 heelToToetipVector, bool avoidLedges
    )
    {
        RaycastHit hit;
        //UnityEngine.Debug.Log(string.Format("{0:F6}", heelToToetipVector));
        // Trace rays
        Vector3 hitAPoint = new Vector3();
        Vector3 hitBPoint = new Vector3();
        Vector3 hitANormal = new Vector3();
        Vector3 hitBNormal = new Vector3();
        bool hitA = false;
        bool hitB = false;
        bool valid = false;

        if (Physics.Raycast(
            pos + _up * maxStepHeight,
            -_up, out hit, maxStepHeight * 2, groundLayers)
        )
        {
            valid = true;
            hitAPoint = hit.point;
            // Ignore surface normal if it deviates too much
            if (Vector3.Angle(hit.normal, _up) < maxSlopeAngle)
            {
                hitANormal = hit.normal; hitA = true;
            }
        }

        Vector3 heelToToetip = rot * heelToToetipVector;
        float footLength = heelToToetip.magnitude;

        if (Physics.Raycast(
            pos + _up * maxStepHeight + heelToToetip,
            -_up, out hit, maxStepHeight * 2, groundLayers)
        )
        {
            valid = true;
            hitBPoint = hit.point;
            // Ignore surface normal if it deviates too much
            if (Vector3.Angle(hit.normal, _up) < maxSlopeAngle)
            {
                hitBNormal = hit.normal; hitB = true;
            }
        }

        if (!valid)
        {
            Matrix4x4 m = Matrix4x4.identity;
            m.SetTRS(pos, rot, Vector3.one);
            return m;
        }

        // Choose which raycast result to use
        bool exclusive = false;
        if (avoidLedges)
        {
            if (!hitA && !hitB) hitA = true;
            else if (hitA && hitB)
            {
                Vector3 avgNormal = (hitANormal + hitBNormal).normalized;
                float hA = Vector3.Dot(hitAPoint, avgNormal);
                float hB = Vector3.Dot(hitBPoint, avgNormal);
                if (hA >= hB) hitB = false;
                else hitA = false;
                if (Mathf.Abs(hA - hB) > footLength / 4) exclusive = true;
            }
            else exclusive = true;
        }

        Vector3 newStepPosition;

        Vector3 stepUp = rot * Vector3.up;

        // Apply result of raycast
        if (hitA)
        {
            if (hitANormal != Vector3.zero)
            {
                rot = Quaternion.FromToRotation(stepUp, hitANormal) * rot;
            }
            newStepPosition = hitAPoint;
            if (exclusive)
            {
                heelToToetip = rot * heelToToetipVector;
                newStepPosition -= heelToToetip * 0.5f;
            }
        }
        else
        {
            if (hitBNormal != Vector3.zero)
            {
                rot = Quaternion.FromToRotation(stepUp, hitBNormal) * rot;
            }
            heelToToetip = rot * heelToToetipVector;
            newStepPosition = hitBPoint - heelToToetip;
            if (exclusive) { newStepPosition += heelToToetip * 0.5f; }
        }

        return MatrixFromQuaternionPosition(rot, newStepPosition);
    }

    public static Matrix4x4 MatrixFromQuaternionPosition(Quaternion q, Vector3 p)
    {
        Matrix4x4 m = MatrixFromQuaternion(q);
        m.SetColumn(3, p);
        m[3, 3] = 1;
        return m;
    }

    public static Matrix4x4 MatrixFromQuaternion(Quaternion q)
    {
        return CreateMatrix(q * Vector3.right, q * Vector3.up, q * Vector3.forward, Vector3.zero);
    }

    public static Matrix4x4 CreateMatrix(Vector3 right, Vector3 up, Vector3 forward, Vector3 position)
    {
        Matrix4x4 m = Matrix4x4.identity;
        m.SetColumn(0, right);
        m.SetColumn(1, up);
        m.SetColumn(2, forward);
        m.SetColumn(3, position);
        m[3, 3] = 1;
        return m;
    }

    public void AdjustLeg(int leg, Vector3 desiredAnklePosition, bool secondPass)
    {
        // Store original foot alignment
        Quaternion qAnkleOrigRotation;
        if (!secondPass)
        {
            // Footbase rotation in character space
            Quaternion objectToFootBaseRotation = _legState[leg].footBaseRotation * Quaternion.Inverse(_rotation);
            qAnkleOrigRotation = objectToFootBaseRotation * _bone.Limbs[leg].AnkleBone.rotation;
        }
        else
        {
            qAnkleOrigRotation = _bone.Limbs[leg].AnkleBone.rotation;
        }
        //Debug.Log(qAnkleOrigRotation);

        // Choose IK solver
        IKSolver ikSolver;
        if (_bone.Limbs[leg].legChain.Length == 3) ikSolver = new IK1JointAnalytic();
        ikSolver = new IKSimple();
        //Debug.Log(desiredAnklePosition);
        // Solve the inverse kinematics
        //Debug.Log(_bone.Limbs[leg].legChain.Length);
        ikSolver.Solve(_bone.Limbs[leg].legChain, desiredAnklePosition);

        // Calculate the desired new joint positions
        Vector3 pHip = _bone.Limbs[leg].RootBone.position;
        Vector3 pAnkle = _bone.Limbs[leg].AnkleBone.position;

        if (!secondPass)
        {
            // Find alignment that is only rotates in horizontal plane
            // and keeps local ankle angle
            Quaternion horizontalRotation = Quaternion.FromToRotation(
                _forward,
                Vector3.ProjectOnPlane(_legState[leg].footBaseRotation * Vector3.forward, _up)
            ) * _bone.Limbs[leg].AnkleBone.rotation;
            //Debug.Log(horizontalRotation);
            // Apply original foot alignment when foot is grounded
            _bone.Limbs[leg].AnkleBone.rotation = Quaternion.Slerp(
                horizontalRotation, // only horizontal rotation (keep local angle)
                qAnkleOrigRotation, // rotates to slope of ground
                1 - _legState[leg].GetFootGrounding(_legState[leg].cycleTime)
            );
        }
        else
        {
            // Rotate leg around hip-ankle axis by half amount of what the foot is rotated
            Vector3 hipAnkleVector = pAnkle - pHip;
            Quaternion legAxisRotate = Quaternion.Slerp(
                Quaternion.identity,
                Quaternion.FromToRotation(
                    Vector3.ProjectOnPlane(_forward, hipAnkleVector),
                    Vector3.ProjectOnPlane(_legState[leg].footBaseRotation * Vector3.forward, hipAnkleVector)
                ),
                0.5f
            );
            _bone.Limbs[leg].RootBone.rotation = legAxisRotate * _bone.Limbs[leg].RootBone.rotation;

            // Apply foot alignment found in first pass
            _bone.Limbs[leg].AnkleBone.rotation = qAnkleOrigRotation;
        }
    }

    private void OnRenderObject()
    {
        if (footMarker) RenderFootMarkers();
    }

    //void updateVelocity()
    //{
    //    if (Time.deltaTime == 0) return;

    //    _position = _transform.position;
    //    _rotation = _transform.rotation;
    //    _velocity = (_position - _prePosition) / Time.deltaTime;
    //    _angularVelocity = CalculateAngular(_preRotation, _rotation);
    //    //UnityEngine.Debug.Log(string.Format("{0:F6}", _angularVelocity));
    //    _accleration = (_velocity - _preVelocity) / Time.deltaTime;
    //    _prePosition = _position;
    //    _preRotation = _rotation;
    //    _preVelocity = _velocity;

    //    _velocitySmooth = Vector3.Lerp(_velocitySmooth, _velocity, Time.deltaTime * 10);

    //    _angularVelocitySmooth = Vector3.Lerp(_angularVelocitySmooth, _angularVelocity, Time.deltaTime * 3);

    //    _acceleSmooth = Vector3.Lerp(_acceleSmooth, _accleration, Time.deltaTime * 3);
    //}

    public void RenderFootMarkers()
    {
        GL.Begin(GL.LINES);

        GL.End();
        GL.Begin(GL.QUADS);
        Vector3 heel, forward, up, right;
        Matrix4x4 m;
        for (int leg = 0; leg < _bone.Limbs.Count; leg++)
        {
            for (int step = 0; step < 3; step++)
            {
                if (_legState[leg] == null) continue;
                if (step == 0)
                {
                    m = _legState[leg].prevNextFootRotateMatrix;
                    GL.Color(Color.green * 0.8f);
                }
                else if (step == 1)
                {
                    m = _legState[leg].nextFootRotateMatrix;
                    GL.Color(Color.green);
                }
                else
                {
                    m = _legState[leg].nextFootRotateMatrix;
                    //GL.Color(legs[leg].debugColor);
                }

                // Draw foot marker
                heel = m.MultiplyPoint3x4(Vector3.zero);
                forward = m.MultiplyVector(_legState[leg].toeToHeelVector);
                up = m.MultiplyVector(Vector3.up);
                right = (Quaternion.AngleAxis(90, up) * forward).normalized * _bone.Limbs[leg].footWidth;
                heel += up.normalized * right.magnitude / 20;
                if (step == 2) { heel += _legState[leg].nextStepPositionGoal - _legState[leg].nextStepPosition; }
                //UnityEngine.Debug.Log(string.Format(leg + "{0:F6}", _legState[leg].nextStepPositionGoal - _legState[leg].nextStepPosition));
                GL.Vertex(heel + right / 2);
                GL.Vertex(heel - right / 2);
                GL.Vertex(heel - right / 4 + forward);
                GL.Vertex(heel + right / 4 + forward);
                if (leg == 0 && step == 0)
                {
                    a = Vector3.zero;
                    b = Vector3.zero;
                    a = heel;
                }
                else if (leg == 0 && step == 1)
                    b = heel;
                if (a.magnitude > 0 && b.magnitude > 0 && leg == 0) ;
                   // UnityEngine.Debug.Log(string.Format(leg + "{0:F6}", a - b));
            }
        }
        GL.End();
    }
}
