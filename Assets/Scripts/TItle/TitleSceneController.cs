using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class TitleSceneController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject titlePanel;     // TitlePanel（最初に表示）
    [SerializeField] private GameObject registerPanel;  // RegisterPanel（最初に非表示）

    [Header("Title Panel UI")]
    [SerializeField] private GameObject firstRegisterGroup; // ←「登録へ」一式の親(推奨)。未設定なら下のボタンを使用
    [SerializeField] private Button openRegisterButton;     // 「登録へ」ボタン（firstRegisterGroup未設定時に使用）
    [SerializeField] private TMP_Text membersListText;      // 任意（未割当OK）

    [Header("Register Panel UI")]
    [SerializeField] private TMP_InputField nameInput;  // 日本語用にTMP推奨
    [SerializeField] private TMP_Dropdown colorDropdown;
    [SerializeField] private Button registerButton;     // 「登録」
    [SerializeField] private Button cancelButton;       // 「キャンセル」
    [SerializeField] private TMP_Text errorText;        // 任意（未割当OK）

    [Header("Validation / Options")]
    [SerializeField, Min(1)] private int maxNameLength = 16;
    [SerializeField] private bool trimWhitespace = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;

    // 色候補
    public enum OutfitColor { Red, Blue, Green, Yellow, Black, White }

    [Serializable]
    public class MemberData
    {
        public string name;
        public OutfitColor color;
        public MemberData(string n, OutfitColor c) { name = n; color = c; }
    }

    [SerializeField] private List<MemberData> members = new List<MemberData>();
    public IReadOnlyList<MemberData> Members => members;

    private bool hasRegistered = false;

    // ===== ライフサイクル =====
    private void Awake()
    {
        // 初期表示（他の影響を受けにくくする）
        SafeSetActive(titlePanel, true);
        SafeSetActive(registerPanel, false);

        // 参照チェック
        if (!titlePanel || !registerPanel) LogWarn("titlePanel / registerPanel の参照を確認してください。");
        if (!nameInput) LogWarn("nameInput 未設定（TMP_InputField 推奨）");
        if (!colorDropdown) LogWarn("colorDropdown 未設定");
        if (!registerButton) LogWarn("registerButton 未設定");
        if (!cancelButton) LogWarn("cancelButton 未設定");
        if (!firstRegisterGroup && !openRegisterButton) LogWarn("firstRegisterGroup か openRegisterButton を設定してください。");

        // 同スクリプト多重アタッチ検出
        var all = FindObjectsOfType<TitleSceneController>(true);
        if (all.Length > 1) LogWarn($"TitleSceneController が {all.Length} 個あります（競合の恐れ）");

        // Dropdown初期化
        SetupDropdownOptions();

        // 入力欄設定（IME妨げない）
        if (nameInput)
        {
            nameInput.contentType = TMP_InputField.ContentType.Standard;
            nameInput.characterLimit = Mathf.Max(1, maxNameLength);
        }

        // クリックイベント
        if (openRegisterButton) openRegisterButton.onClick.AddListener(OpenRegister);
        if (registerButton) registerButton.onClick.AddListener(OnRegister);
        if (cancelButton) cancelButton.onClick.AddListener(BackToTitle);

        SetError("");
    }

    private void Start()
    {
        // 最終状態を確定
        ShowTitlePanel();
        RefreshMembersListText();
    }

    private void OnDestroy()
    {
        if (openRegisterButton) openRegisterButton.onClick.RemoveListener(OpenRegister);
        if (registerButton) registerButton.onClick.RemoveListener(OnRegister);
        if (cancelButton) cancelButton.onClick.RemoveListener(BackToTitle);
    }

    // ===== 初期化 =====
    private void SetupDropdownOptions()
    {
       
    }

    // ===== 画面切替 =====
    private void ShowTitlePanel()
    {
        SafeSetActive(titlePanel, true);
        SafeSetActive(registerPanel, false);

        // ★ いつでも「登録へ」を表示（何度でも登録可能）
        SetFirstRegisterUIActive(true);

        DebugState("ShowTitlePanel");
    }

    private void ShowRegisterPanel()
    {
        SafeSetActive(titlePanel, false);
        SafeSetActive(registerPanel, true);

        ResetRegisterInputs();

        if (nameInput)
        {
            nameInput.Select();
            nameInput.ActivateInputField(); // IMEフォーカス
        }

        DebugState("ShowRegisterPanel");
    }

    private void OpenRegister() => ShowRegisterPanel();

    private void BackToTitle()
    {
        SetError("");
        ShowTitlePanel();
    }

    // ===== 登録処理 =====
    private void OnRegister()
    {
        if (!nameInput) return;

        // IME未確定対策
        nameInput.DeactivateInputField();

        var raw = nameInput.text ?? "";
        string n = trimWhitespace ? raw.Trim() : raw;

        if (string.IsNullOrEmpty(n))
        {
            SetError("名前を入力してください。");
            nameInput.Select();
            nameInput.ActivateInputField();
            return;
        }

        OutfitColor selected = OutfitColor.Red;
        if (colorDropdown)
            selected = (OutfitColor)Mathf.Clamp(colorDropdown.value, 0, Enum.GetNames(typeof(OutfitColor)).Length - 1);

        // データを同一シーン内のリストに蓄積
        members.Add(new MemberData(n, selected));
        RefreshMembersListText();

        // ★ 以前はここで SetFirstRegisterUIActive(false) していたが削除
        SetError("");
        ShowTitlePanel(); // ← タイトルに戻る（“登録へ”は表示されたまま）
    }


    // ===== 表示更新・ユーティリティ =====
    private void RefreshMembersListText()
    {
        if (!membersListText) return;

        if (members.Count == 0)
        {
            membersListText.text = "登録なし";
            return;
        }

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            sb.AppendLine($"{i + 1}. {m.name} / {m.color}");
        }
        membersListText.text = sb.ToString();
    }

    private void ResetRegisterInputs()
    {
        if (nameInput) nameInput.text = "";
        if (colorDropdown)
        {
            colorDropdown.value = 0;
            colorDropdown.RefreshShownValue();
        }
    }

    private void SetError(string msg)
    {
        if (errorText) errorText.text = msg ?? "";
        if (!string.IsNullOrEmpty(msg)) DebugLog($"[ERROR] {msg}");
    }

    private void SetFirstRegisterUIActive(bool active)
    {
        if (firstRegisterGroup)
        {
            SafeSetActive(firstRegisterGroup, active);
            DebugLog($"Set firstRegisterGroup({firstRegisterGroup.name}) active={active}");
        }
        else if (openRegisterButton)
        {
            SafeSetActive(openRegisterButton.gameObject, active);
            DebugLog($"Set openRegisterButton({openRegisterButton.name}) active={active}");
        }
        else
        {
            LogWarn("firstRegisterGroup / openRegisterButton いずれも未設定です。");
        }
    }

    private static void SafeSetActive(GameObject go, bool active)
    {
        if (go && go.activeSelf != active) go.SetActive(active);
    }

    private void DebugState(string where)
    {
        if (!enableDebugLog) return;
        string grp = firstRegisterGroup ? firstRegisterGroup.activeSelf.ToString() : "null";
        string btn = openRegisterButton ? openRegisterButton.gameObject.activeSelf.ToString() : "null";
        Debug.Log($"[{where}] hasRegistered={hasRegistered}, firstGroup={grp}, openBtn={btn}, title={titlePanel?.activeSelf}, register={registerPanel?.activeSelf}", this);
    }

    private void DebugLog(string msg)
    {
        if (enableDebugLog) Debug.Log(msg, this);
    }

    private void LogWarn(string msg)
    {
        Debug.LogWarning(msg, this);
    }
}
