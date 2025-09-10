#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class InjectPlist
{
    [PostProcessBuild(999)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        var plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        var root = plist.root;

        root.SetString("NSCameraUsageDescription",
            "撮影画像を解析してゲーム判定を行うためにカメラを使用します。");

        // これが今回のクラッシュ原因のキー
        root.SetString("NSPhotoLibraryAddUsageDescription",
            "撮影した写真をフォトライブラリに保存します。");

        // もし将来読み取りもするなら↓も追加
        // root.SetString("NSPhotoLibraryUsageDescription", "フォトライブラリから画像を読み込みます。");

        File.WriteAllText(plistPath, plist.WriteToString());
    }
}
#endif
