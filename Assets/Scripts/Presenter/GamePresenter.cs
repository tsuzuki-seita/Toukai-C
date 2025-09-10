// Presenter/GamePresenter.cs
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GamePresenter : System.IDisposable {
    readonly CompositeDisposable cd = new CompositeDisposable();
    public GamePresenter(IGameView view, IGameModel model) {
        // 入力 → Model
        view.OnCaptureClicked.Subscribe(_ => model.RequestCapture.Execute(Unit.Default)).AddTo(cd);
        view.OnSwitchCameraClicked.Subscribe(_ => model.ToggleCameraFace.Execute(Unit.Default)).AddTo(cd);

        // 出力 → View
        model.ScoreText.Subscribe(view.SetScore).AddTo(cd);
        model.TimerText.Subscribe(view.SetTimer).AddTo(cd);
        model.EnemyHintText.Subscribe(view.SetHint).AddTo(cd);
        model.CaptureButtonInteractable.Subscribe(view.SetCaptureInteractable).AddTo(cd);
        model.CameraFaceText.Subscribe(view.SetCameraFace).AddTo(cd);

        // ナビゲーション（Modelがシーン名を発行 → Presenterが遷移を実行）
        model.NavigateToScene.Subscribe(scene => SceneManager.LoadScene(scene)).AddTo(cd);
    }
    public void Dispose(){ cd.Dispose(); }
}
