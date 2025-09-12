using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation; // SpriteSkin / SpriteResolver

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(SpriteSkin))]
[RequireComponent(typeof(SpriteResolver))]
public class ClothesColorSwitcher : MonoBehaviour
{
    // 8色（ラベル名は SpriteLibrary の Label と完全一致させてください）
    public enum ClothesColor { Pink, SkyBlue, Yellow, Red, Orange, Green, Magenta, Navy }

    [Header("体(マスター)の最上位ボーン")]
    public Transform bodyRoot;                 // 例: root/hips

    [Header("服をぶら下げる体ボーン名（空なら bodyRoot 直下）")]
    public string attachBoneName = "spine";

    [Header("骨名が違う場合だけ対応表（任意）")]
    public BoneMap[] boneNameMap;              // 例: from=neck → to=head
    [Serializable] public struct BoneMap { public string from; public string to; }

    SpriteRenderer _sr;
    SpriteSkin _skin;
    SpriteResolver _resolver;
    Sprite _lastSprite;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _skin = GetComponent<SpriteSkin>();
        _resolver = GetComponent<SpriteResolver>();

        // 起動時も一度取り付け＆再バインド
        TryAttach();
        RebindNow();
        _lastSprite = _sr.sprite;
    }

    void LateUpdate()
    {
        // ラベル切替等で Sprite が変わったら自動再バインド
        if (_sr.sprite != _lastSprite)
        {
            _lastSprite = _sr.sprite;
            TryAttach();
            RebindNow();
        }
    }

    // ===== 公開API：色を切り替える =====
    public void SetColor(ClothesColor color)
    {
        if (_resolver == null) return;
        _resolver.SetCategoryAndLabel("Clothes", LabelOf(color)); // ラベル名は下の関数
        // 直後に自動で LateUpdate でも検知して Rebind されますが、
        // 念のため即時に実行
        TryAttach();
        RebindNow();
    }

    // === 実装 ===
    void TryAttach()
    {
        if (!bodyRoot || string.IsNullOrEmpty(attachBoneName)) return;
        var parent = FindDeep(bodyRoot, attachBoneName);
        if (parent && transform.parent != parent)
            transform.SetParent(parent, worldPositionStays: false);
    }

    void RebindNow()
    {
        if (!_skin || !bodyRoot) return;

        var map = BuildMap(boneNameMap);

        // rootBone は触らず、boneTransforms の「要素」だけを体の骨に置換（配列ごと代入は不可）
        var bones = _skin.boneTransforms; // 読み取り専用だが要素代入は可能
        if (bones != null)
        {
            for (int i = 0; i < bones.Length; i++)
                bones[i] = MapBone(bones[i], bodyRoot, map);
        }

        // 反映（有効/無効トグル）
        var was = _skin.enabled;
        _skin.enabled = false; _skin.enabled = was || true;
    }

    static Transform MapBone(Transform src, Transform root, Dictionary<string, string> map)
    {
        if (!src) return null;
        var name = (map != null && map.TryGetValue(src.name, out var to)) ? to : src.name;
        return FindDeep(root, name);
    }

    static Dictionary<string, string> BuildMap(BoneMap[] pairs)
    {
        var d = new Dictionary<string, string>();
        if (pairs != null)
            foreach (var p in pairs)
                if (!string.IsNullOrEmpty(p.from))
                    d[p.from] = string.IsNullOrEmpty(p.to) ? p.from : p.to;
        return d;
    }

    static Transform FindDeep(Transform r, string n)
    {
        if (!r) return null; if (r.name == n) return r;
        for (int i = 0; i < r.childCount; i++)
        {
            var t = FindDeep(r.GetChild(i), n);
            if (t) return t;
        }
        return null;
    }

    // ラベル名は SpriteLibrary の Label と完全一致にしてください
    static string LabelOf(ClothesColor c) => c switch
    {
        ClothesColor.Pink => "Pink",
        ClothesColor.SkyBlue => "SkyBlue",
        ClothesColor.Yellow => "Yellow",
        ClothesColor.Red => "Red",
        ClothesColor.Orange => "Orange",
        ClothesColor.Green => "Green",
        ClothesColor.Magenta => "Magenta",
        ClothesColor.Navy => "Navy",
        _ => "Pink"
    };
}
