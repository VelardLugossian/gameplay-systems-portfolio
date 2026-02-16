using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Envuelve PlayerPrefs para gestionar varios slots de guardado.
/// </summary>
public static class SaveSystem
{
    public const int MaxSlots = 3;

    // Claves para “pendiente de personaje” (se setean desde la pantalla de creación/carga)
    private const string PendingCharacterIdKey = "pending_character_id";
    private const string PendingCharacterNameKey = "pending_character_name";

    [System.Serializable]
    public class SaveFile
    {
        // --- NUEVO: Identidad del personaje ---
        public string characterId;      // p.ej., "elf_male", "elf_female", etc.
        public string characterName;    // p.ej., "Kaela", "Theron", etc.

        // --- Ya existentes ---
        public int levelIndex;
        public float px, py, pz, pk, pr, ps, pm;
        public int health;
        public string date;
        public List<string> events = new List<string>();
    }

    private static string Key(int slot) => $"save_{slot}";

    /// <summary>
    /// Lo usará la pantalla de selección/creación para “preparar” los metadatos del personaje.
    /// Se aplicarán en el próximo Save() de un slot que aún no tenga personaje definido.
    /// </summary>
    public static void SetPendingCharacter(string characterId, string characterName)
    {
        PlayerPrefs.SetString(PendingCharacterIdKey, characterId ?? "");
        PlayerPrefs.SetString(PendingCharacterNameKey, characterName ?? "");
        PlayerPrefs.Save();
    }

    /// <summary>
    /// (Opcional) Limpia los metadatos pendientes tras usarlos.
    /// </summary>
    public static void ClearPendingCharacter()
    {
        PlayerPrefs.DeleteKey(PendingCharacterIdKey);
        PlayerPrefs.DeleteKey(PendingCharacterNameKey);
        PlayerPrefs.Save();
    }

    public static void Save(int slot)
    {
        int currentScene = SceneManager.GetActiveScene().buildIndex;

        if (currentScene == 0)
        {
            Debug.LogWarning("Cannot save in MainMenu");
            return;
        }

        var player = GameObject.FindGameObjectWithTag("Player")?.GetComponent<PlayerCharacter>();
        if (player == null)
        {
            Debug.LogError(" No se encontró un objeto con tag 'Player' o no tiene PlayerCharacter.");
            return;
        }

        // 1) Recuperar personaje desde el slot (si ya existía) o desde “pending”
        string cid = null;
        string cname = null;

        if (HasData(slot))
        {
            var existing = Peek(slot); // puede venir de versiones previas sin campos -> null/empty
            cid = existing?.characterId;
            cname = existing?.characterName;
        }

        // Si aún no tenemos personaje, usamos el pendiente (lo habrá puesto el selector de personaje)
        if (string.IsNullOrWhiteSpace(cid))
            cid = PlayerPrefs.GetString(PendingCharacterIdKey, "");
        if (string.IsNullOrWhiteSpace(cname))
            cname = PlayerPrefs.GetString(PendingCharacterNameKey, "");

        // Defaults amistosos para compatibilidad hacia atrás
        if (string.IsNullOrWhiteSpace(cid)) cid = "unknown";
        if (string.IsNullOrWhiteSpace(cname)) cname = "Héroe";

        // 2) Construir el SaveFile actual
        var file = new SaveFile
        {
            characterId = cid,
            characterName = cname,

            levelIndex = currentScene,
            px = player.transform.position.x,
            py = player.transform.position.y,
            pz = player.transform.position.z,
            pk = player.currentKeys,
            pr = player.currentRPotion,
            ps = player.currentGPotion,
            pm = player.currentBPotion,
            health = player.healthNow,
            date = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            events = GameManager.Instance != null ? GameManager.Instance.worldEvents.ToList() : new List<string>()
        };

        PlayerPrefs.SetString(Key(slot), JsonUtility.ToJson(file));
        PlayerPrefs.Save();

        Debug.Log($"💾 Guardado en slot {slot} | Char: {file.characterName} ({file.characterId}) | Escena {currentScene} | Pos: ({file.px}, {file.py}, {file.pz})");

    }

    public static SaveFile Peek(int slot)
    {
        if (!HasData(slot))
        {
            Debug.LogWarning($"❗ No hay datos guardados en el slot {slot}");
            return null;
        }

        string json = PlayerPrefs.GetString(Key(slot));

        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError($"❗ JSON vacío o nulo en PlayerPrefs para el slot {slot}");
            return null;
        }

        try
        {
            var file = JsonUtility.FromJson<SaveFile>(json);
            if (file == null)
            {
                Debug.LogError($"❗ Fallo al deserializar JSON válido: {json}");
            }
            return file;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❗ Excepción al parsear JSON del slot {slot}: {e.Message}\nJSON: {json}");
            return null;
        }
    }

    public static bool HasData(int slot) => PlayerPrefs.HasKey(Key(slot));

    public static SaveFile Load(int slot)
    {
        var file = Peek(slot);
        if (file == null) return null;

        // Game state global
        GameManager.Instance.levelIndex = file.levelIndex;
        GameManager.Instance.playerPosition = new Vector3(file.px, file.py, file.pz);
        GameManager.Instance.worldEvents = new HashSet<string>(file.events);

        // Estado del PlayerCharacter en la escena actual (si ya existe uno)
        var player = GameObject.FindGameObjectWithTag("Player")?.GetComponent<PlayerCharacter>();
        if (player != null)
        {
            player.healthNow = file.health;
            player.currentKeys = file.pk;
            player.currentRPotion = file.pr;
            player.currentGPotion = file.ps;
            player.currentBPotion = file.pm;
        }
        else
        {
            Debug.LogWarning("No se encontró un objeto con tag 'Player' al cargar datos.");
        }

        return file;
    }

    public static void Delete(int slot)
    {
        if (HasData(slot))
        {
            PlayerPrefs.DeleteKey(Key(slot));
            PlayerPrefs.Save();
        }
    }
}
