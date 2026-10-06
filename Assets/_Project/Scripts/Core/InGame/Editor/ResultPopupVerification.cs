using System;
using OZGL2.UIFlow;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZGL2.InGame.Editor
{
    public static class ResultPopupVerification
    {
        public static string Result { get; private set; }

        [MenuItem("OZGL2/InGame/Verify Result Popup Lifecycle")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                Result = "NOT RUN: Play Mode is required for Selectable focus behavior.";
                return;
            }
            GameObject root = null;
            EventSystem previousSystem = EventSystem.current;
            GameObject previousSelection = previousSystem != null ? previousSystem.currentSelectedGameObject : null;
            try
            {
                root = new GameObject("ResultPopupVerification");
                var events = previousSystem;
                if (events == null)
                {
                    events = new GameObject("Events").AddComponent<EventSystem>();
                    events.transform.SetParent(root.transform);
                    EventSystem.current = events;
                }
                var screen = new GameObject("Screen").AddComponent<CanvasGroup>();
                screen.transform.SetParent(root.transform);
                var button = new GameObject("BaseButton", typeof(RectTransform), typeof(Button));
                button.transform.SetParent(screen.transform);
                var controller = root.AddComponent<UIPopupController>();
                var data = new SerializedObject(controller);
                data.FindProperty("_popupRoot").objectReferenceValue = root.transform;
                data.FindProperty("_screenGroup").objectReferenceValue = screen;
                data.ApplyModifiedPropertiesWithoutUndo();
                var result = CreatePopup(root.transform, "Result");
                var menu = CreatePopup(root.transform, "Menu");
                var settings = CreatePopup(root.transform, "Settings");
                int notifications = 0;
                controller.OpenCountChanged += _ => notifications++;

                events.SetSelectedGameObject(button);
                controller.OpenPopup(result);
                controller.OpenPopup(menu);
                controller.OpenPopup(settings);
                var selected = events.currentSelectedGameObject;
                Check(controller.CloseResolvedPopup(result), "Remove result under two popups");
                Check(!result.gameObject.activeSelf && controller.OpenCount == 2 && controller.IsTopPopup(settings), "Only result removed");
                Check(!screen.interactable && events.currentSelectedGameObject == selected, "Keep modal input and focus");
                int count = notifications;
                Check(!controller.CloseResolvedPopup(result) && notifications == count, "Duplicate close is a no-op");
                controller.CloseConfirmedPopup();
                Check(events.currentSelectedGameObject == menu.FirstSelected.gameObject, "Settings returns to menu");
                controller.CloseConfirmedPopup();
                Check(controller.OpenCount == 0 && screen.interactable && events.currentSelectedGameObject == button, "Menu returns to base after result removal");

                controller.OpenPopup(result);
                result.SetDismissGuard(() => false);
                controller.CloseTopPopup();
                Check(controller.OpenCount == 1, "Dismiss guard retained");
                Check(controller.CloseResolvedPopup(result) && controller.OpenCount == 0, "Owner resolution bypasses dismiss guard");
                Check(screen.interactable && events.currentSelectedGameObject == button, "Top result restores input and focus");
                controller.OpenPopup(menu);
                controller.CloseTopPopup();
                Check(controller.OpenCount == 0, "Ordinary menu dismissal unchanged");
                Result = "PASS: nested result removal, focus chain, input blocking, duplicate close, dismiss guard, ordinary menu close";
            }
            catch (Exception e) { Result = "FAIL: " + e; }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (previousSystem != null && previousSystem.isActiveAndEnabled)
                {
                    EventSystem.current = previousSystem;
                    previousSystem.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy
                        ? previousSelection : null);
                }
            }
        }

        private static UIPopupPanel CreatePopup(Transform root, string name)
        {
            var panel = new GameObject(name, typeof(RectTransform)).AddComponent<UIPopupPanel>();
            panel.transform.SetParent(root);
            var button = new GameObject("First", typeof(RectTransform)).AddComponent<Button>();
            button.transform.SetParent(panel.transform);
            var data = new SerializedObject(panel);
            data.FindProperty("_firstSelected").objectReferenceValue = button;
            data.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            return panel;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
