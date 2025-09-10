// GeminiPhotoAnalyzer.cs
using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
class GeminiPersonJson {
    public string shirt_color;
    public string emotion;
}

[Serializable]
class GeminiAnalysisJson {
    public int total_people;
    public GeminiPersonJson[] people;
}

public static class GeminiPhotoAnalyzer {
    // ★ここに API キーを設定（安全には環境変数や暗号化を推奨）
    public static string ApiKey = "YOUR_GEMINI_API_KEY";
    public static string Model = "gemini-1.5-flash"; // or "gemini-1.5-pro"

    // 画像(Texture2D)を渡して解析
    public static async Task<PhotoAnalysis> AnalyzePhotoAsync(Texture2D tex) {
        if (tex == null) throw new ArgumentNullException(nameof(tex));
        if (string.IsNullOrEmpty(ApiKey)) throw new Exception("Gemini API key 未設定");

        // 画像をJPG化→base64
        byte[] jpg = tex.EncodeToJPG(90);
        string b64 = Convert.ToBase64String(jpg);

        // プロンプト（JSON以外の文字を出させない）
        string systemInstruction = @"
あなたは画像に写っている『人物ごと』に服の色と表情を正規化してJSONで返す。
制約:
- 服の色は次から厳密に1つ: 
  ['red','royal_blue','green','aqua_blue','violet_purple','canary_yellow','orange','tropical_pink','other']
  * blue系は『royal_blue(濃い/標準の青)』か『aqua_blue(明るい/空色/水色/シアン寄り)』に割り振る
  * purple系は『violet_purple』、ピンク/マゼンタ系は『tropical_pink』
- 表情は ['smile','cry','other'] のいずれか1つ（明確に笑顔なら'smile'、泣いている/涙/号泣は'cry'、不明は'other'）
- 見切れ/遠景でも人物だと判断できるならカウント対象
- 出力は以下のJSONのみ、説明やマークダウンは禁止

出力スキーマ:
{
  ""total_people"": number,
  ""people"": [
    { ""shirt_color"": ""red|royal_blue|green|aqua_blue|violet_purple|canary_yellow|orange|tropical_pink|other"", 
      ""emotion"": ""smile|cry|other"" }
  ]
}";


        // generateContent v1beta
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={ApiKey}";

        // ★ generationConfig で JSON を強制（response_mime_type）
        var payload = new {
            contents = new[] {
                new {
                    role = "user",
                    parts = new object[] {
                        new { text = systemInstruction },
                        new {
                            inline_data = new {
                                mime_type = "image/jpeg",
                                data = b64
                            }
                        }
                    }
                }
            },
            generationConfig = new {
                response_mime_type = "application/json"
            }
        };

        string json = JsonUtilityNonStrict.ToJson(payload); // 下の簡易JSONユーティリティを使用
        var req = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        await req.SendWebRequestAwaitable();
        if (req.result != UnityWebRequest.Result.Success) {
            throw new Exception($"Gemini HTTP Error: {req.responseCode} {req.error}\n{req.downloadHandler.text}");
        }

        // 応答の取り出し
        // 典型応答: { "candidates":[{"content":{"parts":[{"text":"{...JSON...}"}]}}], ... }
        string resp = req.downloadHandler.text;
        string jsonText = GeminiResponseJsonExtractor.ExtractJson(resp);

        // パース（JsonUtilityは snake_case に合わせてクラスを作ってある）
        var parsed = JsonUtility.FromJson<GeminiAnalysisJson>(jsonText);

        // 安全側: nullガード＆正規化
        var analysis = new PhotoAnalysis {
            totalPeople = Mathf.Max(0, parsed?.total_people ?? 0),
            people = new List<PersonTag>()
        };
        if (parsed?.people != null) {
            foreach (var p in parsed.people) {
                analysis.people.Add(new PersonTag {
                    color = NormalizeColor(p?.shirt_color),
                    emotion = NormalizeEmotion(p?.emotion)
                });
            }
        }
        // total_peopleが欠けていたら people 配列長で補う
        if (analysis.totalPeople == 0 && analysis.people != null) {
            analysis.totalPeople = analysis.people.Count;
        }
        return analysis;
    }

    static ShirtColor NormalizeColor(string s) {
        if (string.IsNullOrEmpty(s)) return ShirtColor.Other;
        var t = s.ToLowerInvariant().Trim();

        // 和名・同義語もざっくり吸収
        bool Has(params string[] kws) {
            foreach (var k in kws) if (t.Contains(k)) return true;
            return false;
        }

        // --- 厳密カテゴリ ---
        if (Has("red", "crimson", "scarlet", "maroon", "赤")) return ShirtColor.Red;

        // blue系の分岐: 明るい/水色/シアン寄りは AquaBlue, それ以外は RoyalBlue
        if (Has("aqua_blue", "aqua", "sky", "light blue", "light-blue", "cyan", "turquoise", "teal", "水色", "空色", "シアン"))
            return ShirtColor.AquaBlue;
        if (Has("royal_blue", "royalblue", "blue", "navy", "indigo", "cobalt", "青", "紺", "群青"))
            return ShirtColor.RoyalBlue;

        if (Has("green", "lime", "olive", "emerald", "緑", "みどり")) return ShirtColor.Green;

        // purple と pink は明確に分ける
        if (Has("violet_purple", "violet", "purple", "lavender", "紫")) return ShirtColor.VioletPurple;

        if (Has("canary_yellow", "yellow", "lemon", "canary", "gold", "黄色")) return ShirtColor.CanaryYellow;

        if (Has("orange", "amber", "tangerine", "橙", "オレンジ")) return ShirtColor.Orange;

        if (Has("tropical_pink", "pink", "hot pink", "fuchsia", "magenta", "rose", "桃色", "ピンク"))
            return ShirtColor.TropicalPink;

        // モデルが既に厳密ラベルを返してくれた場合の直接対応
        if (t == "royal_blue") return ShirtColor.RoyalBlue;
        if (t == "aqua_blue") return ShirtColor.AquaBlue;
        if (t == "violet_purple") return ShirtColor.VioletPurple;
        if (t == "canary_yellow") return ShirtColor.CanaryYellow;
        if (t == "tropical_pink") return ShirtColor.TropicalPink;

        return ShirtColor.Other;
    }

