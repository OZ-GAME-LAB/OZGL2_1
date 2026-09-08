namespace OZGL2.Contracts
{
    /// <summary>조준·프리뷰 시 강조 표시할 수 있는 대상. 샌드박스 유닛 / 실제 유닛 모두 구현 가능.</summary>
    public interface IHighlightable
    {
        void SetHighlight(bool on);
    }
}
