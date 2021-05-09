using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Bone))]
public class FootLineEditor : Editor
{
    Bone _bone;

    private void OnEnable()
    {
        _bone = (Bone) target;
    }

    private void OnSceneGUI()
    {
        _bone.InitFoot();

        Handles.color = (Color.magenta * Color.white);
        for (int leg = 0; leg < _bone.Limbs.Count; leg++)
        {
            if (_bone.Limbs[leg].TipBone == null) continue;
            if (_bone.Limbs[leg].AnkleBone == null) continue;
            if (_bone.Limbs[leg].footLength == 0) continue;
            if (_bone.Limbs[leg].footWidth == 0) continue;
            Vector3 heel = _bone.Limbs[leg].ankleToHeelVector + _bone.transform.position;
            Vector3 toe = _bone.Limbs[leg].ankleToToeVector + _bone.transform.position;
            Vector3 side = (Quaternion.AngleAxis(90, _bone.transform.up) * (toe - heel)).normalized * _bone.Limbs[leg].footWidth;
            Handles.DrawLine(heel + side / 2, toe + side / 2);
            Handles.DrawLine(heel - side / 2, toe - side / 2);
            Handles.DrawLine(heel - side / 2, heel + side / 2);
            Handles.DrawLine(toe - side / 2, toe + side / 2);
        }
    }
}
