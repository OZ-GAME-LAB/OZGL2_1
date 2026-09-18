namespace OZGL2.Sandbox
{
    /// <summary>
    /// 개인 루프 테스트 씬 3개(로비→스테이지선택→인게임) 사이에서 씬 이름과 선택한 스테이지를
    /// 넘겨주는 정적 상태. 팀 실제 씬(Builds/Lobby 등)과는 완전히 무관.
    /// </summary>
    public static class SandboxLoopState
    {
        public const string LobbyScene = "JOB_SUNGMIN_Lobby";
        public const string StageChoiceScene = "JOB_SUNGMIN_StageChoice";
        public const string InGameScene = "JOB_SUNGMIN_InGame";

        public static string ChosenStageAssetName = "StageNormal30";
    }
}
