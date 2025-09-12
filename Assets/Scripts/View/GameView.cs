// View/GameView.cs
using System;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public interface IGameView {
    IObservable<Unit> OnCaptureClicked { get; }
    IObservable<Unit> OnSwitchCameraClicked { get; }

    void SetScore(string text);
    void SetTimer(string text);
    void SetHint(string text);
    void SetCaptureInteractable(bool on);
    void SetCameraFace(string text); // Front / Back
}

public class GameView : MonoBehaviour, IGameView {
    public Button captureStartButton;
    public Button captureButton;
    public Button switchCameraButton;
    public RawImage cameraPreview;
    public Text scoreText;
    public Text timerText;
    public Text hintText;
    public Text cameraFaceText;

    public IObservable<Unit> OnCaptureClicked => captureButton.OnClickAsObservable();
    public IObservable<Unit> OnSwitchCameraClicked => switchCameraButton.OnClickAsObservable();

    void Start()
    {
        captureStartButton.onClick.AddListener(() =>
        {
            captureStartButton.gameObject.SetActive(false);
            cameraPreview.enabled = true;
            captureButton.gameObject.SetActive(true);
            switchCameraButton.gameObject.SetActive(true);
        });
    }

    public void SetScore(string t) { if (scoreText) scoreText.text = t; }
    public void SetTimer(string t){ if (timerText) timerText.text = t; }
    public void SetHint(string t){ if (hintText)  hintText.text  = t; }
    public void SetCaptureInteractable(bool on){ if (captureButton) captureButton.interactable = on; }
    public void SetCameraFace(string t){ if (cameraFaceText) cameraFaceText.text = $"Camera: {t}"; }
}
