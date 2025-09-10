// Services/GeminiAnalyzer.cs
using System;
using System.Text;
using UniRx;
using UnityEngine;
using UnityEngine.Networking;

public interface IGeminiAnalyzer { IObservable<PhotoAnalysis> Analyze(Texture2D tex); }

public class GeminiAnalyzer : IGeminiAnalyzer {
    readonly string apiKey; readonly string model;
    public GeminiAnalyzer(string apiKey, string model = "gemini-2.0-flash") { this.apiKey = apiKey; this.model = model; }

    public IObservable<PhotoAnalysis> Analyze(Texture2D tex) => Observable.FromCoroutine<PhotoAnalysis>(o => Routine(o, tex));

    System.Collections.IEnumerator Routine(IObserver<PhotoAnalysis> o, Texture2D tex) {
        var jpg = tex.EncodeToJPG(90);
        var b64 = Convert.ToBase64String(jpg);

        // ★ 表情は Smile/Sleep/Surprise/Wink の4種のみ
        string systemInstruction = @"
あなたは画像に写っている『人物ごと』に服の色と表情を正規化してJSONで返す。
制約:
- 服の色は厳密に1つ:
  ['red','royal_blue','green','aqua_blue','violet_purple','canary_yellow','orange','tropical_pink','other']
- 表情は厳密に1つ:
  ['smile','sleep','surprise','wink']
  * 'sleep' は寝顔/目を閉じて眠そう・居眠り
  * 'surprise' は驚き/目を見開く/口が大きく開く
  * 'wink' は片目を閉じたウインク
- 出力は以下のJSONのみ、説明やマークダウンは禁止
{
  ""total_people"": number,
  ""people"": [
    { ""shirt_color"": ""..."", ""emotion"": ""..."" }
  ]
}";

        var payload = new {
            contents = new[] {
                new { role="user", parts=new object[]{
                    new { text = systemInstruction },
                    new { inline_data = new { mime_type="image/jpeg", data=b64 } }
                } }
            },
            generationConfig = new { response_mime_type = "application/json" }
        };
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
        var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(MiniJson.Serialize(payload)));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) { o.OnError(new Exception($"Gemini HTTP {req.responseCode}: {req.error}")); yield break; }

        string resp = req.downloadHandler.text;
        string json = ExtractJson(resp);
        var parsed = JsonUtility.FromJson<GeminiAnalysisJson>(json);

        var result = new PhotoAnalysis { totalPeople = Mathf.Max(0, parsed?.total_people ?? 0), people = new System.Collections.Generic.List<PersonTag>() };
        if (parsed?.people != null) {
            foreach (var p in parsed.people) {
                result.people.Add(new PersonTag { color = NormalizeColor(p.shirt_color), emotion = NormalizeEmotion(p.emotion) });
            }
        }
        if (result.totalPeople == 0) result.totalPeople = result.people.Count;

        o.OnNext(result); o.OnCompleted();
    }

    // --- JSON helpers ---
    [Serializable] class GeminiPersonJson { public string shirt_color; public string emotion; }
    [Serializable] class GeminiAnalysisJson { public int total_people; public GeminiPersonJson[] people; }
    [Serializable] class PartText { public string text; }
    [Serializable] class Content { public PartText[] parts; }
    [Serializable] class Candidate { public Content content; }
    [Serializable] class Root { public Candidate[] candidates; }
    static string ExtractJson(string whole) {
        var root = JsonUtility.FromJson<Root>(whole);
        var text = root?.candidates?[0]?.content?.parts?[0]?.text ?? "{}";
        text = text.Trim();
        if (text.StartsWith("```")) { int s = text.IndexOf('{'); int e = text.LastIndexOf('}'); if (s>=0 && e>s) text = text.Substring(s, e-s+1); }
        return text;
    }

    // --- 9色正規化（前回のまま） ---
    static ShirtColor NormalizeColor(string s) {
        if (string.IsNullOrEmpty(s)) return ShirtColor.Other;
        var t = s.ToLowerInvariant(); bool Has(params string[] kws){ foreach(var k in kws) if (t.Contains(k)) return true; return false; }
        if (Has("aqua_blue","aqua","sky","light blue","cyan","turquoise","teal","水色","空色","シアン")) return ShirtColor.AquaBlue;
        if (Has("royal_blue","royalblue","blue","navy","indigo","cobalt","青","紺","群青")) return ShirtColor.RoyalBlue;
        if (Has("red","crimson","scarlet","maroon","赤")) return ShirtColor.Red;
        if (Has("green","lime","olive","emerald","緑","みどり")) return ShirtColor.Green;
        if (Has("violet_purple","violet","purple","lavender","紫")) return ShirtColor.VioletPurple;
        if (Has("canary_yellow","yellow","lemon","canary","gold","黄色")) return ShirtColor.CanaryYellow;
        if (Has("orange","amber","tangerine","橙","オレンジ")) return ShirtColor.Orange;
        if (Has("tropical_pink","pink","hot pink","fuchsia","magenta","rose","桃色","ピンク")) return ShirtColor.TropicalPink;
        if (t=="royal_blue") return ShirtColor.RoyalBlue;
        if (t=="aqua_blue")  return ShirtColor.AquaBlue;
        if (t=="violet_purple") return ShirtColor.VioletPurple;
        if (t=="canary_yellow") return ShirtColor.CanaryYellow;
        if (t=="tropical_pink") return ShirtColor.TropicalPink;
        return ShirtColor.Other;
    }

    // --- 表情4種にマップ ---
    static Emotion NormalizeEmotion(string s) {
        if (string.IsNullOrEmpty(s)) return Emotion.Smile; // フォールバックは任意
        var t = s.ToLowerInvariant();
        // 😄 笑顔
        if (t.Contains("smile") || t.Contains("happy") || t.Contains("grin")) return Emotion.Smile;
        // 😪 寝顔
        if (t.Contains("sleep") || t.Contains("asleep") || t.Contains("sleepy") || t.Contains("doze")) return Emotion.Sleep;
        // 😱 驚き顔
        if (t.Contains("surprise") || t.Contains("surprised") || t.Contains("astonish") || t.Contains("shock") || t.Contains("wow")) return Emotion.Surprise;
        // 😉 ウインク
        if (t.Contains("wink")) return Emotion.Wink;

        // 厳密ラベル対応
        if (t=="sleep") return Emotion.Sleep;
        if (t=="surprise") return Emotion.Surprise;
        if (t=="wink") return Emotion.Wink;
        return Emotion.Smile;
    }
}

