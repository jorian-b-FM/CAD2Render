using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utils
{
    static float mmToUnityDistanceScale = 0.01f;
    public static void setUnityScale(float newValue)
    {
        if (newValue <= 0)
            mmToUnityDistanceScale = 0.01f;
        else
            mmToUnityDistanceScale = newValue;
    }
    public static float convertUnityToMm(float value) { return value / mmToUnityDistanceScale; }
    public static float convertMmToUnity(float value) { return value * mmToUnityDistanceScale; }
    public static Vector3 convertUnityToMm(Vector3 value) { return value / mmToUnityDistanceScale; }
    public static Vector3 convertMmToUnity(Vector3 value) { return value * mmToUnityDistanceScale; }

    // From https://forum.unity.com/threads/how-to-assign-matrix4x4-to-transform.121966/
    public static Vector3 GetTranslation(this Matrix4x4 m)
    {
        Vector3 position;
        position.x = m.m03;
        position.y = m.m13;
        position.z = m.m23;
        return position;
    }

    // From https://forum.unity.com/threads/how-to-assign-matrix4x4-to-transform.121966/
    public static Quaternion GetRotation(this Matrix4x4 m)
    {
        Vector3 forward;
        forward.x = m.m02;
        forward.y = m.m12;
        forward.z = m.m22;

        Vector3 upwards;
        upwards.x = m.m01;
        upwards.y = m.m11;
        upwards.z = m.m21;

        return Quaternion.LookRotation(forward, upwards);
    }

    // From https://forum.unity.com/threads/how-to-assign-matrix4x4-to-transform.121966/
    public static Vector3 GetScale(this Matrix4x4 matrix)
    {
        Vector3 scale;
        scale.x = new Vector4(matrix.m00, matrix.m10, matrix.m20, matrix.m30).magnitude;
        scale.y = new Vector4(matrix.m01, matrix.m11, matrix.m21, matrix.m31).magnitude;
        scale.z = new Vector4(matrix.m02, matrix.m12, matrix.m22, matrix.m32).magnitude;
        return scale;
    }

}
