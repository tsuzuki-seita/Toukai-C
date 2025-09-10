// Services/WebCamCameraService.cs
using System;
using UniRx;
using UnityEngine;

public interface ICameraService {
    // useFront = true で内カメラ、false で外カメラ
    IObservable<(Texture2D tex, byte[] jpg)> CaptureOneJpg(bool useFront, int w=1280, int h=720, int fps=30, int settleMs=700);
}

public class WebCamCameraService : ICameraService {
    WebCamTexture cam;

    public IObservable<(Texture2D tex, byte[] jpg)> CaptureOneJpg(bool useFront, int w=1280, int h=720, int fps=30, int settleMs=700) {
        return Observable.FromCoroutine<(Texture2D, byte[])>(o => Routine(o, useFront, w, h, fps, settleMs));
    }

    System.Collections.IEnumerator Routine(IObserver<(Texture2D, byte[])> o, bool useFront, int w, int h, int fps, int settleMs) {
        // 権限
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam)) {
            var req = Application.RequestUserAuthorization(UserAuthorization.WebCam);
            yield return req;
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam)) {
                o.OnError(new Exception("Camera permission denied")); yield break;
            }
        }

        // デバイス選択
        string dev = null;
        foreach (var d in WebCamTexture.devices) {
            if (d.isFrontFacing == useFront) { dev = d.name; break; }
        }
        if (dev == null && WebCamTexture.devices.Length > 0) dev = WebCamTexture.devices[0].name;

        cam = dev == null ? new WebCamTexture(w, h, fps) : new WebCamTexture(dev, w, h, fps);
        cam.Play();

        // フレーム待ち
        float t0 = Time.realtimeSinceStartup;
        while (!cam.didUpdateThisFrame && Time.realtimeSinceStartup - t0 < 5f) yield return null;
        if (cam.width <= 16) { o.OnError(new Exception("Camera not ready")); yield break; }

        // 露出安定
        yield return new WaitForSeconds(settleMs / 1000f);

        // 1枚
        var tex = new Texture2D(cam.width, cam.height, TextureFormat.RGBA32, false);
        tex.SetPixels32(cam.GetPixels32()); tex.Apply();
        var jpg = tex.EncodeToJPG(90);

        cam.Stop(); cam = null;
        o.OnNext((tex, jpg)); o.OnCompleted();
    }
}
