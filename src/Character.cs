using UnityEngine;

public class Character : MonoBehaviour
{
    [Header("Estadísticas comunes")]
    public int maximumHealth = 100;
    public int healthNow = 100;

    public int maximumStamina = 100;
    public int staminaNow = 100;

    public int maximumMagic = 100;
    public int magicNow = 100;

    [Header("Regeneración de Stamina")]
    [Tooltip("Stamina por segundo que se regenera de forma pasiva.")]
    public float staminaRegenRate = 10f;

    [Tooltip("Tiempo en segundos tras consumir stamina antes de empezar a regenerar.")]
    public float staminaRegenDelay = 1.5f;

    private float staminaRegenTimer = 0f;
    private float staminaRegenBuffer = 0f;


    protected AudioSource audioSource;

    protected virtual void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    public virtual void TakeDamage(int amount)
    {
        healthNow -= amount;
        Debug.Log($"{gameObject.name} recibió {amount} de daño. Vida actual: {healthNow}");

        if (healthNow <= 0)
        {
            healthNow = 0;
            Die();
        }
    }
    public void SpendStamina(int amount)
    {
        staminaNow = Mathf.Max(staminaNow - amount, 0);
        staminaRegenTimer = staminaRegenDelay; // Reinicia el tiempo antes de regenerar
    }

    private void HandleStaminaRegen()
    {
        // Contador para esperar antes de regenerar
        if (staminaRegenTimer > 0f)
        {
            staminaRegenTimer -= Time.deltaTime;
            return;
        }

        // Regeneración pasiva
        if (staminaNow < maximumStamina)
        {
            // 1) Acumulamos stamina fraccionaria en el buffer
            staminaRegenBuffer += staminaRegenRate * Time.deltaTime;

            // 2) Si ya tenemos al menos 1 punto entero acumulado…
            if (staminaRegenBuffer >= 1f)
            {
                int amount = Mathf.FloorToInt(staminaRegenBuffer);

                // Sumamos esa cantidad a la stamina (entera)
                staminaNow += amount;

                // Restamos del buffer lo que hemos gastado
                staminaRegenBuffer -= amount;

                // 3) No permitir superar el máximo
                staminaNow = Mathf.Min(staminaNow, maximumStamina);
            }
        }
    }

    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name} ha muerto.");
    }

    protected virtual void Update()
    {
        HandleStaminaRegen();
    }

}
