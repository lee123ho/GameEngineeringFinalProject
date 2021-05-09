using UnityEngine;
using UnityEditor;

public class AnalyzeEditor : MonoBehaviour
{
    [MenuItem("Analyze/Analyze")]
    static void Helo()
    {
        Bone _bone = GameObject.Find("1234").GetComponent<Bone>();
        _bone.Init();
    }
}
