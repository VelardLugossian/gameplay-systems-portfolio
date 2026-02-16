using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controla el panel de carga de partidas y refresca los slots.
/// </summary>
public class LoadMenu : MonoBehaviour
{
    [SerializeField] private SaveSlotUI[] slots; // Asigna los prefabs/instancias desde el inspector
    [SerializeField] private GameObject panel;   // Panel raíz del menú "Load"

    private void Awake()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].Init(this, i);
        }
    }

    public void Open()
    {
        panel.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        panel.SetActive(false);
    }

    private void Refresh()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (SaveSystem.HasData(i))
            {
                var data = SaveSystem.Peek(i);
                slots[i].SetData(data); // <- data.characterName y data.characterId ya disponibles
            }
            else
            {
                slots[i].SetEmpty();
            }
        }
    }

    // ---- Callbacks de los botones ----
    public void Load(int slot)
    {
        Debug.Log($"🟡 Botón LOAD presionado. Slot seleccionado: {slot}");

        if (!SaveSystem.HasData(slot))
        {
            Debug.LogWarning($"⚠️ No hay datos en el slot {slot}");
            return;
        }

        var data = SaveSystem.Peek(slot);
        if (data == null)
        {
            Debug.LogError("❌ Error: datos de guardado nulos. No se cargará la escena.");
            return;
        }

        Debug.Log($"✅ Cargando escena index {data.levelIndex} desde el slot {slot} | Char: {data.characterName} ({data.characterId})");
        PlayerPrefs.SetInt("load_slot", slot);
        PlayerPrefs.Save();

        SceneManager.LoadScene(data.levelIndex);
    }

    public void Delete(int slot)
    {
        SaveSystem.Delete(slot);
        Refresh();
    }
}
