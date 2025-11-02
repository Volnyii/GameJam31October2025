using UnityEngine;
using UnityEngine.Playables;

public class StartBgMusicScript : MonoBehaviour
{
    [Header("Ссылка на PlayableDirector (кат-сцену)")]
    public PlayableDirector director;

    [Header("Звуки, которые должны заиграть после кат-сцены")]
    public AudioSource[] sources;

    [Header("Скрипт TutorialHandHandler, который нужно запустить")]
    public TutorialHandHandler tutorialHandHandler;

    void Start()
    {
        if (director != null)
            director.stopped += OnStopped;
    }

    void OnStopped(PlayableDirector d)
    {
        // 🔊 Включаем все звуки
        foreach (var s in sources)
        {
            if (!s) continue;
            s.loop = true;
            s.Play();
        }

        // ✋ Активируем руку (если назначена)
        if (tutorialHandHandler != null)
        {
            tutorialHandHandler.Activate();
        }
        else
        {
            Debug.LogWarning("TutorialHandHandler не назначен в StartBgMusicScript.");
        }
    }

    void OnDestroy()
    {
        if (director != null)
            director.stopped -= OnStopped;
    }
}