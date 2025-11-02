using UnityEngine;

public class TutorialHandHandler : MonoBehaviour
{
    [Tooltip("Объект, который нужно включить на 5 секунд")]
    public GameObject targetObject;

    [Tooltip("Время, через которое объект выключится (в секундах)")]
    public float activeTime = 5f;

    // Метод можно вызвать из другого скрипта, кнопки или события Timeline
    public void Activate()
    {
        if (targetObject == null)
        {
            Debug.LogWarning("Не назначен targetObject в ActivateFor5Seconds.");
            return;
        }

        targetObject.SetActive(true);
        CancelInvoke(nameof(Deactivate)); // на случай повторных вызовов
        Invoke(nameof(Deactivate), activeTime);
    }

    private void Deactivate()
    {
        if (targetObject != null)
            targetObject.SetActive(false);
    }
}