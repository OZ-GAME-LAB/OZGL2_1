using System.Collections.Generic;

namespace OZGL2.Tutorial
{
    public enum StepKind
    {
        Talk,      // 마왕이 말한다. 화면을 누르면 다음으로
        Click,     // "이걸 눌러 보거라" — 강조된 단추를 직접 누르면 다음으로(그 단추만 눌린다)
        WaitClose, // 열린 창을 닫을 때까지 기다린다(창은 직접 만질 수 있다)
    }

    /// <summary>마왕의 한 마디. Target은 화살표로 가리킬 UI의 열쇠(TutorialDirector가 찾는다) — 비우면 가리키지 않고 말만 한다.</summary>
    public sealed class TutorialStep
    {
        public readonly string Text;
        public readonly string Target;
        public readonly bool Emphasis; // 마왕이 몸짓(공격 모션)을 하며 말한다
        public readonly StepKind Kind;
        public readonly string Popup;  // WaitClose가 기다리는 창의 오브젝트 이름

        public TutorialStep(string text, string target = null, bool emphasis = false)
        {
            Text = text; Target = target; Emphasis = emphasis; Kind = StepKind.Talk;
        }

        private TutorialStep(string text, string target, StepKind kind, string popup)
        {
            Text = text; Target = target; Emphasis = false; Kind = kind; Popup = popup;
        }

        /// <summary>강조한 단추를 직접 눌러 보게 한다.</summary>
        /// <param name="popup">이 단추를 누르면 열리는 창의 이름. 주면 그 창이 열리는 순간 바로 다음(설명)으로 넘어간다.</param>
        public static TutorialStep Press(string text, string target, string popup = null) => new TutorialStep(text, target, StepKind.Click, popup);

        /// <summary>열린 창을 닫을 때까지 기다린다. 화살표는 그 창의 닫기 단추를 가리킨다.</summary>
        public static TutorialStep WaitClose(string text, string popupName) => new TutorialStep(text, "popup.close:" + popupName, StepKind.WaitClose, popupName);
    }

    public sealed class TutorialSequence
    {
        public readonly string Id;
        public readonly string Title;          // 도움말 목록에 보이는 이름
        public readonly string Scene;          // "ingame" 또는 "lobby" — 도움말(?)에는 지금 장면의 주제만 나온다
        public readonly bool PauseGame;        // 말하는 동안 게임을 멈춘다(전투 중 설명)
        public readonly bool InHelpMenu;       // 도움말 목록에 올릴지
        public readonly TutorialStep[] Steps;

        public TutorialSequence(string id, string title, string scene, bool pause, bool inHelp, params TutorialStep[] steps)
        {
            Id = id; Title = title; Scene = scene; PauseGame = pause; InHelpMenu = inHelp; Steps = steps;
        }
    }

    /// <summary>
    /// 마왕이 들려주는 설명 모음. 말투는 거만하지만 친절한 마왕.
    /// 게임 규칙이 바뀌면 이 표의 문장만 고치면 된다(코드·씬은 건드리지 않는다).
    /// Target 열쇠: wave(이번 웨이브) · grid(마왕성 칸) · hand(카드 손패) · synergy(시너지 목록) · start(전투 시작) ·
    ///              skills(스킬 슬롯) · xp(경험치바) · currency(재화) · reroll(리롤) · king(마왕 자리)
    /// 로비: level(레벨) · nav(하단 메뉴) · stage(스테이지) · startbtn(전투 준비) · traittree(특성 트리) ·
    ///       skillpoints(SP) · slots(장착 칸) · codexlist(도감 목록) · btn.achievement/btn.traits/btn.skills/btn.codex(하단 메뉴 단추)
    /// </summary>
    public static class TutorialLibrary
    {
        // 처음 한 번 자동으로 나오는 것 ────────────────────────────────

        public static readonly TutorialSequence Intro = new TutorialSequence("intro", "게임 목표와 배치", "ingame", false, true,
            new TutorialStep("크하하! 잘 왔다, 새내기 마왕이여! 이 몸이 직접 마왕성을 지키는 법을 알려주마!", null, true),
            new TutorialStep("저 '이번 웨이브'를 보거라. 이번에 쳐들어올 용사들이다. 30웨이브를 모두 막아내면 짐의 승리니라!", "wave"),
            new TutorialStep("이 칸들이 마왕성이다. 용사들은 위에서 내려와 짐을 노린다. 막으려면 마왕군을 배치해야 하느니라.", "grid"),
            new TutorialStep("아래 카드를 끌어다 칸 위에 놓으면 마왕군이 배치된다. 잘못 놓았으면 카드 쪽으로 다시 끌어 보관할 수 있다.", "hand"),
            new TutorialStep("같은 마왕군 두 기를 겹쳐 놓으면 합성되어 별(★)이 오른다. 별은 최대 3개, 별이 높을수록 강하다!", "hand"),
            new TutorialStep("같은 직업을 많이 모으면 시너지가 발동한다. 오른쪽 목록에 마우스를 올리면 효과를 볼 수 있다.", "synergy"),
            new TutorialStep("준비가 끝났으면 '전투 시작'을 눌러라. 짐은 뒤에서 지켜보고 있겠다!", "start", true));

