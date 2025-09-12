using UnityEngine;

public class Spawner : MonoBehaviour
{
    [Header("生成したいプレハブ（Rootに OjisanInit が付いたもの）")]
    [SerializeField] private OjisanInit ojisanPrefab;

    [Header("配置先（空ならワールド直下）")]
    [SerializeField] private Transform spawnParent;
    [SerializeField] private Vector3 spawnPos = Vector3.zero;

    [Header("生成と同時に指定する初期値")]
    [SerializeField] private ClothesTint.ClothesColor initialColor = ClothesTint.ClothesColor.White;
    [SerializeField] private OjisanInit.FaceLabel initialFace = OjisanInit.FaceLabel.Smile;

    // 起動時に自動で1体出したいなら有効化
    [SerializeField] private bool spawnOnStart = false;

    void Start()
    {
        if (spawnOnStart) Spawn();
    }

    // ボタンからも呼べる
    public OjisanInit Spawn()
    {
        if (!ojisanPrefab) { Debug.LogWarning("[Spawner] ojisanPrefab 未設定"); return null; }

        var o = Instantiate(ojisanPrefab, spawnPos, Quaternion.identity, spawnParent);
        o.Initialize(initialColor, initialFace); // ← ここが「生成と同時に指定」の本体
        return o;
    }
}
