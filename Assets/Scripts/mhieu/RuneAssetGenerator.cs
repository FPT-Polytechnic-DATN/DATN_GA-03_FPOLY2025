#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu: Star Fall > Tạo bộ Rune mặc định.
/// Tạo 14 asset trong Assets/Resources/Runes. Rune nào đã tồn tại sẽ được giữ nguyên (không ghi đè chỉnh sửa của bạn).
/// </summary>
public static class RuneAssetGenerator
{
    private const string Folder = "Assets/Resources/Runes";

    private static StatModifier F(StatType s, float v) =>
        new StatModifier { stat = s, mode = ModifierMode.Flat, value = v };
    private static StatModifier P(StatType s, float v) =>
        new StatModifier { stat = s, mode = ModifierMode.Percent, value = v };

    [MenuItem("Star Fall/Tạo bộ Rune mặc định")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "Runes");

        // ----- Thường (1 điểm) -----
        Make("fehu", "Fehu", "ᚠ", "Của cải", RuneRarity.Common, 1,
            "Ký ức về kho báu Valhalla, nơi mỗi chiến binh ngã xuống đều được ban thưởng.",
            F(StatType.EnergyOnHit, 4));
        Make("uruz", "Uruz", "ᚢ", "Sức mạnh nguyên thủy", RuneRarity.Common, 1,
            "Sức bền của thân xác phàm nhân đang dần được rèn lại.",
            F(StatType.MaxHealth, 20));
        Make("raidho", "Raidho", "ᚱ", "Hành trình", RuneRarity.Common, 1,
            "Mỗi bước chân trên nhân giới đều là một bước gần hơn tới sự thật.",
            P(StatType.MoveSpeed, 0.12f));
        Make("algiz", "Algiz", "ᛉ", "Bảo hộ", RuneRarity.Common, 1,
            "Dấu ấn hộ vệ cũ của cô, từng che chắn cho cả một đội quân.",
            F(StatType.DamageReduction, 0.10f));
        Make("kenaz", "Kenaz", "ᚲ", "Ngọn đuốc", RuneRarity.Common, 1,
            "Ngọn lửa soi đường trong bóng tối, cũng là thứ nuôi kiếm khí.",
            P(StatType.SlashEnergyCost, -0.30f));

        // ----- Hiếm (2 điểm) -----
        Make("thurisaz", "Thurisaz", "ᚦ", "Gai nhọn", RuneRarity.Rare, 2,
            "Cơn giận của người từng bị phản bội, sắc như gai.",
            P(StatType.MeleeDamage, 0.25f), P(StatType.KnockbackPower, 0.20f));
        Make("ehwaz", "Ehwaz", "ᛖ", "Chiến mã", RuneRarity.Rare, 2,
            "Tiếng vó ngựa tám chân vọng về từ ký ức xa xăm.",
            F(StatType.ExtraJumps, 1));
        Make("wunjo", "Wunjo", "ᚹ", "Hân hoan", RuneRarity.Rare, 2,
            "Niềm vui chiến thắng, thứ từng khiến cô thấy mình còn sống.",
            F(StatType.LifestealOnHit, 1));
        Make("sowilo", "Sowilo", "ᛋ", "Mặt trời", RuneRarity.Rare, 2,
            "Ánh sáng cuối cùng cô nhìn thấy trước khi ngã xuống.",
            P(StatType.SlashDamage, 0.50f));
        Make("dagaz", "Dagaz", "ᛞ", "Bình minh", RuneRarity.Rare, 2,
            "Khoảnh khắc giữa đêm và ngày, khi mọi thứ còn có thể đổi thay.",
            P(StatType.DashCooldown, -0.30f), P(StatType.DashSpeed, 0.10f));

        // ----- Huyền thoại (3 điểm) -----
        Make("tiwaz", "Tiwaz", "ᛏ", "Chiến thần Tyr", RuneRarity.Legendary, 3,
            "Lời thề của kẻ dám hy sinh để giữ trật tự, và cô đã bị chính trật tự ấy phản bội.",
            P(StatType.MeleeDamage, 0.35f), F(StatType.EnergyOnHit, 4));

        // ----- Nguyền rủa (mạnh nhưng có cái giá) -----
        Make("hagalaz", "Hagalaz", "ᚺ", "Tai ương", RuneRarity.Cursed, 1,
            "Mưa đá không chọn người. Sức mạnh này cũng vậy.",
            P(StatType.MeleeDamage, 0.50f), P(StatType.MaxHealth, -0.30f));
        Make("naudhiz", "Naudhiz", "ᚾ", "Khốn khó", RuneRarity.Cursed, 1,
            "Chỉ trong túng quẫn, người ta mới học được cách chiến đấu hết mình.",
            F(StatType.EnergyOnHit, 8), F(StatType.DamageReduction, -0.15f));
        Make("isa", "Isa", "ᛁ", "Băng giá", RuneRarity.Cursed, 2,
            "Thời gian đóng băng quanh cô, nhưng cơ thể thì nặng như chì.",
            P(StatType.IFrameDuration, 0.50f), P(StatType.MoveSpeed, -0.10f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Rune] Đã tạo bộ rune mặc định tại " + Folder);
    }

    private static void Make(string id, string name, string glyph, string meaning,
        RuneRarity rarity, int cost, string lore, params StatModifier[] mods)
    {
        string path = $"{Folder}/Rune_{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<RuneData>(path) != null) return; // giữ bản đã chỉnh

        var rune = ScriptableObject.CreateInstance<RuneData>();
        rune.runeId = id;
        rune.displayName = name;
        rune.glyph = glyph;
        rune.meaning = meaning;
        rune.rarity = rarity;
        rune.cost = cost;
        rune.lore = lore;
        rune.modifiers.AddRange(mods);
        AssetDatabase.CreateAsset(rune, path);
    }
}
#endif