    static Emotion NormalizeEmotion(string s) {
        if (string.IsNullOrEmpty(s)) return Emotion.Other;
        s = s.ToLowerInvariant();
        if (s.Contains("smile") || s.Contains("happy")) return Emotion.Smile;
        if (s.Contains("cry") || s.Contains("tears") || s.Contains("sad")) return Emotion.Cry;
        if (s == "smile") return Emotion.Smile;
        if (s == "cry") return Emotion.Cry;
        return Emotion.Other;
    }
}

/// <summary>
/// Geminiの応答JSONから、本文のJSON文字列だけを抜き出す
/// </summary>
static class GeminiResponseJsonExtractor {
    [Serializable] class PartText { public string text; }
    [Serializable] class Content { public PartText[] parts; }
    [Serializable] class Candidate { public Content content; }
    [Serializable] class Root { public Candidate[] candidates; }

    public static string ExtractJson(string whole) {
        try {
            var root = JsonUtility.FromJson<Root>(whole);
            var text = root?.candidates?[0]?.content?.parts?[0]?.text;
            if (string.IsNullOrEmpty(text)) throw new Exception("parts[0].text が空");
            // モデルが ```json ... ``` で囲んだ場合の除去
            text = text.Trim();
            if (text.StartsWith("```")) {
                int start = text.IndexOf('{');
                int end = text.LastIndexOf('}');
                if (start >= 0 && end > start) {
                    text = text.Substring(start, end - start + 1);
                }
            }
            return text;
        } catch {
            // 失敗時はそのまま返して上位で例外にする
            return whole;
        }
    }
}

/// <summary>
/// Unity標準のJsonUtilityは匿名型/Dicに弱いので、必要最低限の ToJson だけ提供
/// </summary>
static class JsonUtilityNonStrict {
    public static string ToJson(object o) {
        // 最小限: Newtonsoft を使わない代替（簡易・安全のため）
        // ここでは payload の構造が単純なので Unity の JsonUtility で二段階にする
        return MiniJson.Serialize(o);
    }
}

// --- MiniJson（軽量シリアライザ） ---
// 出典: Unity公式サンプルに準拠した簡易版（プロダクションなら Newtonsoft を推奨）
static class MiniJson {
    public static string Serialize(object obj) => Serializer.Serialize(obj);

    class Serializer {
        StringBuilder builder = new StringBuilder();

        public static string Serialize(object obj) {
            var instance = new Serializer();
            instance.SerializeValue(obj);
            return instance.builder.ToString();
        }

        void SerializeValue(object value) {
            if (value == null) { builder.Append("null"); return; }

            if (value is string s) { SerializeString(s); return; }
            if (value is bool b) { builder.Append(b ? "true" : "false"); return; }
            if (value is IDictionary<string, object> dict) { SerializeObject(dict); return; }
            if (value is System.Collections.IEnumerable enumerable && !(value is string)) { SerializeArray(enumerable); return; }
            if (value is int || value is long || value is float || value is double || value is decimal) {
                builder.Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                return;
            }

            // 匿名型や new { a=1 } を Dictionary に粗変換
            var props = value.GetType().GetProperties();
            var map = new Dictionary<string, object>();
            foreach (var p in props) map[p.Name] = p.GetValue(value, null);
            SerializeObject(map);
        }

        void SerializeObject(IDictionary<string, object> obj) {
            bool first = true;
            builder.Append('{');
            foreach (var kv in obj) {
                if (!first) builder.Append(',');
                SerializeString(kv.Key);
                builder.Append(':');
                SerializeValue(kv.Value);
                first = false;
            }
            builder.Append('}');
        }

        void SerializeArray(System.Collections.IEnumerable array) {
            builder.Append('[');
            bool first = true;
            foreach (var obj in array) {
                if (!first) builder.Append(',');
                SerializeValue(obj);
                first = false;
            }
            builder.Append(']');
        }

        void SerializeString(string str) {
            builder.Append('\"');
            foreach (var c in str) {
                switch (c) {
                    case '\"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (c < ' ') builder.Append("\\u" + ((int)c).ToString("x4"));
                        else builder.Append(c);
                        break;
                }
            }
            builder.Append('\"');
        }
    }
}

/// <summary>
/// UnityWebRequest を await できるようにする拡張
/// </summary>
static class UnityWebRequestAwaiter {
    public static async Task SendWebRequestAwaitable(this UnityWebRequest req) {
        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();
    }
}
