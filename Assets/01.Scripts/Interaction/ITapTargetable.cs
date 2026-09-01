using UnityEngine;

namespace MySecondWorld.Interaction
{
    /// <summary>
    /// 탭으로 고를 수 있는 대상의 계약.
    /// 판정은 TapTarget 레이어의 트리거 콜라이더에 대한 레이캐스트로 한다 —
    /// 눈에 보이는 몸과 판정이 어긋나지 않게 하려면 실제 콜라이더밖에 답이 없다.
    /// </summary>
    public interface ITapTargetable
    {
        Transform Anchor { get; }   // UI 추적(3단계)이 쓰는 기준점
        bool IsSelectable { get; }  // 사망 등 지금 고르면 안 되는 상태에서 false
        void OnTapped();
    }
}