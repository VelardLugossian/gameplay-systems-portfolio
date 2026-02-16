using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // 🔴 Esta línea es clave: define el singleton
    public static GameManager Instance;

    public PlayerCharacter player;
    public Animator playerAnimator;
    public GameObject canvasGameOver;
    public GameOverMenu gameOverMenu;
    public AudioClip elfmaleScream;

    public Vector3 playerPosition;
    public int levelIndex;
    public bool isLoadingFromSave = false;
    public bool receivedDamagePlayer;

    private bool isDead = false;
    private AudioSource audioSource;

    [SerializeField] private CharacterMenuToggle characterMenuToggle;
    public HashSet<string> worldEvents = new HashSet<string>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        CachePlayerState();
    }

    private void Start()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerCharacter>();

        if (playerAnimator == null && player != null)
            playerAnimator = player.GetComponentInChildren<Animator>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        int loadedSlot = PlayerPrefs.HasKey("load_slot") ? PlayerPrefs.GetInt("load_slot") : -1;
        if (loadedSlot >= 0 && SaveSystem.HasData(loadedSlot))
        {
            var data = SaveSystem.Load(loadedSlot);
            isLoadingFromSave = true;

            if (player != null)
                player.transform.position = new Vector3(data.px, data.py, data.pz);

            player.healthNow = data.health;

            PlayerPrefs.DeleteKey("load_slot");
        }
    }

    private void Update()
    {
        if (receivedDamagePlayer)
        {
            ApplyDamageToPlayer(10);
            receivedDamagePlayer = false;
        }

        if (player != null && player.healthNow <= 0 && !isDead)
        {
            StartCoroutine(GameOverSequence());
        }
    }

    public void ApplyDamageToPlayer(int damageAmount)
    {
        player.TakeDamage(damageAmount);

        if (playerAnimator != null)
            playerAnimator.SetTrigger("TakeDamage");

        if (audioSource != null && elfmaleScream != null)
            audioSource.PlayOneShot(elfmaleScream);
    }

    IEnumerator GameOverSequence()
    {
        isDead = true;
        Debug.Log("☠️ Iniciando secuencia de Game Over");

        if (playerAnimator != null)
        {
            playerAnimator.Rebind();
            playerAnimator.Update(0);
            playerAnimator.SetTrigger("IsDying");
            yield return null;

            AnimatorStateInfo state = playerAnimator.GetCurrentAnimatorStateInfo(0);
            float animTime = state.length > 0.1f ? state.length : 3f;
            yield return new WaitForSeconds(animTime);
        }
        else
        {
            yield return new WaitForSeconds(2f);
        }

        if (characterMenuToggle != null)
            characterMenuToggle.ForceCloseMenu();

        if (canvasGameOver != null)
            canvasGameOver.SetActive(true);

        if (gameOverMenu != null)
            gameOverMenu.enabled = true;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerCharacter>();
            if (player != null)
            {
                playerAnimator = player.GetComponentInChildren<Animator>();
                Debug.Log("✅ Referencias del jugador reasignadas automáticamente.");
            }
        }

        if (!PlayerPrefs.HasKey("load_slot"))
        {
            CachePlayerState();
            return;
        }

        int slot = PlayerPrefs.GetInt("load_slot");
        PlayerPrefs.DeleteKey("load_slot");

        var data = SaveSystem.Peek(slot);
        if (data == null)
        {
            Debug.LogWarning("❌ No se pudieron cargar los datos del slot");
            return;
        }

        if (player != null)
            player.transform.position = new Vector3(data.px, data.py, data.pz);

        playerPosition = player.transform.position;
        levelIndex = data.levelIndex;
        player.healthNow = data.health;
        player.currentKeys = data.pk;
        player.currentRPotion = data.pr;
        player.currentGPotion = data.ps;
        player.currentBPotion = data.pm;

        Debug.Log($"✅ Datos aplicados desde slot {slot} al cargar la escena {scene.buildIndex}");
    }

    public void CachePlayerState()
    {
        if (player == null) return;

        levelIndex = SceneManager.GetActiveScene().buildIndex;
        playerPosition = player.transform.position;
    }

    public bool IsPlayerDead()
    {
        return isDead;
    }
}
