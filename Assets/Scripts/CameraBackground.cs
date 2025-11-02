using UnityEngine;

[DefaultExecutionOrder(10000)]
public class ForceCameraBgLast : MonoBehaviour
{
    public Camera cam;
    public Color bg = new Color32(0x10, 0x6A, 0xA9, 0xFF); // 106AA9

    void Awake()    { if (!cam) cam = Camera.main; }
    void LateUpdate(){ cam.backgroundColor = bg; }         // позже большинства скриптов
    void OnPreCull(){ cam.backgroundColor = bg; }          // позже Timeline/Animator обычно не доходит
}