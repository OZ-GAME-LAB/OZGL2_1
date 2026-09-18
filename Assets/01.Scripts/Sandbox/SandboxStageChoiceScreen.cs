using UnityEngine;
using UnityEngine.SceneManagement;

namespace OZGL2.Sandbox
{
    /// <summary>
    /// 개인 루프 테스트용 스테이지 선택 화면 — 보통(30R)/어려움(50R) 고르면 인게임으로 이동.
    /// 실제 팀 Builds/StageChoice.unity와 완전히 무관한 내 전용 테스트 씬.
    /// </summary>
    public class SandboxStageChoiceScreen : MonoBehaviour
    {
        private void OnGUI()
        {
            const float w = 440f, h = 300f;
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), SandboxGameUI.Panel);
            GUILayout.Label("스테이지 선택", SandboxGameUI.Title);
            GUILayout.Label("난이도를 골라줘", SandboxGameUI.Subtitle);
            GUILayout.Space(16f);

            if (GUILayout.Button("보통 (30라운드)", SandboxGameUI.PrimaryButton))
            {
                SandboxLoopState.ChosenStageAssetName = "StageNormal30";
                SceneManager.LoadScene(SandboxLoopState.InGameScene);
            }
            GUILayout.Space(8f);
            if (GUILayout.Button("어려움 (50라운드)", SandboxGameUI.PrimaryButton))
            {
                SandboxLoopState.ChosenStageAssetName = "StageHard50";
                SceneManager.LoadScene(SandboxLoopState.InGameScene);
            }

            GUILayout.Space(16f);
            if (GUILayout.Button("로비로", SandboxGameUI.SecondaryButton))
                SceneManager.LoadScene(SandboxLoopState.LobbyScene);

            GUILayout.EndArea();
        }
    }
}
