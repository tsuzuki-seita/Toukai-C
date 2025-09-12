using UnityEngine;

public class OjisanInit : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ClothesTint clothes;      // Clothes に付けたやつ
    [SerializeField] private SpriteRenderer face;      // Face の SpriteRenderer

    [Header("顔スプライト（Inspectorで割当）")]
    [SerializeField] private Sprite faceNeutral;
    [SerializeField] private Sprite faceSmile;
    [SerializeField] private Sprite faceBlink;
    [SerializeField] private Sprite faceAngry;

    public enum FaceLabel { Neutral, Smile, Blink, Angry }

    // ====== 生成直後まとめて指定（メソッド派） ======
    public void Initialize(ClothesTint.ClothesColor clothesColor, FaceLabel faceLabel)
    {
        Clothes = clothesColor;
        Face = faceLabel;
    }
    public void Initialize(ClothesTint.ClothesColor clothesColor, Sprite faceSprite)
    {
        Clothes = clothesColor;
        if (face) face.sprite = faceSprite;
    }

    // ====== プロパティ派（ojisan.Face = FaceLabel.Smile; など） ======
    public ClothesTint.ClothesColor Clothes
    {
        get => _clothes;
        set { _clothes = value; if (clothes) clothes.SetColor(value); }
    }
    public FaceLabel Face
    {
        get => _face;
        set { _face = value; if (face) face.sprite = SpriteOf(value); }
    }
    private ClothesTint.ClothesColor _clothes = ClothesTint.ClothesColor.White;
    private FaceLabel _face = FaceLabel.Neutral;

    // 参照入れ忘れ対策（任意）
    void Awake()
    {
        if (!clothes) clothes = GetComponentInChildren<ClothesTint>(true);
        if (!face) face = transform.Find("Face")?.GetComponent<SpriteRenderer>();
    }

    Sprite SpriteOf(FaceLabel label) => label switch
    {
        FaceLabel.Neutral => faceNeutral,
        FaceLabel.Smile => faceSmile,
        FaceLabel.Blink => faceBlink,
        FaceLabel.Angry => faceAngry,
        _ => faceNeutral
    };
}
