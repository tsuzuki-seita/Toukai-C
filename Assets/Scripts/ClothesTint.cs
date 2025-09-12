using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class ClothesTint : MonoBehaviour
{
    public enum ClothesColor { White, Pink, SkyBlue, Yellow, Red, Orange, Green, Magenta, Navy }

    SpriteRenderer _sr;

    // 追記ここから
    [SerializeField] ClothesColor initialColor = ClothesColor.White;
    void Start() { SetColor(initialColor); }
    // 追記ここまで

    void Awake() { _sr = GetComponent<SpriteRenderer>(); }

    public void SetColor(ClothesColor c)
    {
        _sr.color = ColorOf(c);
    }

    static Color ColorOf(ClothesColor c) => c switch
    {
        ClothesColor.White => Color.white,
        ClothesColor.Pink => Hex("#FF4FA0"),
        ClothesColor.SkyBlue => Hex("#37B5FF"),
        ClothesColor.Yellow => Hex("#FFD400"),
        ClothesColor.Red => Hex("#FF3B30"),
        ClothesColor.Orange => Hex("#FF8A00"),
        ClothesColor.Green => Hex("#1ABC5C"),
        ClothesColor.Magenta => Hex("#D100A2"),
        ClothesColor.Navy => Hex("#0D47A1"),
        _ => Color.white
    };

    static Color Hex(string hex)
    {
        Color c; return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.white;
    }
}
