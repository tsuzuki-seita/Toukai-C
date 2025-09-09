// EnemySpec.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public enum ShirtColor { Red, RoyalBlue, Green, AquaBlue, VioletPurple, CanaryYellow, Orange, TropicalPink, Other }
public enum Emotion { Smile, Cry, Other }

[Serializable]
public class EnemyRequirement {
    public ShirtColor color;
    public Emotion emotion;
    public int count = 1;
}

[Serializable]
public class PersonTag {
    public ShirtColor color;
    public Emotion emotion;
}

[Serializable]
public class PhotoAnalysis {
    public int totalPeople;
    public List<PersonTag> people = new List<PersonTag>();

    // 集計を返す（(色,表情)→人数）
    public Dictionary<(ShirtColor, Emotion), int> CountBuckets() {
        var dict = new Dictionary<(ShirtColor, Emotion), int>();
        foreach (var p in people) {
            var key = (p.color, p.emotion);
            dict.TryGetValue(key, out var v);
            dict[key] = v + 1;
        }
        return dict;
    }
}