// MiniJson（簡易のまま）
static class MiniJson {
    public static string Serialize(object obj) => Serializer.Serialize(obj);
    class Serializer {
        System.Text.StringBuilder b = new System.Text.StringBuilder();
        public static string Serialize(object o){ var s=new Serializer(); s.Val(o); return s.b.ToString(); }
        void Val(object v){
            if (v==null){ b.Append("null"); return; }
            switch (v) {
                case string s: Str(s); return;
                case bool bo: b.Append(bo?"true":"false"); return;
                case int or long or float or double or decimal: b.Append(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)); return;
            }
            if (v is System.Collections.IDictionary dic){ Obj(dic); return; }
            if (v is System.Collections.IEnumerable en){ Arr(en); return; }
            var map=new System.Collections.Generic.Dictionary<string,object>();
            foreach(var p in v.GetType().GetProperties()) map[p.Name]=p.GetValue(v,null);
            Obj(map);
        }
        void Obj(System.Collections.IDictionary d){ b.Append('{'); bool f=true;
            foreach (System.Collections.DictionaryEntry kv in d){ if(!f)b.Append(','); Str((string)kv.Key); b.Append(':'); Val(kv.Value); f=false; } b.Append('}');
        }
        void Arr(System.Collections.IEnumerable a){ b.Append('['); bool f=true; foreach(var x in a){ if(!f)b.Append(','); Val(x); f=false; } b.Append(']'); }
        void Str(string s){ b.Append('\"'); foreach(var c in s){ switch(c){ case '\"': b.Append("\\\""); break; case '\\': b.Append("\\\\"); break; case '\b': b.Append("\\b"); break; case '\f': b.Append("\\f"); break; case '\n': b.Append("\\n"); break; case '\r': b.Append("\\r"); break; case '\t': b.Append("\\t"); break; default: if (c<' ') b.Append("\\u"+((int)c).ToString("x4")); else b.Append(c); break; } } b.Append('\"'); }
    }
}
