// Presenter/GamePresenter.cs 〈差し替え〉
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GamePresenter : System.IDisposable 
{
    readonly CompositeDisposable cd = new CompositeDisposable();

    public GamePresenter(IGameView view, IGameModel model) {
        // View → Model
        view.OnStartPreviewClicked.Subscribe(_ => model.RequestStartPreview.Execute()).AddTo(cd);
        view.OnCaptureClicked.Subscribe(_ => model.RequestCapture.Execute()).AddTo(cd);
        view.OnSwitchCameraClicked.Subscribe(_ => model.ToggleCameraFace.Execute()).AddTo(cd);
        view.OnAttackAnimationFinished.Subscribe(_ => model.AttackAnimationFinished.Execute()).AddTo(cd);

        // Model → View
        model.ScoreText.Subscribe(view.SetScore).AddTo(cd);
        model.TimerText.Subscribe(view.SetTimer).AddTo(cd);
        model.EnemyHintText.Subscribe(view.SetHint).AddTo(cd);
        model.CaptureButtonInteractable.Subscribe(view.SetCaptureInteractable).AddTo(cd);
        model.CameraFaceText.Subscribe(view.SetCameraFace).AddTo(cd);
        model.PreviewStarted.Subscribe(info => view.ShowPreview(info)).AddTo(cd);
        model.PreviewVisible.Subscribe(visible => view.SetPreviewVisible(visible)).AddTo(cd);

        // 攻撃演出：Model側で“結果は出ているが、演出後に処理したい”ので、ここで再生だけ呼ぶ
        // （トリガーはModelのPreviewVisibleが false になったタイミングで十分）
        model.PreviewVisible.Pairwise()
            .Where(p => p.Previous && !p.Current) // true→false（撮影直後にRawImageを隠した瞬間）
            .Subscribe(_ => view.PlayAttackAndNotify())
            .AddTo(cd);

        // ナビゲーション
        model.NavigateToScene.Subscribe(scene => SceneManager.LoadScene(scene)).AddTo(cd);
    }

    public void Dispose(){ cd.Dispose(); }
}
