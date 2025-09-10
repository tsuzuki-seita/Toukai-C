// PhotoGameExample.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class PhotoGameExample : MonoBehaviour 
{
    WebCamTexture cam;

    [SerializeField] RawImage preview;
    Texture2D capturedTexture;

    [Header("Gemini")]
    public string geminiApiKey; // インスペクタで設定
    public string model = "gemini-2.0-flash";

    void Start()
    {
        // cam = new WebCamTexture();
        // cam.Play();

        // APIキー注入
        GeminiPhotoAnalyzer.ApiKey = geminiApiKey;
        GeminiPhotoAnalyzer.Model = model;

        capturedTexture = preview.texture as Texture2D;
        CaptureAndJudge();
    }

    [ContextMenu("Capture And Judge")]
    public async void CaptureAndJudge() {
        // var tex = CaptureFromWebCam();
        var tex = capturedTexture;
        var analysis = await GeminiPhotoAnalyzer.AnalyzePhotoAsync(tex);

        // 例: 「笑顔×青1人」「泣き×赤1人」で倒せる敵
        var requires = new List<EnemyRequirement> {
            new EnemyRequirement{ color = ShirtColor.Orange, emotion = Emotion.Smile, count=1 },
            // new EnemyRequirement{ color = ShirtColor.Green,  emotion = Emotion.Cry,   count=1 },
        };

        var (ok, missing) = PhotoJudge.Check(analysis, requires);
        if (ok) {
            Debug.Log($"勝ち: total={analysis.totalPeople}, people={analysis.people.Count}");
            // ここで撃破処理
        } else {
            foreach (var m in missing) {
                Debug.Log($"不足: {m.emotion} x {m.color} を {m.count} 人");
            }
        }
    }

    Texture2D CaptureFromWebCam() {
        var tex = new Texture2D(cam.width, cam.height, TextureFormat.RGBA32, false);
        tex.SetPixels32(cam.GetPixels32());
        tex.Apply();
        return tex;
    }
}
