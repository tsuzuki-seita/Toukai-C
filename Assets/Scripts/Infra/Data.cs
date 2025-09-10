// EnemySpec.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public enum ShirtColor {
    Red, RoyalBlue, Green, AquaBlue, VioletPurple, CanaryYellow, Orange, TropicalPink, Other
}

// 表情は4種のみ
public enum Emotion { Smile, Sleep, Surprise, Wink }

[Serializable] public class EnemyRequirement {
    public ShirtColor color;
    public Emotion emotion;
    public int count = 1;
}

[Serializable] public class PersonTag {
    public ShirtColor color;
    public Emotion emotion;
}

[Serializable] public class PhotoAnalysis {
    public int totalPeople;
    public List<PersonTag> people = new List<PersonTag>();
}

// ===== Waves =====
[Serializable] public class Wave {
    public string name = "Wave";
    public float timeLimitSec = 60f;              // ウェーブごとの持ち時間
    public List<EnemyRequirement> enemies;        // このウェーブで倒すべき条件
    public int TotalEnemyCount() {
        int s = 0; if (enemies != null) foreach (var e in enemies) s += Mathf.Max(0, e.count); return s;
    }
}
