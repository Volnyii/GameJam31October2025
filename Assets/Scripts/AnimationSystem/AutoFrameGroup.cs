using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class AutoFrameGroup : MonoBehaviour
{
    [Header("Кого держим в кадре")]
    public List<Transform> targets = new List<Transform>();

    [Header("Параметры кадрирования")]
    [Tooltip("Запас рамки вокруг группы (множитель, >= 1)")]
    [Min(1f)] public float padding = 1.2f;

    [Tooltip("Скорость движения камеры")]
    [Min(0f)] public float moveLerp = 5f;

    [Tooltip("Скорость зума (ортографическая/FOV/дистанция)")]
    [Min(0f)] public float zoomLerp = 5f;

    [Header("Масштаб фокуса")]
    [Tooltip("Во сколько раз сделать кадр крупнее при фокусе (>1 = сильнее приблизить)")]
    [Min(1f)] public float focusScale = 1.5f;

    [Header("Ограничения (чтобы не было бесконечного зума)")]
    [Tooltip("Мин. Orthographic Size (2D)")]
    [Min(0.0001f)] public float minOrthoSize = 1f;
    [Tooltip("Макс. Orthographic Size (2D). 0 = без ограничений")]
    [Min(0f)]      public float maxOrthoSize = 0f;

    [Tooltip("Мин. дистанция до цели (3D)")]
    [Min(0.01f)] public float minDistance = 1f;
    [Tooltip("Макс. дистанция до цели (3D). 0 = без ограничений")]
    [Min(0f)]    public float maxDistance = 0f;

    [Tooltip("Минимальный «физический» размер bounds, при котором считаем группу точкой")]
    [Min(0f)] public float boundsEpsilon = 0.01f;

    [Header("Режим")]
    [Tooltip("Пока включено — держим группу в кадре с указанным масштабом")]
    public bool focusMode = false;   // чекбокс в инспекторе

    [Tooltip("Поворачивать ли камеру к центру группы (актуально для 3D)")]
    public bool lookAtCenter = false;

    // Сохранение общего плана
    private Vector3 startPos;
    private float startFOV;
    private float startOrtho;

    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        startPos   = transform.position;
        startFOV   = cam.fieldOfView;
        startOrtho = cam.orthographicSize;

        // Подстраховка на случай некорректных значений
        if (minOrthoSize <= 0f) minOrthoSize = 0.01f;
        if (minDistance  <= 0f) minDistance  = 0.01f;
        if (padding      <  1f) padding      = 1f;
        if (focusScale   <  1f) focusScale   = 1f;
    }

    void LateUpdate()
    {
        // 1) Авто-выключение фокуса, если не осталось активных целей
        if (focusMode && GetActiveTargetsCount() == 0)
            focusMode = false;

        // 2) Возврат к общему плану
        if (!focusMode || GetActiveTargetsCount() == 0)
        {
            if (cam.orthographic)
            {
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, startOrtho, Time.deltaTime * zoomLerp);
                transform.position   = Vector3.Lerp(transform.position,   startPos,   Time.deltaTime * moveLerp);
            }
            else
            {
                cam.fieldOfView      = Mathf.Lerp(cam.fieldOfView,        startFOV,   Time.deltaTime * zoomLerp);
                transform.position   = Vector3.Lerp(transform.position,   startPos,   Time.deltaTime * moveLerp);
            }
            return;
        }

        // 3) Центр и границы только активных целей
        Bounds b = GetActiveTargetsBounds();
        Vector3 center = b.center;

        // Если бокс слишком маленький (цели близко/одна точка) — задаём минимальный «разумный» размер,
        // чтобы не стремиться к бесконечному приближению
        Vector3 ext = b.extents;
        if (ext.x < boundsEpsilon && ext.y < boundsEpsilon && ext.z < boundsEpsilon)
        {
            // Сделаем бокс маленьким, но не нулевым
            b.extents = new Vector3(boundsEpsilon, boundsEpsilon, boundsEpsilon);
            ext = b.extents;
        }

        if (cam.orthographic)
        {
            // 2D: целевой размер (чем он меньше — тем крупнее)
            float sizeX = ext.x / Mathf.Max(cam.aspect, 0.0001f);
            float sizeY = ext.y;
            float targetSize = Mathf.Max(sizeX, sizeY) * padding;

            // Увеличиваем «крупность» на focusScale
            targetSize = targetSize / focusScale;

            // Клампы — чтобы не ехать бесконечно
            targetSize = Mathf.Max(targetSize, minOrthoSize);
            if (maxOrthoSize > 0f) targetSize = Mathf.Min(targetSize, maxOrthoSize);

            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, Time.deltaTime * zoomLerp);

            // Держим текущий Z
            Vector3 targetPos = new Vector3(center.x, center.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * moveLerp);
        }
        else
        {
            // 3D: считаем необходимую дистанцию при текущем FOV
            float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
            float tan    = Mathf.Tan(Mathf.Max(fovRad * 0.5f, 0.001f));

            float reqDistY = ext.y / tan;
            float reqDistX = (ext.x / Mathf.Max(cam.aspect, 0.0001f)) / tan;
            float reqDist  = Mathf.Max(reqDistX, reqDistY) * padding;

            // Доп. крупность
            reqDist = reqDist / focusScale;

            // Клампы
            reqDist = Mathf.Max(reqDist, minDistance);
            if (maxDistance > 0f) reqDist = Mathf.Min(reqDist, maxDistance);

            // Позиция вдоль forward
            Vector3 targetPos = center - transform.forward * reqDist;
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * moveLerp);

            if (lookAtCenter)
            {
                var look = Quaternion.LookRotation(center - transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * moveLerp);
            }
        }
    }

    // === Вспомогательные методы ===
    int GetActiveTargetsCount()
    {
        int count = 0;
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            var t = targets[i];
            if (t == null) { targets.RemoveAt(i); continue; }
            if (t.gameObject.activeInHierarchy) count++;
        }
        return count;
    }

    Bounds GetActiveTargetsBounds()
    {
        bool first = true;
        Bounds b = new Bounds(Vector3.zero, Vector3.zero);

        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;

            if (first) { b = new Bounds(t.position, Vector3.zero); first = false; }
            else       { b.Encapsulate(t.position); }
        }

        if (first) // страховка
            b = new Bounds(transform.position, Vector3.one * boundsEpsilon * 2f);

        return b;
    }

    // === Публичные триггеры для кнопок / Timeline Signals ===
    public void FocusOnTargets()  { focusMode = true;  }
    public void GoWide()          { focusMode = false; }
}
