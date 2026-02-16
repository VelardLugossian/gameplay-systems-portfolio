using System.Collections;
using UnityEngine;

/// <summary>
/// Hechizo de infravisión para el jugador.
/// Hereda de PlayerSpells y aprovecha duración y coste de magia de Spells.
/// </summary>
public class InfravisionSpell : PlayerSpells
{
    [Header("Infravisión")]
    [Tooltip("Objeto con luces/postpro que simula la infravisión (hijo de la cámara del jugador).")]
    public GameObject infravisionVFX;

    [Header("VFX de casteo")]
    [Tooltip("Prefab del efecto visual que se reproducirá al lanzar el hechizo (por ejemplo vfxgraph_HealingSpell).")]
    public GameObject castVFXPrefab;

    [Tooltip("Punto donde aparecerá el VFX. Si se deja vacío, se usará la posición del propio personaje.")]
    public Transform castVFXSpawnPoint;

    [Tooltip("Tiempo en segundos antes de destruir automáticamente el VFX instanciado.")]
    public float castVFXLifetime = 3f;

    private bool isActive = false;
    private Coroutine activeRoutine;

    protected override void Awake()
    {
        base.Awake();

        if (infravisionVFX != null)
        {
            infravisionVFX.SetActive(false);
        }
    }

    /// <summary>
    /// Evita lanzar el hechizo si ya está activo, además de las comprobaciones base.
    /// </summary>
    public override bool CanCast()
    {
        if (isActive)
            return false;

        return base.CanCast();
    }

    /// <summary>
    /// Lógica concreta de este hechizo al lanzarse.
    /// El coste de magia ya se ha descontado en TryCast().
    /// </summary>
    protected override void OnCast()
    {
        // 1) Aquí llamamos a la función que gobierna la animación del player
        player.PlayCastAnimation();

        // 2) Reproducir VFX de casteo
        PlayCastVFX();

        // 3) Activar lógica de infravisión
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(InfravisionRoutine());
    }

    private IEnumerator InfravisionRoutine()
    {
        isActive = true;

        if (infravisionVFX != null)
            infravisionVFX.SetActive(true);

        // Usamos la duración definida en la clase padre Spells
        yield return new WaitForSeconds(duration);

        if (infravisionVFX != null)
            infravisionVFX.SetActive(false);

        isActive = false;
        activeRoutine = null;
    }

    /// <summary>
    /// Instancia el prefab de VFX en la posición indicada y lo destruye tras castVFXLifetime.
    /// Funciona tanto con ParticleSystem como con VFX Graph.
    /// </summary>
    private void PlayCastVFX()
    {
        if (castVFXPrefab == null)
            return;

        Transform spawn = castVFXSpawnPoint != null ? castVFXSpawnPoint : transform;

        GameObject vfxInstance = Instantiate(
            castVFXPrefab,
            spawn.position,
            spawn.rotation
        );

        if (castVFXLifetime > 0f)
        {
            Destroy(vfxInstance, castVFXLifetime);
        }
    }

    public override void ForceStop()
    {
        base.ForceStop();

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        if (infravisionVFX != null)
            infravisionVFX.SetActive(false);

        isActive = false;
    }
}