        public static readonly TutorialSequence Battle = new TutorialSequence("battle", "전투와 스킬", "ingame", true, true,
            new TutorialStep("전투가 시작됐다! 우리 마왕군이 용사들과 싸우는 모습을 지켜보거라.", null, true),
            new TutorialStep("아래 스킬 슬롯을 눌러 범위를 정하고 놓으면, 짐이 직접 마법을 쓴다. 쿨타임이 끝나면 다시 쓸 수 있느니라.", "skills"),
            new TutorialStep("용사를 쓰러뜨리면 영혼이 모여 경험치가 오른다. 레벨이 오를수록 짐이 강해지느니라.", "xp"),
            new TutorialStep("용사를 잡으면 재화도 얻는다. 재화는 보상 카드를 다시 뽑는 리롤에 쓰거라.", "currency"),
            new TutorialStep("위쪽의 배속 버튼으로 전투를 빠르게 돌릴 수도 있다. 자, 용사들을 막아 보거라!", null, true));

        public static readonly TutorialSequence Reward = new TutorialSequence("reward", "보상 카드와 리롤", "ingame", false, true,
            new TutorialStep("웨이브 클리어! 마왕군 카드 3장 중 하나를 골라 보상으로 받거라.", null, true),
            new TutorialStep("고른 유닛은 보관함(아래 카드 칸)에 들어간다. 다음 배치 때 칸에 올려 놓으면 된다."),
            new TutorialStep("마음에 드는 카드가 없으면 리롤이다. 재화를 내고 카드를 다시 뽑을 수 있다.", "reroll"));

        public static readonly TutorialSequence Augment = new TutorialSequence("augment", "보스와 증강", "ingame", false, true,
            new TutorialStep("보스를 쓰러뜨렸구나, 대단하다! 보상으로 증강을 고를 수 있다.", null, true),
            new TutorialStep("증강은 이번 판 동안 마왕군과 스킬을 강하게 해 준다. 어떤 증강이 우리 마왕군에게 어울릴지 잘 생각해서 고르거라."));

        public static readonly TutorialSequence Boss = new TutorialSequence("boss", "보스 웨이브", "ingame", false, false,
            new TutorialStep("이번 웨이브에는 보스가 온다! 체력이 매우 높으니 스킬을 아껴 두고, 마왕군을 든든히 세워 두거라.", "wave", true));

        // 도움말(?)에서만 다시 보는 것 ───────────────────────────────

        public static readonly TutorialSequence Synergy = new TutorialSequence("topic.synergy", "시너지", "ingame", false, true,
            new TutorialStep("같은 직업을 3명 모으면 1단계, 5명 모으면 2단계 시너지가 발동한다.", "synergy"),
            new TutorialStep("직업이 다른 둘을 함께 모으면 '조합 시너지'도 생긴다. 예를 들어 궁수와 마법사 각 2명이면 포격대다!"));

        public static readonly TutorialSequence Levels = new TutorialSequence("topic.levels", "경험치와 재화", "ingame", false, true,
            new TutorialStep("용사를 쓰러뜨리면 경험치가 올라 마왕이 레벨업한다. 레벨업으로 얻는 포인트는 로비 특성에 쓰거라.", "xp"),
            new TutorialStep("재화도 용사를 잡을 때와 웨이브를 클리어할 때 얻는다. 재화는 리롤에 쓴다.", "currency"));

        public static readonly TutorialSequence Unlock = new TutorialSequence("topic.unlock", "SP와 해금", "ingame", false, true,
            new TutorialStep("SP는 웨이브를 깰 때마다 쌓인다. 2웨이브마다 1씩 얻고, 10웨이브 단위로는 보너스가 붙는다. 로비 스킬창에서 새 스킬을 해금하는 데 쓰거라."),
            new TutorialStep("마왕군은 5·10·15웨이브를 클리어할 때마다 새 유닛이 해금된다. 해금된 유닛은 이후 보상 카드에 나온다!"));


        // 로비 설명 ──────────────────────────────────────────────

