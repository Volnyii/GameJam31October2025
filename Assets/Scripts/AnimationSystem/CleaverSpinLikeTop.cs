using UnityEngine;

public class CleaverSpinLikeTop : MonoBehaviour
{
    public float spinSpeed = 720f;
    public Vector3 localAxis = Vector3.up;

    void Update()
    {
        transform.Rotate(localAxis, spinSpeed * Time.deltaTime, Space.Self);
    }
}