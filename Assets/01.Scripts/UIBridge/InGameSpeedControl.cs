using UnityEngine;

namespace OZGL2.UIBridge
{
    /// <summary>배속 디버그 — 에디터·개발 빌드에서만 보이는 작은 버튼 줄(II / 1x / 2x / 4x / 8x).</summary>
    public sealed class InGameSpeedControl : MonoBehaviour
    {
        private static readonly float[] Speeds = { 0f, 1f, 2f, 4f, 8f };
        private static readonly string[] Labels = { "II", "1x", "2x", "4x", "8x" };
        private float _speed = 1f;

        private void Awake()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) enabled = false;
        }

        private void OnDestroy() => Time.timeScale = 1f; // 씬을 떠날 때 배속이 남지 않게

        private void OnGUI()
        {
            const float buttonW = 44f, buttonH = 28f, gap = 4f;
            float total = Speeds.Length * buttonW + (Speeds.Length - 1) * gap;
            float x = (Screen.width - total) / 2f;
            float y = Screen.height * 0.105f; // 상단 HUD 바 바로 아래 가운데 — 스킬·보관함 UI와 겹치지 않는 자리
            var previous = GUI.backgroundColor;
            for (int i = 0; i < Speeds.Length; i++)
            {
                GUI.backgroundColor = Mathf.Approximately(_speed, Speeds[i]) ? new Color(1f, 0.82f, 0.3f) : Color.white;
                if (GUI.Button(new Rect(x + i * (buttonW + gap), y, buttonW, buttonH), Labels[i]))
                {
                    _speed = Speeds[i];
                    Time.timeScale = _speed;
                }
            }
            GUI.backgroundColor = previous;
        }
    }
}
