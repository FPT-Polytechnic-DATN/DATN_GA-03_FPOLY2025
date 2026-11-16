using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RuneSaveData
{
    public List<string> owned = new List<string>();
    public List<string> equipped = new List<string>();
    public int bonusCapacity;
}

/// <summary>
/// Quản lý rune người chơi đang có và đang gắn.
/// Mỗi rune tốn "cost" điểm; tổng điểm gắn không vượt Capacity.
/// Capacity tăng khi hạ boss (gọi IncreaseCapacity).
/// </summary>
public class RuneInventory : MonoBehaviour
{
    private const string ResourcesPath = "Runes"; // Assets/Resources/Runes

    [SerializeField] private PlayerStats stats;
    [SerializeField] private int baseCapacity = 4;
    [SerializeField] private int maxCapacity = 10;
    [Tooltip("Bật khi test. Thực tế chỉ cho đổi rune tại checkpoint/bàn thờ")]
    [SerializeField] private bool allowEquipAnywhere = false;

    /// <summary>Checkpoint/bàn thờ set true khi người chơi đang đứng cạnh.</summary>
    public bool AtAltar { get; set; }

    private readonly List<RuneData> owned = new List<RuneData>();
    private readonly List<RuneData> equipped = new List<RuneData>();
    private int bonusCapacity;

    public IReadOnlyList<RuneData> Owned => owned;
    public IReadOnlyList<RuneData> Equipped => equipped;
    public int Capacity => Mathf.Min(maxCapacity, baseCapacity + bonusCapacity);
    public int UsedCapacity
    {
        get { int sum = 0; foreach (RuneData r in equipped) sum += r.cost; return sum; }
    }

    public event Action Changed;                 // UI vẽ lại
    public event Action<RuneData> RuneAcquired;  // popup "Nhặt được rune X"

    private void Awake()
    {
        if (stats == null) stats = GetComponent<PlayerStats>();
    }

    public bool Has(RuneData rune) => rune != null && owned.Contains(rune);
    public bool IsEquipped(RuneData rune) => equipped.Contains(rune);

    public bool AddRune(RuneData rune)
    {
        if (rune == null || owned.Contains(rune)) return false;
        owned.Add(rune);
        RuneAcquired?.Invoke(rune);
        Changed?.Invoke();
        return true;
    }

    public void IncreaseCapacity(int amount)
    {
        bonusCapacity += amount;
        Changed?.Invoke();
    }

    public bool CanEquip(RuneData rune, out string reason)
    {
        reason = null;
        if (!Has(rune)) reason = "Chưa sở hữu rune này";
        else if (IsEquipped(rune)) reason = "Đã trang bị";
        else if (!AtAltar && !allowEquipAnywhere) reason = "Chỉ đổi rune tại bàn thờ";
        else if (UsedCapacity + rune.cost > Capacity) reason = "Không đủ điểm rune";
        return reason == null;
    }

    public bool Equip(RuneData rune)
    {
        if (!CanEquip(rune, out _)) return false;
        equipped.Add(rune);
        stats.SetModifiers(rune, rune.modifiers);
        Changed?.Invoke();
        return true;
    }

    public bool Unequip(RuneData rune)
    {
        if (!IsEquipped(rune)) return false;
        if (!AtAltar && !allowEquipAnywhere) return false;
        equipped.Remove(rune);
        stats.RemoveModifiers(rune);
        Changed?.Invoke();
        return true;
    }

    // ---------- LƯU / TẢI (nối vào hệ thống lưu game ở checkpoint) ----------
    public RuneSaveData GetSaveData()
    {
        var data = new RuneSaveData { bonusCapacity = bonusCapacity };
        foreach (RuneData r in owned) data.owned.Add(r.runeId);
        foreach (RuneData r in equipped) data.equipped.Add(r.runeId);
        return data;
    }

    public void LoadSaveData(RuneSaveData data)
    {
        foreach (RuneData r in equipped) stats.RemoveModifiers(r);
        equipped.Clear();
        owned.Clear();
        bonusCapacity = data.bonusCapacity;

        var lookup = new Dictionary<string, RuneData>();
        foreach (RuneData r in Resources.LoadAll<RuneData>(ResourcesPath)) lookup[r.runeId] = r;

        foreach (string id in data.owned)
            if (lookup.TryGetValue(id, out RuneData found)) owned.Add(found);

        foreach (string id in data.equipped)
        {
            if (lookup.TryGetValue(id, out RuneData found) && owned.Contains(found)
                && UsedCapacity + found.cost <= Capacity)
            {
                equipped.Add(found);
                stats.SetModifiers(found, found.modifiers);
            }
        }
        Changed?.Invoke();
    }
}
