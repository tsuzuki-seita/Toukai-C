// WaveSetConfig.cs
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName="WaveSetConfig", menuName="Game/Wave Set Config")]
public class WaveSetConfig : ScriptableObject {
    public string gameClearSceneName = "GameClear";
    public string gameOverSceneName  = "GameOver";
    public List<Wave> waves;
}
