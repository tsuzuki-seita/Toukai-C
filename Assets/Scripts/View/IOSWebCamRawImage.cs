using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class IOSCameraToRawImage : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RawImage RawImage;               // 3:4 のままサイズ固定（元の仕様を維持）
    [SerializeField] private bool startWithBackCamera = true; // 起動時に背面カメラで始めるか

    private WebCamTexture webCam;
    private WebCamDevice[] devices;
    private int currentDeviceIndex = -1;

    private void OnEnable()
    {
        StartCoroutine(Init());
    }

    private IEnumerator Init()
    {
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

        devices = WebCamTexture.devices;

        // 初期カメラ（背面 or 前面）
        int initialIndex = GetIndexByFrontFacing(front: !startWithBackCamera);
        if (initialIndex < 0) initialIndex = devices.Length > 0 ? 0 : -1;

        if (initialIndex >= 0)
            yield return StartCoroutine(StartCamera(initialIndex));

        ToggleCamera(); // 1回切り替えて、希望のカメラにする
        ToggleCamera();
    }

    /// <summary>
    /// ボタンから呼ぶ：外⇄内カメラを切り替え
    /// </summary>
    public void ToggleCamera()
    {
        if (devices == null || devices.Length == 0) return;
        if (currentDeviceIndex < 0) return;

        bool wantFront = !devices[currentDeviceIndex].isFrontFacing;
        int nextIndex = GetIndexByFrontFacing(wantFront);

        // 同じ種類が無ければ切り替えしない（例：カメラ1台）
        if (nextIndex < 0 || nextIndex == currentDeviceIndex) return;

        StopCurrentCamera();
        StartCoroutine(StartCamera(nextIndex));
    }

    private int GetIndexByFrontFacing(bool front)
    {
        if (devices == null) return -1;
        for (int i = 0; i < devices.Length; i++)
        {
            if (devices[i].isFrontFacing == front)
                return i;
        }
        return -1;
    }

    private void StopCurrentCamera()
    {
        if (webCam != null)
        {
            if (webCam.isPlaying) webCam.Stop();
            Destroy(webCam);
            webCam = null;
        }
    }

    private IEnumerator StartCamera(int deviceIndex)
    {
        currentDeviceIndex = deviceIndex;

        // 指定カメラを起動させる（元の解像度指定を踏襲）
        webCam = new WebCamTexture(devices[deviceIndex].name, 640, 480);

        // RawImageのテクスチャにWebCamTextureのインスタンスを設定
        RawImage.texture = webCam;

        // いったんスケールと回転をリセット（切替時の累積反転を防止）
        var rt = RawImage.GetComponent<RectTransform>();
        rt.localScale = Vector3.one;
        rt.eulerAngles = Vector3.zero;

        // カメラ起動
        webCam.Play();

        // 表示用パラメータが入るまで1フレ待機（幅/高さ/回転角など）
        yield return null;

        if (webCam.videoVerticallyMirrored)
        {
            // 左右反転しているのを戻す（元実装を踏襲）
            Vector3 scaletmp = rt.localScale;
            scaletmp.y = -1;
            rt.localScale = scaletmp;
        }

        // 表示するRawImageを回転させる（元実装を踏襲）
        Vector3 angles = rt.eulerAngles;
        angles.z = -webCam.videoRotationAngle;
        rt.eulerAngles = angles;

        // サイズ調整（元実装を踏襲：RawImageのsizeDelta基準でスケーリング）
        float scaler;
        Vector2 sizetmp = rt.sizeDelta;
        if (webCam.width > webCam.height)
        {
            scaler = sizetmp.x / webCam.width;
        }
        else
        {
            scaler = sizetmp.y / webCam.height;
        }
        sizetmp.x = webCam.width * scaler;
        sizetmp.y = webCam.height * scaler;
        rt.sizeDelta = sizetmp;

        // Face Camera（前面）の場合の反転処理（元実装を踏襲、対象デバイスだけ参照を置換）
        if (devices[deviceIndex].isFrontFacing)
        {
            Vector3 scaletmp = rt.localScale;
            if ((webCam.videoRotationAngle == 90) || (webCam.videoRotationAngle == 270))
            {
                scaletmp.y *= -1;
            }
            else
            {
                scaletmp.x *= -1;
            }
            rt.localScale = scaletmp;
        }

        // 幅が requestedWidth になるまで待機（元実装を踏襲）
        while (webCam.width != webCam.requestedWidth)
        {
            yield return null;
        }
    }
}
