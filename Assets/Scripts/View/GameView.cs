// View/GameView.cs 〈差し替え/拡張〉
using System;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public interface IGameView
{
    // 入力
    IObservable<Unit> OnStartPreviewClicked { get; }   // 撮影開始
    IObservable<Unit> OnCaptureClicked { get; }        // 撮影
    IObservable<Unit> OnSwitchCameraClicked { get; }   // 内/外

    // 表示
    void SetScore(string text);
    void SetTimer(string text);
    void SetHint(string text);
    void SetCaptureInteractable(bool on);
    void SetCameraFace(string text);

    // プレビュー
    void ShowPreview(CameraPreviewInfo info);
    void SetPreviewVisible(bool visible);

    // 攻撃演出：Modelが結果を出した後に呼ばれる
    // Presenterがこれを呼ぶ→内部で演出→完了時に OnAttackAnimationFinished を発火
    void PlayAttackAndNotify(float durationSec = 0.6f);

    // View→Presenterに“演出完了”を知らせる
    IObservable<Unit> OnAttackAnimationFinished { get; }
}

public class GameView : MonoBehaviour, IGameView 
{
    // UI
    public Button startPreviewButton;
    public Button captureButton;
    public Button switchCameraButton;
    public Text scoreText, timerText, hintText, cameraFaceText;

    // プレビュー
    public RawImage preview;        // これにWebCamTextureを貼る
    public RectTransform previewRoot; // 回転させたいならRawImageではなく親を回すのがおすすめ

    Subject<Unit> attackFinished = new Subject<Unit>();

    public IObservable<Unit> OnStartPreviewClicked => startPreviewButton.OnClickAsObservable();
    public IObservable<Unit> OnCaptureClicked => captureButton.OnClickAsObservable();
    public IObservable<Unit> OnSwitchCameraClicked => switchCameraButton.OnClickAsObservable();
    public IObservable<Unit> OnAttackAnimationFinished => attackFinished;

    void Awake() {
        if (preview) preview.gameObject.SetActive(false);
    }

    public void SetScore(string t){ if (scoreText) scoreText.text = t; }
    public void SetTimer(string t){ if (timerText) timerText.text = t; }
    public void SetHint(string t){ if (hintText)  hintText.text  = t; }
    public void SetCaptureInteractable(bool on){ if (captureButton) captureButton.interactable = on; }
    public void SetCameraFace(string t){ if (cameraFaceText) cameraFaceText.text = $"Camera: {t}"; }

    public void ShowPreview(CameraPreviewInfo info) {
        if (!preview) return;
        preview.texture = info.cam;

        // 映像の回転補正（UI側）。縦横比を崩さないため、親RectTransformを回すのが楽。
        if (previewRoot) previewRoot.localEulerAngles = new Vector3(0, 0, -info.rotation);
        else             preview.rectTransform.localEulerAngles = new Vector3(0, 0, -info.rotation);

        // 上下反転（videoVerticallyMirrored）
        var uv = preview.uvRect;
        uv.y = info.vMirror ? 1 : 0;
        uv.height = info.vMirror ? -1 : 1;

        // 内カメラは“鏡像プレビュー”に（自然な自撮り体験）
        uv.x = info.hMirror ? 1 : 0;
        uv.width = info.hMirror ? -1 : 1;

        preview.uvRect = uv;
    }

    public void SetPreviewVisible(bool visible) {
        if (preview) preview.gameObject.SetActive(visible);
    }

    public void PlayAttackAndNotify(float durationSec = 0.6f) {
        // ここに後で演出を差し込める（パーティクル/アニメ/SE等）
        // いまはプレースホルダとして一定時間後に完了通知
        StartCoroutine(CoAttack(durationSec));
    }

    System.Collections.IEnumerator CoAttack(float sec) {
        // （例）フェードやエフェクトの再生…
        yield return new WaitForSeconds(sec);
        attackFinished.OnNext(Unit.Default);
    }
}
