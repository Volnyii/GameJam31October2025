using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class VideoController : MonoBehaviour
{
    [Header("Папки/имена")]
    [SerializeField] private string folderName = "";             // подпапка в StreamingAssets/Videos
    [SerializeField] private string newVideoName = "";           // имя файла без расширения

    [Header("Поведение")]
    [SerializeField] private bool isOnceAnim = false;            // следующее видео проиграть один раз
    [SerializeField] private bool muteForWebGLAutoplay = true;   // для WebGL — мьютим звук, чтобы автоплей сработал

    [Header("Форматы")]
    [Tooltip("Расширение для ДЕСКТОП/мобилок (Editor/Standalone/Android/iOS). " +
             "В WebGL будет автоматически использован .webm")]
    [SerializeField] private string defaultDesktopExtension = ".mp4"; // .mp4 или .mov

    private VideoPlayer videoPlayer;
    private string currentVideoName;
    private string previousVideoName;
    private bool isPlayingOneShot;

    private void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        if (!videoPlayer)
        {
            Debug.LogError("VideoController: отсутствует компонент VideoPlayer.");
            enabled = false;
            return;
        }

#if UNITY_WEBGL
        // WebGL: только URL-источник
        videoPlayer.source = VideoSource.Url;

        if (muteForWebGLAutoplay)
        {
            // Чтобы автоплей не блокировался браузером
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        }
#endif
    }

    private void Start()
    {
        currentVideoName = newVideoName; // дефолт со старта
        PlayVideo(playOnce: false);
    }

    public void ChangeVideo(string updateVideoName, bool isOneTimeAnim)
    {
        newVideoName = updateVideoName;
        isOnceAnim = isOneTimeAnim; // флаг применится при ближайшем апдейте
    }

    private void Update()
    {
        if (string.IsNullOrEmpty(newVideoName)) return;

        if (newVideoName != currentVideoName)
        {
            string prev = currentVideoName;
            bool playOnce = isOnceAnim;

            currentVideoName = newVideoName;

            if (playOnce)
            {
                if (!isPlayingOneShot)
                    previousVideoName = prev; // запомним базовый ролик для возврата
            }

            PlayVideo(playOnce);
        }
    }

    private void OnEnable()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached += OnVideoLoopPointReached;
    }

    private void OnDisable()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoLoopPointReached;
    }

    private void OnVideoLoopPointReached(VideoPlayer vp)
    {
        // Срабатывает в конце НЕзацикленного клипа
        if (!isPlayingOneShot) return;

        isPlayingOneShot = false;
        isOnceAnim = false;

        if (!string.IsNullOrEmpty(previousVideoName) && previousVideoName != currentVideoName)
        {
            currentVideoName = previousVideoName;
            newVideoName = previousVideoName; // синхронизируем поля
            PlayVideo(playOnce: false);
        }
    }

    private void PlayVideo(bool playOnce)
    {
        if (!videoPlayer) return;

        string url = BuildVideoUrl(currentVideoName);
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogError($"VideoController: не удалось сформировать URL для видео '{currentVideoName}'.");
            return;
        }

        videoPlayer.isLooping = !playOnce;
        isPlayingOneShot = playOnce;

        // Безопасный запуск через Prepare — особенно важен для WebGL
        StopAllCoroutines();
        StartCoroutine(PrepareAndPlay(url));
    }

    private IEnumerator PrepareAndPlay(string url)
    {
        videoPlayer.url = url;
        videoPlayer.Prepare();

        // ждём подготовки
        while (!videoPlayer.isPrepared)
            yield return null;

        // на всякий — подождём 1 кадр, чтобы текстуры связались
        yield return null;

        videoPlayer.Play();
    }

    /// <summary>
    /// Формирует абсолютный/относительный URL с учётом платформы и StreamingAssets.
    /// Структура: Assets/StreamingAssets/Videos/{folderName}/{name}.{ext}
    /// </summary>
    private string BuildVideoUrl(string nameNoExt)
    {
        if (string.IsNullOrEmpty(nameNoExt))
            return null;

        string fileNameWithExt;

#if UNITY_WEBGL
        // В WebGL используем .webm (для прозрачности или просто для совместимости)
        fileNameWithExt = nameNoExt + ".webm";
#else
        // Для нативных билдов — что указали в инспекторе (mp4/mov)
        string ext = defaultDesktopExtension;
        if (string.IsNullOrEmpty(ext) || (!ext.StartsWith(".")))
            ext = ".mp4";
        fileNameWithExt = nameNoExt + ext;
#endif

        // Базовая папка StreamingAssets
        // В WebGL Application.streamingAssetsPath уже возвращает http(s) URL к билду
        string basePath = System.IO.Path.Combine(Application.streamingAssetsPath, "Videos");

        if (!string.IsNullOrEmpty(folderName))
            basePath = System.IO.Path.Combine(basePath, folderName);

        // Важно: для URL используем прямые слэши
        string full = System.IO.Path.Combine(basePath, fileNameWithExt).Replace("\\", "/");

        return full;
    }

    // (Опционально) открытый метод для ручного перезапуска — можно повесить на кнопку по пользовательскому клику
    public void Replay()
    {
        if (string.IsNullOrEmpty(currentVideoName)) return;
        PlayVideo(isPlayingOneShot);
    }
}
