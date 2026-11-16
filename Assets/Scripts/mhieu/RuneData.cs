using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum StatType
{
    MaxHealth,        // Flat: +máu
    MoveSpeed,        // Percent
    JumpForce,        // Percent
    ExtraJumps,       // Flat: +1 = thêm 1 lần nhảy trên không
    DashCooldown,     // Percent âm = hồi nhanh hơn
    DashSpeed,        // Percent
    IFrameDuration,   // Percent: thời gian miễn thương khi lướt
    MeleeDamage,      // Percent hoặc Flat
    SlashDamage,      // Percent hoặc Flat
    SlashEnergyCost,  // Percent âm = rẻ hơn
    EnergyOnHit,      // Flat: năng lượng hồi mỗi đòn trúng
    DamageReduction,  // Flat: 0.1 = giảm 10% sát thương nhận
    LifestealOnHit,   // Flat: máu hồi mỗi đòn trúng
    KnockbackPower    // Percent
}

public enum ModifierMode { Flat, Percent } // Percent: 0.25 = +25%

[Serializable]
public struct StatModifier
{
    public StatType stat;
    public ModifierMode mode;
    public float value;
}

public enum RuneRarity { Common, Rare, Legendary, Cursed }

[CreateAssetMenu(menuName = "Star Fall/Rune", fileName = "NewRune")]
public class RuneData : ScriptableObject
{
    [Tooltip("ID duy nhất, dùng để lưu game (vd: fehu)")]
    public string runeId;
    public string displayName;
    [Tooltip("Ký tự rune Unicode, hiển thị tạm khi chưa có icon")]
    public string glyph;
    [Tooltip("Ý nghĩa gốc của rune, vd: Của cải")]
    public string meaning;
    public RuneRarity rarity;
    [Range(1, 3)] public int cost = 1;
    public Sprite icon;

    [TextArea] public string lore;   // mảnh cốt truyện, hiện trong nhật ký
    public List<StatModifier> modifiers = new List<StatModifier>();

    /// <summary>Tự sinh mô tả hiệu ứng cho UI, ví dụ "+25% Sát thương cận chiến".</summary>
    public string BuildEffectSummary()
    {
        var sb = new StringBuilder();
        foreach (StatModifier m in modifiers)
        {
            bool asPercent = m.mode == ModifierMode.Percent || m.stat == StatType.DamageReduction;
            float shown = asPercent ? Mathf.Abs(m.value) * 100f : Mathf.Abs(m.value);
            string sign = m.value >= 0f ? "+" : "-";
            string unit = asPercent ? "%" : "";
            sb.AppendLine($"{sign}{shown:0.#}{unit} {StatLabel(m.stat)}");
        }
        return sb.ToString().TrimEnd();
    }

    public static string StatLabel(StatType s)
    {
        switch (s)
        {
            case StatType.MaxHealth: return "Máu tối đa";
            case StatType.MoveSpeed: return "Tốc độ chạy";
            case StatType.JumpForce: return "Lực nhảy";
            case StatType.ExtraJumps: return "Lần nhảy trên không";
            case StatType.DashCooldown: return "Thời gian hồi lướt";
            case StatType.DashSpeed: return "Tốc độ lướt";
            case StatType.IFrameDuration: return "Thời gian miễn thương khi lướt";
            case StatType.MeleeDamage: return "Sát thương cận chiến";
            case StatType.SlashDamage: return "Sát thương kiếm khí";
            case StatType.SlashEnergyCost: return "Tiêu hao năng lượng kiếm khí";
            case StatType.EnergyOnHit: return "Năng lượng hồi mỗi đòn trúng";
            case StatType.DamageReduction: return "Giảm sát thương nhận";
            case StatType.LifestealOnHit: return "Máu hồi mỗi đòn trúng";
            case StatType.KnockbackPower: return "Lực đẩy lùi";
            default: return s.ToString();
        }
    }
}
