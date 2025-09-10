// PhotoJudge.cs
using System.Collections.Generic;

public static class PhotoJudge {
    // 要件を満たしているか、満たしていない分を返す
    public static (bool ok, List<EnemyRequirement> missing) Check(PhotoAnalysis result, List<EnemyRequirement> requires) {
        var counts = result.CountBuckets();
        var missing = new List<EnemyRequirement>();
        foreach (var req in requires) {
            counts.TryGetValue((req.color, req.emotion), out var have);
            if (have < req.count) {
                missing.Add(new EnemyRequirement {
                    color = req.color,
                    emotion = req.emotion,
                    count = req.count - have
                });
            }
        }
        return (missing.Count == 0, missing);
    }
}
