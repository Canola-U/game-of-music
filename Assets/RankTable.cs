using System.Collections.Generic;
using UnityEngine;

// 정확도 → 등급(티어) 표. 결과 화면과 화면 전환이 같이 쓴다
public static class RankTable
{
    public class Tier
    {
        public string name;
        public float minAccuracy;   // 이 정확도(%) 이상이면 이 티어
        public Color color;

        public Tier(string name, float minAccuracy, Color color)
        {
            this.name = name;
            this.minAccuracy = minAccuracy;
            this.color = color;
        }
    }

    // 높은 티어부터 순서대로
    public static readonly List<Tier> tiers = new List<Tier>
    {
        new Tier("SSS", 99f, Hex(0xFFF1A8)),
        new Tier("SS", 97f, Hex(0xFFD54F)),
        new Tier("S", 95f, Hex(0xFFB300)),
        new Tier("A", 90f, Hex(0xBA68C8)),
        new Tier("B", 80f, Hex(0x64B5F6)),
        new Tier("C", 70f, Hex(0x81C784)),
        new Tier("D", 60f, Hex(0xA1887F)),
        new Tier("F", 0f, Hex(0x757575)),
    };

    // 정확도 100% = GOD (티어 표 위의 특별 등급)
    public const string godName = "GOD";
    public static readonly Color godColor = Hex(0xFF1744);

    public static bool IsGod(float accuracy) => accuracy >= 100f;

    public static Tier Get(float accuracy)
    {
        foreach (Tier t in tiers)
        {
            if (accuracy >= t.minAccuracy) return t;
        }
        return tiers[tiers.Count - 1];
    }

    public static string NameFor(float accuracy) => IsGod(accuracy) ? godName : Get(accuracy).name;
    public static Color ColorFor(float accuracy) => IsGod(accuracy) ? godColor : Get(accuracy).color;

    public static Color Hex(uint rgb)
    {
        return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
