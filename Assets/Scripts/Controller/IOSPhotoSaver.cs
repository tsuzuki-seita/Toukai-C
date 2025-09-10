// Services/PhotoSaver.cs
using System.Runtime.InteropServices;
using UnityEngine;

public interface IPhotoSaver { void SaveJpg(byte[] jpg); }

public class IOSPhotoSaver : IPhotoSaver {
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void SaveJPGToCameraRoll(byte[] data, int length);
#endif
    public void SaveJpg(byte[] jpg) {
#if UNITY_IOS && !UNITY_EDITOR
        SaveJPGToCameraRoll(jpg, jpg.Length);
#else
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.persistentDataPath,"last_capture.jpg"), jpg);
#endif
    }
}
