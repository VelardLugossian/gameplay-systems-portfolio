using UnityEngine;

/// <summary>
/// Clase base para cualquier hechizo (jugador o NPC).
/// Define duración, coste y metadatos de quién puede usarlo.
/// </summary>
/// 
public enum SpellClassType
{
    Any = 0,
    Elf = 1,
    Dwarf = 2,
    Barbarian = 3,
    Mage = 4
}

public enum SpellOwnerType
{
    Player = 0,
    NPC = 1,
    Both = 2
}
public abstract class Spells : MonoBehaviour
{
    [Header("Config común de hechizo")]
    [Tooltip("Duración del efecto del hechizo en segundos.")]
    public float duration = 5f;

    [Tooltip("Coste en puntos de magia para lanzar este hechizo.")]
    public int magicCost = 10;

    [Tooltip("Clase o tipo para la que está pensado este hechizo.")]
    public SpellClassType spellClass = SpellClassType.Any;

    [Tooltip("Si está pensado para jugador, NPC, o ambos.")]
    public SpellOwnerType spellOwner = SpellOwnerType.Player;

    /// <summary>
    /// Referencia al lanzador del hechizo (Character base).
    /// </summary>
    protected Character caster;

    /// <summary>
    /// Inicializa la referencia al Character.
    /// </summary>
    protected virtual void Awake()
    {
        caster = GetComponent<Character>();
    }

    
    public virtual bool CanCast()
    {
        if (caster == null)
            return false;

        if (caster.magicNow < magicCost)
            return false;

        return true;
    }

    
    public virtual bool TryCast()
    {
        if (!CanCast())
            return false;

        // Pagar coste de magia
        caster.magicNow -= magicCost;

        // Lógica específica del hechizo
        OnCast();
        return true;
    }

    /// <summary>
    /// Implementar en las clases hijas: qué hace el hechizo al lanzarse.
    /// </summary>
    protected abstract void OnCast();

    /// <summary>
    /// Método opcional para forzar la cancelación del hechizo
    /// (por ejemplo al morir, cambiar de escena, etc).
    /// </summary>
    public virtual void ForceStop()
    {
        // Por defecto no hace nada; las hijas lo ampliarán si lo necesitan.
    }
}
