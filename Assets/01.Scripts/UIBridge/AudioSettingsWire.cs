using System.Reflection;
using UnityEngine.SceneManagement;
using OZGL2.InGame;
using UnityEngine;
using UnityEngine.Events;
using OZGL2.Progression;
using TMPro;
using UnityEngine.UI;

namespace OZGL2.UIBridge
{
    /// <summary>
    /// 로비 설정 창(UILobbyOverlayView)과 전투 메뉴(UIBattleMenuPopupView)의 「배경음악」「효과음」 토글, 「게임 종료」를 실제로 연결한다.
    /// 두 창은 "오디오 저장·종료는 외부 시스템이 UnityEvent 에 연결한다"는 규칙이라 이벤트만 비어 있다 — 팀 UI·프리팹은 수정하지 않고
    /// 실행 중에 리스너를 달고, 저장된 설정을 토글에 먼저 반영한다.
    /// </summary>
    public sealed class AudioSettingsWire : MonoBehaviour
    {
        [SerializeField] private TitleConfirmationView _giveUpDialogPrefab;

        private const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private bool _wired;
        private float _next;
        private static TMP_Text _giveUpLabel;
        private static Button _quitTemplate;
        private static GameObject _giveUpDialog;
        private const string GiveUpText = "포기";

        private void Update()
        {
            if (_wired || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            _wired = Wire(_giveUpDialogPrefab);
        }

        private static bool Wire(TitleConfirmationView giveUpDialogPrefab)
        {
            bool any = false;
            foreach (var view in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (view == null) continue;
                string name = view.GetType().Name;
                if (name != "UILobbyOverlayView" && name != "UIBattleMenuPopupView") continue;
                var type = view.GetType();
                if (type.GetField("_bgmEnabledChanged", Priv)?.GetValue(view) is UnityEvent<bool> bgm) bgm.AddListener(OnBgm);
                if (type.GetField("_sfxEnabledChanged", Priv)?.GetValue(view) is UnityEvent<bool> sfx) sfx.AddListener(GameAudioSettings.SetSfx);
                // 로비의 「게임 종료」는 앱을 끄지만, 전투 메뉴의 나가기(메인 로비로)는 로비 화면으로 돌아간다
                if (type.GetField("_quitRequested", Priv)?.GetValue(view) is UnityEvent quit && name == "UIBattleMenuPopupView")
                {
                    // 인게임 씬에는 이 단추에 「진행 중단 → 로비 씬 열기」가 이미 연결돼 있어서, 누르는 즉시 로비로 가 버렸다(경고창이 뜨기도 전에).
                    // 포기는 경고창에서 확인한 뒤에만 실행해야 하므로 그 두 연결을 끈다.
                    for (int i = 0; i < quit.GetPersistentEventCount(); i++) quit.SetPersistentListenerState(i, UnityEventCallState.Off);
                }
                if (type.GetField("_quitRequested", Priv)?.GetValue(view) is UnityEvent quit2) quit2.AddListener(name == "UIBattleMenuPopupView" ? (UnityAction)(() => ShowGiveUpDialog(view, giveUpDialogPrefab)) : Quit);
                // 「게임 종료」·「나가기」 단추는 팀 UI에서 비활성(interactable 꺼짐)으로 올라와 있어서, 이벤트를 연결해도 눌러지지 않았다. 켜 준다.
                foreach (var button in view.GetComponentsInChildren<Button>(true))
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                        if (button.onClick.GetPersistentMethodName(i) == "RequestQuit")
                        {
                            button.interactable = true;
                            if (name == "UIBattleMenuPopupView")
                            {
                                _quitTemplate = button;                                          // 경고창의 단추를 이 단추 모양 그대로 복제해서 만든다
                                _giveUpLabel = button.GetComponentInChildren<TMP_Text>(true);   // 전투 메뉴의 「메인로비로」 단추는 「포기」가 된다
                                if (_giveUpLabel != null) _giveUpLabel.text = GiveUpText;
                            }
                        }
                // 저장된 설정을 토글 모양에 먼저 반영한다(바뀐 경우에만 이벤트가 나가므로 같은 값이면 아무 일도 없다)
                type.GetMethod("SetBgmEnabled", Priv)?.Invoke(view, new object[] { GameAudioSettings.BgmEnabled });
                type.GetMethod("SetSfxEnabled", Priv)?.Invoke(view, new object[] { GameAudioSettings.SfxEnabled });
                AudioVolumeSliderUi.AttachTo(view.transform); // 설정 패널이면 「배경음악」「효과음」 줄 아래에 음량 슬라이더를 붙인다
                any = true;
            }
            return any;
        }

        private static void OnBgm(bool enabled)
        {
            GameAudioSettings.SetBgm(enabled);
            // 전투 중에 켰다면 전투 BGM 을 다시 시작한다(타이틀·로비는 SceneBgm 이 알아서 다시 튼다)
            if (enabled && FindFirstObjectByType<InGamePrototypeBootstrap>() != null) Sfx.PlayBattleBgm();
        }

        /// <summary>
        /// 「포기」를 누르면 먼저 경고창을 띄운다 — 포기하면 로비로 돌아간다는 것과 무엇이 남고 무엇이 사라지는지 알려 주고,
        /// 창 안의 「포기」를 한 번 더 눌러야 실제로 포기한다(「취소」를 누르면 닫힌다). 편집용 원본이 없으면 전투 메뉴 단추를 복제한다.
        /// </summary>
        private static void ShowGiveUpDialog(MonoBehaviour view, TitleConfirmationView prefab)
        {
            if (_giveUpDialog == null) _giveUpDialog = BuildGiveUpDialog(view, prefab);
            if (_giveUpDialog == null) { ExecuteGiveUp(); return; } // 창을 못 만들 때만(템플릿 단추 없음) 바로 실행
            _giveUpDialog.transform.SetAsLastSibling();
            _giveUpDialog.SetActive(true);
            // 비활성 원본에서 추가된 Canvas는 활성화한 뒤 정렬을 설정해야 유지된다.
            if (_giveUpDialog.TryGetComponent<Canvas>(out var overlay))
            {
                overlay.overrideSorting = true;
                overlay.sortingOrder = 5000;
            }
        }

        private static GameObject BuildGiveUpDialog(MonoBehaviour view, TitleConfirmationView prefab)
        {
            var canvas = view.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            canvas = canvas.rootCanvas;

            if (prefab != null)
            {
                GameObject dialog = BuildPrefabGiveUpDialog(view, canvas, prefab);
                if (dialog != null) return dialog;
            }
            if (_quitTemplate == null) return null;

            var root = new GameObject("GiveUpDialog", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image), typeof(GiveUpDialogGuard));
            root.transform.SetParent(canvas.transform, false);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one; rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            var overlay = root.GetComponent<Canvas>();
            overlay.overrideSorting = true; overlay.sortingOrder = 5000;          // 전투 메뉴·다른 UI 위에 항상 보이게
            var dim = root.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.62f); dim.raycastTarget = true;   // 뒤쪽 단추가 눌리지 않게 막는다
            root.GetComponent<GiveUpDialogGuard>().View = view;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
            panel.transform.SetParent(root.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 420f);
            panel.GetComponent<Image>().color = new Color(0.09f, 0.065f, 0.11f, 0.98f);
            var border = panel.GetComponent<Outline>();
            border.effectColor = new Color(0.79f, 0.64f, 0.29f, 1f); border.effectDistance = new Vector2(3f, -3f);

