using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gom mọi modifier (từ rune, sau này có thể từ buff/vũ khí) thành chỉ số cuối cùng.
/// Công thức: (giá trị gốc + tổng Flat) x (1 + tổng Percent).
/// Các script khác chỉ cần gọi Get(stat, giá trị gốc).
/// </summary>
public class PlayerStats : MonoBehaviour
{
    private static readonly int StatCount = Enum.GetValues(typeof(StatType)).Length;

    private readonly Dictionary<object, IReadOnlyList<StatModifier>> sources =
        new Dictionary<object, IReadOnlyList<StatModifier>>();
    private readonly float[] flat = new float[StatCount];
    private readonly float[] percent = new float[StatCount];

    /// <summary>Bắn mỗi khi chỉ số đổi (Health dùng để cập nhật máu tối đa, UI dùng để vẽ lại).</summary>
    public event Action StatsChanged;

    public void SetModifiers(object source, IReadOnlyList<StatModifier> modifiers)
    {
        sources[source] = modifiers;
        Recalculate();
    }

    public void RemoveModifiers(object source)
    {
        if (sources.Remove(source)) Recalculate();
    }

    public float Get(StatType stat, float baseValue)
    {
        int i = (int)stat;
        return (baseValue + flat[i]) * Mathf.Max(0f, 1f + percent[i]);
    }

    private void Recalculate()
    {
        Array.Clear(flat, 0, StatCount);
        Array.Clear(percent, 0, StatCount);

        foreach (var pair in sources)
        {
            foreach (StatModifier m in pair.Value)
            {
                if (m.mode == ModifierMode.Flat) flat[(int)m.stat] += m.value;
                else percent[(int)m.stat] += m.value;
            }
        }
        StatsChanged?.Invoke();
    }
}
