using UnityEngine;
using UnityEngine.UI;

public class ManaBar : MonoBehaviour
{
    [Header("Barra de magia")]
    [Tooltip("Imagen de relleno de la barra de magia (fill).")]
    public Image fillManaBar;

    private Character targetCharacter;

    void Start()
    {
        // Buscar al jugador por tag "Player", igual que en StaminaBar
        GameObject targetObj = GameObject.FindGameObjectWithTag("Player");

        if (targetObj != null)
        {
            targetCharacter = targetObj.GetComponent<Character>();
        }

        if (targetCharacter == null)
        {
            Debug.LogError("⚠️ No se encontró un componente Character con el tag 'Player' para la ManaBar.");
        }
    }

    void Update()
    {
        if (targetCharacter != null && targetCharacter.maximumMagic > 0)
        {
            // Actualiza el fill de la barra en base a la magia actual
            fillManaBar.fillAmount =
                (float)targetCharacter.magicNow / targetCharacter.maximumMagic;
        }
    }
}
