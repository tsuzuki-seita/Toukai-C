// Model/GameModel.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

public interface IGameModel : IDisposable 
{
    // 表示系
    IReadOnlyReactiveProperty<string> ScoreText { get; }
    IReadOnlyReactiveProperty<string> TimerText { get; }
    IReadOnlyReactiveProperty<string> EnemyHintText { get; }
    IReadOnlyReactiveProperty<bool> CaptureButtonInteractable { get; }
    IReadOnlyReactiveProperty<string> CameraFaceText { get; }
    IReadOnlyReactiveProperty<bool> PreviewVisible { get; }

    // プレビュー開始時に View がテクスチャと回転/反転を受け取る
    IObservable<CameraPreviewInfo> PreviewStarted { get; }

    // シーン遷移（Model→Presenter）
    IObservable<string> NavigateToScene { get; }

    // ボタンなど（Presenter→Model）
    ReactiveCommand RequestStartPreview { get; }   // 「撮影開始」ボタン
    ReactiveCommand RequestCapture { get; }        // 「撮影」ボタン
    ReactiveCommand ToggleCameraFace { get; }      // 内/外トグル
    ReactiveCommand AttackAnimationFinished { get; } // 攻撃演出が終わったらView→Presenter→Model
}

public class GameModel : IGameModel 
{
    public IReadOnlyReactiveProperty<string> ScoreText => _scoreText;
    public IReadOnlyReactiveProperty<string> TimerText => _timerText;
    public IReadOnlyReactiveProperty<string> EnemyHintText => _hintText;
    public IReadOnlyReactiveProperty<bool> CaptureButtonInteractable => _captureInteractable;
    public IReadOnlyReactiveProperty<string> CameraFaceText => _cameraFaceText;
    public IReadOnlyReactiveProperty<bool> PreviewVisible => _previewVisible;
    public IObservable<CameraPreviewInfo> PreviewStarted => _previewStarted;
    public IObservable<string> NavigateToScene => _navigateToScene;

    public ReactiveCommand RequestCapture { get; } = new ReactiveCommand();
    public ReactiveCommand ToggleCameraFace { get; } = new ReactiveCommand();
    public ReactiveCommand RequestStartPreview { get; } = new ReactiveCommand();
    public ReactiveCommand AttackAnimationFinished { get; } = new ReactiveCommand();

    readonly ICameraService camera;
    readonly IPhotoSaver saver;
    readonly IGeminiAnalyzer analyzer;
    readonly WaveSetConfig waveSet;

    readonly CompositeDisposable cd = new CompositeDisposable();
    readonly CompositeDisposable roundCd = new CompositeDisposable();

    readonly ReactiveProperty<string> _scoreText = new ReactiveProperty<string>("Score: 0");
    readonly ReactiveProperty<string> _timerText = new ReactiveProperty<string>("Time: 60");
    readonly ReactiveProperty<string> _hintText  = new ReactiveProperty<string>("");
    readonly ReactiveProperty<bool> _captureInteractable = new ReactiveProperty<bool>(true);
    readonly ReactiveProperty<string> _cameraFaceText = new ReactiveProperty<string>("Back");
    readonly Subject<string> _navigateToScene = new Subject<string>();
    readonly ReactiveProperty<bool> _previewVisible = new ReactiveProperty<bool>(false);
    readonly Subject<CameraPreviewInfo> _previewStarted = new Subject<CameraPreviewInfo>();

    bool useFront = false;
    bool? lastWinPending = null; // 攻撃演出の完了待ちに使う

    int score = 0;
    int currentWaveIndex = -1;
    float timeLeft;
    List<EnemyRequirement> requires = new List<EnemyRequirement>();
    bool busy = false;
    bool ended = false;

