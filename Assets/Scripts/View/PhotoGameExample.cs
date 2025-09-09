// PhotoGameExample.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class PhotoGameExample : MonoBehaviour {
    WebCamTexture cam;

    [Header("Gemini")]
    public string geminiApiKey; // インスペクタで設定
    public string model = "gemini-1.5-flash";

    void Start() {
        cam = new WebCamTexture();
        cam.Play();

        // APIキー注入
        GeminiPhotoAnalyzer.ApiKey = geminiApiKey;
        GeminiPhotoAnalyzer.Model = model;
    }

    [ContextMenu("Capture And Judge")]
    public async void CaptureAndJudge() {
        var tex = CaptureFromWebCam();
        var analysis = await GeminiPhotoAnalyzer.AnalyzePhotoAsync(tex);

        // 例: 「笑顔×青1人」「泣き×赤1人」で倒せる敵
        var requires = new List<EnemyRequirement> {
            new EnemyRequirement{ color = ShirtColor.AquaBlue, emotion = Emotion.Smile, count=1 },
            new EnemyRequirement{ color = ShirtColor.Red,  emotion = Emotion.Cry,   count=1 },
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
