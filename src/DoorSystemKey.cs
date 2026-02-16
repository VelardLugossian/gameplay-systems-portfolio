using UnityEngine;

public class DoorSystemKey : MonoBehaviour, IInteractable
{
    [Header("Rotación de puerta (local)")]
    public Transform doorTransform;
    public Vector3 closedRotation;
    public Vector3 openRotation;
    public float openSpeed = 2f;

    [Header("Audio")]
    public AudioClip openDoor;
    public AudioClip closeDoor;
    public AudioClip noKeySound;
    private AudioSource audioSource;

    [Header("Llaves")]
    public bool hasKey = false;   // Se mantiene por compatibilidad con otros sistemas
    private bool wasUnlocked = false;

    [Header("UI de interacción")]
    public GameObject interactionPrompt; // Texto en el Canvas general

    // Estado interno (igual que DoorSystem)
    private bool isOpen = false;
    private Quaternion targetRotation;

    private void Start()
    {
        // Rotación local inicial: puerta cerrada
        targetRotation = Quaternion.Euler(closedRotation);
        if (doorTransform != null)
            doorTransform.localRotation = targetRotation;

        audioSource = GetComponent<AudioSource>();

        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);
    }

    private void Update()
    {
        // Animación suave con rotación local (igual que DoorSystem)
        if (doorTransform != null)
            doorTransform.localRotation = Quaternion.Lerp(
                doorTransform.localRotation,
                targetRotation,
                Time.deltaTime * openSpeed
            );
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerCharacter player = other.GetComponentInParent<PlayerCharacter>();
            if (player != null)
            {
                player.SetInteractable(this);
                if (interactionPrompt != null)
                    interactionPrompt.SetActive(true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (interactionPrompt != null)
                interactionPrompt.SetActive(false);

            PlayerCharacter player = other.GetComponent<PlayerCharacter>();
            if (player != null)
                player.ClearInteractable(this);
        }
    }

    // Implementación de IInteractable, igual que DoorSystem
    public void Interact(PlayerCharacter player)
    {
        if (player == null)
        {
            Debug.LogWarning("⚠️ Interact llamado sin PlayerCharacter.");
            return;
        }

        // Si nunca se ha desbloqueado, intentamos gastar una llave del jugador
        if (!wasUnlocked)
        {
            // Se respeta la lógica original: aunque 'hasKey' sea true,
            // el primer desbloqueo exige SpendKey(); si falla, no se abre.
            bool success = player.SpendKey();
            if (!success)
            {
                if (audioSource != null && noKeySound != null)
                    audioSource.PlayOneShot(noKeySound);

                Debug.Log("🚪 Necesitas una llave para abrir esta puerta.");
                return;
            }

            wasUnlocked = true; // A partir de ahora, se comporta como una puerta normal
        }

        // Toggle abrir/cerrar (igual que DoorSystem)
        isOpen = !isOpen;

        if (audioSource != null)
        {
            if (isOpen && openDoor != null) audioSource.PlayOneShot(openDoor);
            else if (!isOpen && closeDoor != null) audioSource.PlayOneShot(closeDoor);
        }

        // Asignar nueva rotación local objetivo
        targetRotation = isOpen ? Quaternion.Euler(openRotation) : Quaternion.Euler(closedRotation);

        Debug.Log($"🟡 Puerta con llave {(isOpen ? "abierta" : "cerrada")} (desbloqueada={wasUnlocked})");
    }

    public GameObject GetGameObject()
    {
        return gameObject;
    }
}