        /// <summary>업적 → 특성 → 스킬 → 도감 순서로 마왕이 단추를 눌러 보게 하고, 열린 창마다 설명한다.</summary>
        public static readonly TutorialSequence LobbyTour = new TutorialSequence("lobby.tour", "로비 둘러보기", "lobby", false, true,
            new TutorialStep("크하하! 마왕성에 잘 왔다! 여기는 전투에 나서기 전에 짐을 단련하는 로비다.", null, true),
            new TutorialStep("여기에 짐의 레벨과 경험치가 있다. 전투에서 용사를 쓰러뜨린 만큼 성장하느니라.", "level"),
            TutorialStep.Press("아래 메뉴를 순서대로 둘러보자. 먼저 '업적'을 눌러 보거라!", "btn.achievement", "@page"),
            new TutorialStep("여기는 업적이다. 전투에서 이룬 일들이 기록되는 곳이니라.", null, true),
            new TutorialStep("'첫 전투에서 승리하기', '용사 100명 처치하기'처럼 조건이 정해져 있고, 이루면 '달성'으로 바뀐다. 위쪽 '달성' 숫자로 얼마나 이루었는지 볼 수 있다."),
            TutorialStep.WaitClose("다 보았으면 오른쪽 위 '뒤로'를 눌러 돌아오거라!", "@page"),
            TutorialStep.Press("다음은 '특성'이다. 눌러 보거라!", "btn.traits", "@page"),
            new TutorialStep("여기는 특성이다. 전투에서 레벨업으로 얻은 포인트(LP)로 짐을 영구히 강화한다.", null, true),
            new TutorialStep("군대, 명령, 저주, 연구 네 갈래가 있다. 마음에 드는 갈래를 깊게 파도 좋고, 골고루 찍어도 좋다.", "traittree"),
            new TutorialStep("특성은 한 번 찍으면 다음 판에도 이어진다. 잘못 찍었다면 초기화해서 포인트를 돌려받을 수 있느니라."),
            TutorialStep.WaitClose("다 보았으면 '뒤로'를 눌러 돌아오거라!", "@page"),
            TutorialStep.Press("이번에는 '스킬 세팅'이다. 눌러 보거라!", "btn.skills", "@page"),
            new TutorialStep("여기는 스킬이다. 전투 중에 짐이 직접 쓰는 마법을 해금하고 장착하는 곳이니라.", null, true),
            new TutorialStep("스킬은 SP로 해금한다. SP는 전투에서 웨이브를 깰 때 쌓이는데, 2웨이브마다 1씩 얻는다.", "skillpoints"),
            new TutorialStep("장착 칸은 처음엔 3개다. 장착해 둔 스킬만 전투 아래 슬롯에 나오니, 쓸 스킬을 골라 끼워 두거라.", "slots"),
            TutorialStep.WaitClose("다 보았으면 '뒤로'를 눌러 돌아오거라!", "@page"),
            TutorialStep.Press("마지막으로 '도감'을 눌러 보거라!", "btn.codex", "@page"),
            new TutorialStep("도감에는 마왕군과 용사들의 정보가 모여 있다. 해금된 마왕군만 전투 보상 카드로 나오느니라.", "codexlist", true),
            TutorialStep.WaitClose("다 보았으면 '뒤로'를 눌러 돌아오거라. 마지막 설명이 남았다!", "@page"),
            new TutorialStep("가운데에서 도전할 스테이지를 고른다.", "stage"),
            new TutorialStep("준비가 끝났으면 '전투 준비'를 눌러라. 용사들이 기다리고 있다!", "startbtn", true));

        public static readonly TutorialSequence LobbyTraits = new TutorialSequence("lobby.traits", "특성", "lobby", false, true,
            new TutorialStep("여기는 특성이다. 전투에서 레벨업으로 얻은 포인트(LP)로 짐을 영구히 강화한다.", null, true),
            new TutorialStep("군대, 명령, 저주, 연구 네 갈래가 있다. 마음에 드는 갈래를 깊게 파도 좋고, 골고루 찍어도 좋다.", "traittree"),
            new TutorialStep("특성은 한 번 찍으면 다음 판에도 이어진다. 잘못 찍었다면 초기화해서 포인트를 돌려받을 수 있느니라."));

        public static readonly TutorialSequence LobbySkills = new TutorialSequence("lobby.skills", "스킬", "lobby", false, true,
            new TutorialStep("여기는 스킬이다. 전투 중에 짐이 직접 쓰는 마법을 해금하고 장착하는 곳이니라.", null, true),
            new TutorialStep("스킬은 SP로 해금한다. SP는 전투에서 웨이브를 깰 때 쌓이는데, 2웨이브마다 1씩 얻는다.", "skillpoints"),
            new TutorialStep("장착 칸은 처음엔 3개다. 장착해 둔 스킬만 전투 아래 슬롯에 나오니, 쓸 스킬을 골라 끼워 두거라.", "slots"));

        public static readonly TutorialSequence LobbyCodex = new TutorialSequence("lobby.codex", "도감", "lobby", false, true,
            new TutorialStep("도감에는 마왕군과 용사들의 정보가 모여 있다. 해금된 마왕군만 전투 보상 카드로 나오느니라.", "codexlist", true));

        /// <summary>자동 설명 + 도움말 목록 순서.</summary>
        public static readonly TutorialSequence[] All =
            { Intro, Battle, Reward, Augment, Synergy, Levels, Unlock, Boss, LobbyTour, LobbyTraits, LobbySkills, LobbyCodex };

        public static TutorialSequence Find(string id)
        {
            foreach (var s in All) if (s.Id == id) return s;
            return null;
        }

        public static IEnumerable<TutorialSequence> HelpTopics(string scene)
        {
            foreach (var s in All) if (s.InHelpMenu && s.Scene == scene) yield return s;
        }
    }
}
