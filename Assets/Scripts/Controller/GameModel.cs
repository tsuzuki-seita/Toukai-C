// Model/GameModel.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

public interface IGameModel : IDisposable {
    // View へ公開（描画のみ）
    IReadOnlyReactiveProperty<string> ScoreText { get; }
    IReadOnlyReactiveProperty<string> TimerText { get; }
    IReadOnlyReactiveProperty<string> EnemyHintText { get; }
    IReadOnlyReactiveProperty<bool> CaptureButtonInteractable { get; }
    IReadOnlyReactiveProperty<string> CameraFaceText { get; } // "Front"/"Back" 表示

    // Presenter が購読してシーン遷移だけ実行
    IObservable<string> NavigateToScene { get; }

    // Presenter はクリックを流すだけ
    ReactiveCommand<Unit> RequestCapture { get; }
    ReactiveCommand<Unit> ToggleCameraFace { get; }
}

public class GameModel : IGameModel {
    public IReadOnlyReactiveProperty<string> ScoreText => _scoreText;
    public IReadOnlyReactiveProperty<string> TimerText => _timerText;
    public IReadOnlyReactiveProperty<string> EnemyHintText => _hintText;
    public IReadOnlyReactiveProperty<bool> CaptureButtonInteractable => _captureInteractable;
    public IReadOnlyReactiveProperty<string> CameraFaceText => _cameraFaceText;
    public IObservable<string> NavigateToScene => _navigateToScene;
    public ReactiveCommand<Unit> RequestCapture { get; } = new ReactiveCommand<Unit>();
    public ReactiveCommand<Unit> ToggleCameraFace { get; } = new ReactiveCommand<Unit>();

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

    int score = 0;
    int currentWaveIndex = -1;
    float timeLeft;
    List<EnemyRequirement> requires = new List<EnemyRequirement>();
    bool busy = false;
    bool ended = false;
    bool useFront = false; // 内カメラ=false→外カメラ/true→内カメラ

    public GameModel(ICameraService cam, IPhotoSaver saver, IGeminiAnalyzer analyzer, WaveSetConfig waveSet) {
        this.camera = cam; this.saver = saver; this.analyzer = analyzer; this.waveSet = waveSet;

        // 撮影要求
        RequestCapture.Where(_ => !busy && !ended)
            .Do(_ => { busy = true; _captureInteractable.Value = false; })
            .SelectMany(_ => camera.CaptureOneJpg(useFront))
            .Do(t => saver.SaveJpg(t.jpg))
            .SelectMany(t => analyzer.Analyze(t.tex))
            .Select(analysis => Judge(analysis))
            .ObserveOnMainThread()
            .Subscribe(win => {
                busy = false; _captureInteractable.Value = true;
                if (win) { OnWaveCleared(); } else { EndGame(waveSet.gameOverSceneName); }
            }, err => {
                busy = false; _captureInteractable.Value = true;
                Debug.LogError(err);
                EndGame(waveSet.gameOverSceneName);
            }).AddTo(cd);

        // カメラ切替
        ToggleCameraFace.Subscribe(_ => {
            useFront = !useFront;
            _cameraFaceText.Value = useFront ? "Front" : "Back";
        }).AddTo(cd);

        // 開始
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
