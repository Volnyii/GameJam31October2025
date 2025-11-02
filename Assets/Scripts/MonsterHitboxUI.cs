using UnityEngine;

/// <summary>
/// Компонент для обработки столкновений монстра-UI с крюком
/// Содержит BoxCollider2D для определения размеров и обработки столкновений
/// Работает с UI элементами на Canvas
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class MonsterHitboxUI : MonoBehaviour
{
    [Header("Audio")]
    [Tooltip("Звук при попадании крюка в монстра")]
    public AudioClip hitSound;
    [Tooltip("Громкость звука попадания (0-1)")]
    [Range(0f, 1f)]
    public float hitSoundVolume = 1f;
    [Tooltip("Автоматически создавать AudioSource если его нет")]
    public bool autoCreateAudioSource = true;
    
    private BoxCollider2D boxCollider;
    private MonsterUI monsterUI;
    private RectTransform rectTransform;
    private AudioSource audioSource;
    
    void Awake()
    {
        SetupComponents();
    }
    
    void SetupComponents()
    {
        // Добавляем Rigidbody2D для работы коллайдера (kinematic, чтобы не влиять на физику)
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.isKinematic = true; // Kinematic для UI элементов
        rb.gravityScale = 0f; // Отключаем гравитацию
        
        // Получаем или создаем BoxCollider2D
        boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider2D>();
        }
        
        // Настраиваем как триггер
        boxCollider.isTrigger = true;
        
        // Получаем MonsterUI
        monsterUI = GetComponent<MonsterUI>();
        if (monsterUI == null)
        {
            monsterUI = GetComponentInParent<MonsterUI>();
        }
        
        // Получаем RectTransform
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = GetComponentInParent<RectTransform>();
        }
        
        // Автоматически устанавливаем размер коллайдера
        SetupColliderSize();
        
        // Настраиваем AudioSource если нужно
        SetupAudioSource();
    }
    
    /// <summary>
    /// Настраивает AudioSource для воспроизведения звуков
    /// </summary>
    void SetupAudioSource()
    {
        if (autoCreateAudioSource)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = GetComponentInParent<AudioSource>();
            }
            
            if (audioSource == null)
            {
                // Создаем AudioSource на этом объекте
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D звук
                audioSource.volume = 1f;
            }
        }
    }
    
    /// <summary>
    /// Настраивает размер коллайдера по размеру UI элемента (уменьшенный размер)
    /// </summary>
    void SetupColliderSize()
    {
        if (boxCollider == null) return;
        
        if (rectTransform != null)
        {
            // Используем sizeDelta для размера коллайдера (в мировых единицах)
            // Конвертируем пиксели в мировые единицы и уменьшаем (примерно 100 пикселей = 1 единица)
            Vector2 sizeInWorldUnits = rectTransform.sizeDelta / 100f;
            boxCollider.size = sizeInWorldUnits * 0.7f; // Уменьшаем до 70% от исходного размера
        }
        else
        {
            // Размер по умолчанию (уменьшенный)
            boxCollider.size = new Vector2(0.448f, 0.448f); // 70% от 0.64
        }
    }
    
    /// <summary>
    /// Вызывается при столкновении с другим триггером (крюком)
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        // Проверяем что это крюк
        HookUI hookUI = other.GetComponent<HookUI>();
        if (hookUI != null || 
            other.CompareTag("Hook") ||
            other.name.Contains("Hook"))
        {
            // Проверяем что крюк еще не убил другого монстра
            if (hookUI != null && hookUI.HasHitMonster())
            {
                return; // Крюк уже убил монстра
            }
            
            // Вызываем метод смерти у монстра
            if (monsterUI != null && !monsterUI.IsDead)
            {
                Debug.Log($"Крюк попал в монстра {gameObject.name} через коллайдер (UI)");
                
                // Воспроизводим звук попадания
                PlayHitSound();
                
                monsterUI.Die();
                
                // Отмечаем что крюк попал в монстра
                if (hookUI != null)
                {
                    hookUI.MarkMonsterHit();
                }
            }
        }
    }
    
    /// <summary>
    /// Обновляет размер коллайдера (можно вызвать вручную если размер изменился)
    /// </summary>
    public void UpdateColliderSize()
    {
        SetupColliderSize();
    }
    
    /// <summary>
    /// Получает размеры коллайдера
    /// </summary>
    public Vector2 GetSize()
    {
        if (boxCollider != null)
        {
            return boxCollider.size;
        }
        return Vector2.zero;
    }
    
    /// <summary>
    /// Получает границы коллайдера в мировых координатах
    /// </summary>
    public Bounds GetBounds()
    {
        if (boxCollider != null)
        {
            return boxCollider.bounds;
        }
        return new Bounds(transform.position, Vector3.zero);
    }
    
    /// <summary>
    /// Воспроизводит звук попадания в монстра
    /// </summary>
    void PlayHitSound()
    {
        if (hitSound == null)
        {
            Debug.LogWarning($"MonsterHitboxUI: hitSound не назначен для {gameObject.name}");
            return;
        }
        
        // Используем сохраненный AudioSource или ищем его
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = GetComponentInParent<AudioSource>();
            }
        }
        
        if (audioSource != null)
        {
            // Используем AudioSource (более надежный способ)
            audioSource.PlayOneShot(hitSound, hitSoundVolume);
            Debug.Log($"MonsterHitboxUI: Звук воспроизведен через AudioSource на {gameObject.name}");
        }
        else
        {
            // Если нет AudioSource, используем PlayClipAtPoint
            // Для UI элементов лучше воспроизводить звук на позиции камеры для лучшей слышимости
            Camera mainCam = Camera.main;
            Vector3 soundPos = mainCam != null ? mainCam.transform.position : Vector3.zero;
            
            // Проверяем наличие AudioListener на камере
            if (mainCam != null)
            {
                AudioListener listener = mainCam.GetComponent<AudioListener>();
                if (listener == null)
                {
                    Debug.LogWarning("MonsterHitboxUI: На камере нет AudioListener! Звук может быть не слышен.");
                }
            }
            
            AudioSource.PlayClipAtPoint(hitSound, soundPos, hitSoundVolume);
            Debug.Log($"MonsterHitboxUI: Звук воспроизведен через PlayClipAtPoint на позиции камеры {soundPos}");
        }
    }
}

