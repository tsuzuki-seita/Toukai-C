// Services/WebCamCameraService.cs 〈差し替え〉
using System;
using UniRx;
using UnityEngine;

public struct CameraPreviewInfo {
    public WebCamTexture cam;
    public int rotation;            // cam.videoRotationAngle
    public bool vMirror;            // cam.videoVerticallyMirrored
    public bool hMirror;            // 内カメラの見え方を“鏡像”にするなら true（プレビュー用）
}

public interface ICameraService {
    IObservable<CameraPreviewInfo> StartPreview(bool useFront, int w=1280, int h=720, int fps=30);
    void StopPreview();
    bool IsPreviewRunning { get; }
    // プレビュー中のフレームを「正しい向き（回転・反転補正済み）」で1枚取得してJPG化
    IObservable<(Texture2D tex, byte[] jpg)> CaptureOneFromPreviewUpright(bool unMirrorFront=true, int jpgQuality=90);
}

public class WebCamCameraService : ICameraService {
    WebCamTexture cam;
    bool currentFront = false;

    public bool IsPreviewRunning => cam != null && cam.isPlaying;

    public IObservable<CameraPreviewInfo> StartPreview(bool useFront, int w=1280, int h=720, int fps=30) {
        return Observable.FromCoroutine<CameraPreviewInfo>(observer => RoutineStart(observer, useFront, w, h, fps));
    }

    System.Collections.IEnumerator RoutineStart(IObserver<CameraPreviewInfo> o, bool useFront, int w, int h, int fps) {
        // 権限
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam)) {
            var req = Application.RequestUserAuthorization(UserAuthorization.WebCam);
            yield return req;
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam)) { o.OnError(new Exception("Camera permission denied")); yield break; }
        }

        // 既存を止める
        StopPreview();

        // デバイス選択
        string dev = null;
        foreach (var d in WebCamTexture.devices) if (d.isFrontFacing == useFront) { dev = d.name; break; }
        if (dev == null && WebCamTexture.devices.Length > 0) dev = WebCamTexture.devices[0].name;

        cam = dev == null ? new WebCamTexture(w, h, fps) : new WebCamTexture(dev, w, h, fps);
        currentFront = useFront;
        cam.Play();

        // 最初のフレーム待ち（5s）
        float t0 = Time.realtimeSinceStartup;
        while (!cam.didUpdateThisFrame && Time.realtimeSinceStartup - t0 < 5f) yield return null;
        if (cam.width <= 16) { o.OnError(new Exception("Camera not ready")); yield break; }

        var info = new CameraPreviewInfo {
            cam      = cam,
            rotation = cam.videoRotationAngle,       // 0/90/180/270
            vMirror  = cam.videoVerticallyMirrored,  // UIでuvRect.yを反転
            hMirror  = currentFront                  // 内カメラは“鏡像プレビュー”したいなら true
        };
        o.OnNext(info); o.OnCompleted();
    }

    public void StopPreview() {
        if (cam != null) { cam.Stop(); cam = null; }
    }

    public IObservable<(Texture2D tex, byte[] jpg)> CaptureOneFromPreviewUpright(bool unMirrorFront=true, int jpgQuality=90) {
        return Observable.Create<(Texture2D, byte[])>(observer => {
            if (!IsPreviewRunning) { observer.OnError(new Exception("Preview not running")); return Disposable.Empty; }

            // 1枚取り出し
            var src = new Texture2D(cam.width, cam.height, TextureFormat.RGBA32, false);
            src.SetPixels32(cam.GetPixels32());
            src.Apply();

            // 向き補正（回転＋必要なら反転）
            bool flipH = false;
            bool flipV = cam.videoVerticallyMirrored; // Unity側が上下反転のことがある
            if (currentFront && unMirrorFront) {
                // 保存は鏡像を解除（一般的な写真の向きに合わせる）
                flipH = true;
            }

            var upright = RotateAndFlip(src, cam.videoRotationAngle, flipH, flipV);
            UnityEngine.Object.Destroy(src);

            byte[] jpg = upright.EncodeToJPG(jpgQuality);
            observer.OnNext((upright, jpg));
            observer.OnCompleted();
            return Disposable.Empty;
        });
    }

    // 回転角は 0/90/180/270 を想定
    static Texture2D RotateAndFlip(Texture2D src, int angle, bool flipH, bool flipV) {
        Color32[] pix = src.GetPixels32();
        int w = src.width, h = src.height;
        int newW = (angle == 90 || angle == 270) ? h : w;
        int newH = (angle == 90 || angle == 270) ? w : h;
        var dst = new Texture2D(newW, newH, TextureFormat.RGBA32, false);
        var outpix = new Color32[newW * newH];

        Func<int,int,int> idxSrc = (x,y) => y * w + x;

        for (int y=0; y<newH; y++) {
            for (int x=0; x<newW; x++) {
                int sx=0, sy=0;
                switch (angle) {
                    case 0:   sx = x;          sy = y;          break;
                    case 90:  sx = y;          sy = w - 1 - x;  break;
                    case 180: sx = w - 1 - x;  sy = h - 1 - y;  break;
                    case 270: sx = h - 1 - y;  sy = x;          break;
                    default:  sx = x;          sy = y;          break;
                }
                if (flipH) sx = ( (angle==90||angle==270) ? newW : w ) - 1 - sx;
                if (flipV) sy = ( (angle==90||angle==270) ? newH : h ) - 1 - sy;
                // sx,sy は元座標系に合わせる必要があるので、角度別に補正済みの上で使う
                outpix[y * newW + x] = pix[idxSrc(Mathf.Clamp(sx,0,w-1), Mathf.Clamp(sy,0,h-1))];
            }
        }
        dst.SetPixels32(outpix);
        dst.Apply();
        return dst;
    }
}