    public GameModel(ICameraService cam, IPhotoSaver saver, IGeminiAnalyzer analyzer, WaveSetConfig waveSet) {
        this.camera = cam; this.saver = saver; this.analyzer = analyzer; this.waveSet = waveSet;

        // 撮影開始（プレビューON）
        RequestStartPreview
            .Where(_ => !ended && !camera.IsPreviewRunning)
            .SelectMany(_ => camera.StartPreview(useFront))
            .ObserveOnMainThread()
            .Subscribe(info => {
                _previewVisible.Value = true;
                _previewStarted.OnNext(info); // View はここで RawImage に貼る＆回転/反転を適用
            }, err => {
                Debug.LogError(err);
            }).AddTo(cd);

        // 撮影（プレビューから1枚）
        RequestCapture
            .Where(_ => !ended && camera.IsPreviewRunning)
            .Do(_ => { busy = true; _captureInteractable.Value = false; _previewVisible.Value = false; }) // RawImageを消す（攻撃演出の裏で判定）
            .SelectMany(_ => camera.CaptureOneFromPreviewUpright(unMirrorFront:true))
            .Do(t => saver.SaveJpg(t.jpg)) // iOSアルバムへ保存（向き補正後）
            .SelectMany(t => analyzer.Analyze(t.tex))
            .Select(analysis => Judge(analysis)) // true=勝ち/false=負け
            .ObserveOnMainThread()
            .Subscribe(win => {
                // ここでは遷移せず、まず“攻撃演出の完了”を待つ
                lastWinPending = win;
                // Viewに任せる：Presenter経由で攻撃演出をかけ、終わったら AttackAnimationFinished を押してもらう
            }, err => {
                Debug.LogError(err);
                lastWinPending = false; // エラーは敗北扱いで演出後に遷移
            }).AddTo(cd);

        // 攻撃演出が終わったら結果に従って遷移/次ウェーブ
        AttackAnimationFinished
            .Where(_ => lastWinPending.HasValue)
            .Subscribe(_ => {
                busy = false; _captureInteractable.Value = true;
                if (lastWinPending.Value) {
                    OnWaveCleared();               // 勝ち → 次の敵（クリアならNavigate）
                } else {
                    EndGame(waveSet.gameOverSceneName); // 負け → 遷移
                }
                lastWinPending = null;
            }).AddTo(cd);

        // カメラ切替（プレビュー中ならいったん再起動）
        ToggleCameraFace.Subscribe(_ => {
            useFront = !useFront;
            _cameraFaceText.Value = useFront ? "Front" : "Back";
            if (camera.IsPreviewRunning) {
                camera.StopPreview();
                RequestStartPreview.Execute(); // すぐ再開
            }
        }).AddTo(cd);

        // 開始：最初のウェーブ出現
        NextWave();
    }

    void NextWave() {
        if (ended) return;
        roundCd.Clear();

        currentWaveIndex++;
        if (waveSet.waves == null || currentWaveIndex >= waveSet.waves.Count) {
            // すべて倒した → クリア
            EndGame(waveSet.gameClearSceneName);
            return;
        }

        var wave = waveSet.waves[currentWaveIndex];
        requires = wave.enemies != null ? wave.enemies : new List<EnemyRequirement>();
        timeLeft = Mathf.Max(1f, wave.timeLimitSec);
        _timerText.Value = $"Time: {Mathf.CeilToInt(timeLeft)}";
        _hintText.Value = ToHintText(requires);

        // タイマー
        Observable.EveryUpdate()
            .Subscribe(_ => {
                if (ended) return;
                timeLeft -= Time.deltaTime;
                _timerText.Value = $"Time: {Mathf.CeilToInt(Mathf.Max(0,timeLeft))}";
                if (timeLeft <= 0f) {
                    EndGame(waveSet.gameOverSceneName);
                }
            }).AddTo(roundCd);
    }

    void OnWaveCleared() {
        // このウェーブの敵数をスコアに加算
        int gained = 0; if (requires != null) foreach (var r in requires) gained += Mathf.Max(0, r.count);
        score += gained;
        _scoreText.Value = $"Score: {score}";
        NextWave();
    }

    void EndGame(string sceneName) {
        if (ended) return;
        ended = true;
        roundCd.Clear();
        _captureInteractable.Value = false;
        _navigateToScene.OnNext(sceneName);
        _navigateToScene.OnCompleted();
    }

    // 判定（Modelに集約）
    bool Judge(PhotoAnalysis result) {
        var buckets = new Dictionary<(ShirtColor, Emotion), int>();
        foreach (var p in result.people) {
            var key = (p.color, p.emotion);
            buckets.TryGetValue(key, out var v); buckets[key] = v + 1;
        }
        foreach (var r in requires) {
            buckets.TryGetValue((r.color, r.emotion), out var have);
            if (have < r.count) return false;
        }
        return true;
    }

    string ToHintText(List<EnemyRequirement> rs) {
        if (rs == null || rs.Count == 0) return "Target: (none)";
        return "Target: " + string.Join(", ", rs.Select(r => $"{r.emotion}×{r.color}×{r.count}"));
    }

    public void Dispose() { cd.Dispose(); roundCd.Dispose(); }
}
