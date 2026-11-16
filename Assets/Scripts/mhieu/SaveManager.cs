using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class GameSaveData
{
    public string sceneName;
    public float posX, posY;
    public RuneSaveData runes = new RuneSaveData();
    public List<string> flags = new List<string>(); // boss đã hạ, câu đố đã giải, ghi chép đã nhặt...
}

/// <summary>
/// Lưu tiến trình tại checkpoint (vị trí, rune, cờ thế giới) vào file JSON.
/// Chết -> nạp lại scene, quái hồi sinh, mọi thứ chưa lưu bị hoàn tác.
/// </summary>
public class SaveManager : MonoBehaviour
{
    private static SaveManager instance;
    public static SaveManager Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("SaveManager");
                instance = go.AddComponent<SaveManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    public GameSaveData Data { get; private set; } = new GameSaveData();
    /// <summary>True khi scene vừa nạp xong cần áp dụng dữ liệu lưu lên player.</summary>
    public bool PendingApply { get; private set; }
    public bool HasSave => File.Exists(FilePath);

    private readonly HashSet<string> flags = new HashSet<string>();
    private string FilePath => Path.Combine(Application.persistentDataPath, "savegame.json");

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        LoadFromDisk();
    }

    // ---------- CỜ THẾ GIỚI ----------
    // Ví dụ: SaveManager.Instance.SetFlag("boss_area1"); if (HasFlag("puzzle_gate_2")) mở cổng ngay.
    public void SetFlag(string id) => flags.Add(id);
    public bool HasFlag(string id) => flags.Contains(id);

    // ---------- LƯU ----------
    public void Save(Vector2 respawnPosition, RuneInventory runes)
    {
        Data.sceneName = SceneManager.GetActiveScene().name;
        Data.posX = respawnPosition.x;
        Data.posY = respawnPosition.y;
        if (runes != null) Data.runes = runes.GetSaveData();
        Data.flags = new List<string>(flags);

        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true));
        }
        catch (Exception e)
        {
            Debug.LogError("[Save] Không ghi được file: " + e.Message);
        }
    }

    // ---------- TẢI ----------
    private void LoadFromDisk()
    {
        if (!HasSave) return;
        try
        {
            Data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(FilePath)) ?? new GameSaveData();
            flags.Clear();
            foreach (string f in Data.flags) flags.Add(f);
        }
        catch (Exception e)
        {
            Debug.LogError("[Save] File lưu hỏng, bỏ qua: " + e.Message);
            Data = new GameSaveData();
        }
    }

    /// <summary>Dùng cho nút "Tiếp tục" ở menu chính và khi nhân vật chết.</summary>
    public void RequestLoad()
    {
        Time.timeScale = 1f;

        if (string.IsNullOrEmpty(Data.sceneName))
        {
            // Chưa từng chạm checkpoint: chơi lại scene hiện tại từ đầu
            flags.Clear();
            PendingApply = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        // Hoàn tác mọi cờ đặt sau lần lưu cuối
        flags.Clear();
        foreach (string f in Data.flags) flags.Add(f);

        PendingApply = true;
        SceneManager.LoadScene(Data.sceneName);
    }

    /// <summary>PlayerRespawn gọi sau khi scene nạp xong.</summary>
    public void ApplyTo(GameObject player)
    {
        PendingApply = false;

        player.transform.position = new Vector3(Data.posX, Data.posY, player.transform.position.z);

        if (player.TryGetComponent(out RuneInventory runes)) runes.LoadSaveData(Data.runes);
        if (player.TryGetComponent(out Health health)) health.ResetHealth(); // hồi sinh đầy máu
    }

    public void DeleteSave()
    {
        if (HasSave) File.Delete(FilePath);
        Data = new GameSaveData();
        flags.Clear();
        PendingApply = false;
    }
}
