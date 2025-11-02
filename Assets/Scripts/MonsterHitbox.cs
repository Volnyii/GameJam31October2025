using UnityEngine;

/// <summary>
/// Компонент для обработки столкновений монстра с крюком
/// Содержит BoxCollider2D для определения размеров и обработки столкновений
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class MonsterHitbox : MonoBehaviour
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
    private MonsterController monsterController;
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
        rb.isKinematic = true; // Kinematic чтобы не влиять на движение монстра
        rb.gravityScale = 0f; // Отключаем гравитацию
        
        // Получаем или создаем BoxCollider2D
        boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider2D>();
        }
        
        // Настраиваем как триггер
        boxCollider.isTrigger = true;
        
        // Получаем MonsterController
        monsterController = GetComponent<MonsterController>();
        if (monsterController == null)
        {
            monsterController = GetComponentInParent<MonsterController>();
        }
        
        // Автоматически устанавливаем размер коллайдера по спрайту
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
    /// Настраивает размер коллайдера по спрайту монстра (уменьшенный размер)
    /// </summary>
    void SetupColliderSize()
    {
        if (boxCollider == null) return;
        
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInParent<SpriteRenderer>();
        }
        
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            // Используем уменьшенный размер спрайта (70% от исходного)
            Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
            boxCollider.size = spriteSize * 0.7f;
        }
        else
        {
            // Размер по умолчанию (уменьшенный)
            boxCollider.size = new Vector2(0.7f, 0.7f);
        }
    }
    
    /// <summary>
    /// Вызывается при столкновении с другим триггером (крюком)
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        // Проверяем что это крюк
        HookController hookController = other.GetComponent<HookController>();
        if (hookController != null || 
            other.CompareTag("Hook") ||
            other.name.Contains("Hook"))
        {
            // Проверяем что крюк еще не убил другого монстра
            if (hookController != null && hookController.HasHitMonster())
            {
                return; // Крюк уже убил монстра
            }
            
            // Вызываем метод смерти у монстра
            if (monsterController != null && !monsterController.IsDead)
            {
                Debug.Log($"Крюк попал в монстра {gameObject.name} через коллайдер");
                
                // Воспроизводим звук попадания
                PlayHitSound();
                
                monsterController.Die();
                
                // Отмечаем что крюк попал в монстра
                if (hookController != null)
                {
                    hookController.MarkMonsterHit();
                }
            }
        }
    }
    
    /// <summary>
    /// Обновляет размер коллайдера (можно вызвать вручную если спрайт изменился)
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
            Debug.LogWarning($"MonsterHitbox: hitSound не назначен для {gameObject.name}");
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
            Debug.Log($"MonsterHitbox: Звук воспроизведен через AudioSource на {gameObject.name}");
        }
        else
        {
            // Если нет AudioSource, используем PlayClipAtPoint
            // Используем позицию камеры для лучшей слышимости
            Vector3 soundPos = transform.position;
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                // Проверяем наличие AudioListener на камере
                AudioListener listener = mainCam.GetComponent<AudioListener>();
                if (listener == null)
                {
                    Debug.LogWarning("MonsterHitbox: На камере нет AudioListener! Звук может быть не слышен.");
                }
                
                // Если монстр далеко от камеры, воспроизводим звук ближе к камере
                float distance = Vector3.Distance(transform.position, mainCam.transform.position);
                if (distance > 20f)
                {
                    // Воспроизводим звук на позиции камеры для лучшей слышимости
                    soundPos = mainCam.transform.position;
                }
            }
            
            AudioSource.PlayClipAtPoint(hitSound, soundPos, hitSoundVolume);
            Debug.Log($"MonsterHitbox: Звук воспроизведен через PlayClipAtPoint на позиции {soundPos}");
        }
    }
}