            var font = _giveUpLabel != null ? _giveUpLabel.font : null;
            Text("Title", panelRect, "정말 포기하시겠어요?", 46f, new Color(1f, 0.82f, 0.48f), font, new Vector2(0f, 135f), new Vector2(700f, 70f), FontStyles.Bold);
            Text("Body", panelRect,
                "포기하면 <b>로비로 돌아갑니다.</b>\n<size=80%><color=#CDBFA8>마왕 레벨과 지금까지의 웨이브 기록은 그대로 남지만,\n진행 중인 배치와 고른 증강은 초기화됩니다.</color></size>",
                32f, Color.white, font, new Vector2(0f, 20f), new Vector2(700f, 150f), FontStyles.Normal);

            Clone("ConfirmButton", panelRect, GiveUpText, new Vector2(-150f, -140f), ExecuteGiveUp);
            Clone("CancelButton", panelRect, "취소", new Vector2(150f, -140f), () => root.SetActive(false));
            root.SetActive(false);
            return root;
        }

        private static GameObject BuildPrefabGiveUpDialog(MonoBehaviour view, Canvas canvas, TitleConfirmationView prefab)
        {
            GameObject root = null;
            try
            {
                var confirmation = Instantiate(prefab, canvas.transform, false);
                root = confirmation.gameObject;
                root.name = "GiveUpDialog";
                root.SetActive(false);

                var rootRect = (RectTransform)root.transform;
                rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
                if (!root.TryGetComponent<Canvas>(out var overlay)) overlay = root.AddComponent<Canvas>();
                overlay.overrideSorting = true; overlay.sortingOrder = 5000;
                if (!root.TryGetComponent<GraphicRaycaster>(out _)) root.AddComponent<GraphicRaycaster>();
                if (!root.TryGetComponent<GiveUpDialogGuard>(out var guard)) guard = root.AddComponent<GiveUpDialogGuard>();
                guard.View = view;

                // 표시는 원본 프리팹이 소유하고, 실제 포기 처리는 기존 콜백을 그대로 사용한다.
                if (!confirmation.Bind(ExecuteGiveUp, () => root.SetActive(false)))
                    throw new System.InvalidOperationException("확인/취소 버튼 연결이 누락되었습니다.");
                return root;
            }
            catch (System.Exception exception)
            {
                if (root != null)
                {
                    root.SetActive(false);
                    Destroy(root);
                }
                Debug.LogWarning("포기 확인창 원본 연결을 확인하세요. 기존 확인창을 사용합니다: " + exception.Message, prefab);
                return null;
            }
        }

