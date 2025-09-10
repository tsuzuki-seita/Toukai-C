// Boot/GameEntry.cs
using UnityEngine;

public class GameEntry : MonoBehaviour {
    public GameView view;
    public WaveSetConfig waveSet;

    [Header("Gemini")]
    public string geminiApiKey;
    public string model = "gemini-2.0-flash";

    GamePresenter presenter;
    GameModel gameModel;

    void Start() {
        var cameraSvc = new WebCamCameraService();
        var saver     = new IOSPhotoSaver();
        var analyzer  = new GeminiAnalyzer(geminiApiKey, model);

        gameModel = new GameModel(cameraSvc, saver, analyzer, waveSet);
        presenter = new GamePresenter(view, gameModel);
    }

    void OnDestroy() { presenter?.Dispose(); gameModel?.Dispose(); }
}