        private static void Text(string name, RectTransform parent, string text, float size, Color color, TMP_FontAsset font, Vector2 pos, Vector2 box, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos; rect.sizeDelta = box;
            var label = go.GetComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text; label.fontSize = size; label.color = color; label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; label.richText = true;
            label.textWrappingMode = TextWrappingModes.Normal;
        }

        private static void Clone(string name, RectTransform parent, string text, Vector2 pos, UnityAction onClick)
        {
            var clone = Instantiate(_quitTemplate.gameObject, parent);
            clone.name = name;
            clone.SetActive(true);
            var rect = (RectTransform)clone.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos; rect.sizeDelta = new Vector2(260f, 78f); rect.localScale = Vector3.one;
            var button = clone.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent(); // 복제되면서 따라온 「게임 종료」 연결을 끊는다
            button.onClick.AddListener(onClick);
            button.interactable = true;
            // 원래 단추는 왼쪽 성 모양 아이콘 때문에 글자가 오른쪽으로 밀려 있다 — 아이콘을 없애고 글자를 단추 한가운데에 둔다
            foreach (var child in clone.GetComponentsInChildren<Transform>(true))
                if (child != clone.transform && child.name == "MenuIcon") child.gameObject.SetActive(false);
            var label = clone.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = text;
                label.alignment = TextAlignmentOptions.Center;
                var labelRect = label.rectTransform;
                labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            }
        }

        /// <summary>경고창이 떠 있는 동안 전투 메뉴가 닫히면(예: 일시정지 해제) 경고창도 같이 닫는다.</summary>
        private sealed class GiveUpDialogGuard : MonoBehaviour
        {
            public MonoBehaviour View;
            private void Update() { if (View == null || !View.isActiveAndEnabled) gameObject.SetActive(false); }
        }

        /// <summary>
        /// 포기 실행: 이번 판을 접고 로비로 돌아간다. 마왕 레벨·경험치는 그대로 두고, 지금까지 깬 웨이브는 최고 기록으로 남긴다.
        /// 이어하기 저장은 지운다 — 다음에 도전하면 1웨이브부터 새로 시작하고 증강도 초기화된다.
        /// </summary>
        private static void ExecuteGiveUp()
        {
            var bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            var progress = bootstrap != null && bootstrap.Stage != null ? bootstrap.Stage.Progress : null;
            string stageId = !string.IsNullOrEmpty(bootstrap?.SelectedStageId) ? bootstrap.SelectedStageId : progress?.StageId;
            Debug.Log("[포기] 실행: 스테이지=" + stageId + " 깬 웨이브=" + (progress != null ? progress.ClearedRoundCount : -1));

            if (progress != null) StageRecordStore.Submit(stageId, progress.ClearedRoundCount, false, 0f); // 깬 웨이브는 기록으로 남는다
            if (!string.IsNullOrEmpty(stageId)) RunSaveStore.Clear(stageId);                              // 이어하기 저장은 지운다(레벨은 별도 저장이라 그대로)
            InGameRunSaver.SuppressSave = true;                                                          // 나가는 순간의 배치가 다시 저장되지 않게
            ReturnToLobby();
        }

        /// <summary>전투 메뉴에서 「메인 로비로」: 진행 중인 판을 멈추고 로비 씬으로 간다. 저장은 계속 남아 있어 다음에 이어할 수 있다.</summary>
        private static void ReturnToLobby()
        {
            var bootstrap = FindFirstObjectByType<InGamePrototypeBootstrap>();
            string lobbyPath = bootstrap != null && bootstrap.Config != null ? bootstrap.Config.LobbyScenePath : null;
            if (string.IsNullOrEmpty(lobbyPath)) lobbyPath = "Assets/00.Scenes/Builds/Lobby.unity";
            FindFirstObjectByType<InGameRunSaver>()?.FlushNow(); // 나가기 직전 배치까지 저장
            Time.timeScale = 1f;
            bootstrap?.CancelRun();
            if (Application.CanStreamedLevelBeLoaded(lobbyPath)) SceneManager.LoadScene(lobbyPath, LoadSceneMode.Single);
            else SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
        }

        private static void Quit()
        {
            PlayerPrefs.Save(); // 이어하기·설정 저장을 확실히 디스크에 남기고 끈다
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
