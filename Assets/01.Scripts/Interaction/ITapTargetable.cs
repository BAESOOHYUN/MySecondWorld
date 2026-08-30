using UnityEngine;

namespace MySecondWorld.Interaction
{
    /// <summary>
    /// 탭으로 고를 수 있는 대상의 계약.
    /// 콜라이더를 요구하지 않는다 — 클라이언트의 몬스터 사본에는 콜라이더가 하나도 없어서
    /// 레이캐스트 기반 선택이 원리적으로 불가능하기 때문(CONTEXT §1-②).
    /// </summary>
    public interface ITapTargetable
    {
        // 화면 좌표의 기준점. 판정(리졸버)과 UI 추적(3단계)이 같은 지점을 공유한다.
        Transform Anchor { get; }

        // Anchor 로컬 공간 기준의 바운드. 최초 1회 산출한 뒤 고정한다.
        //
        // 월드 바운드가 아니라 로컬인 이유:
        // SkinnedMeshRenderer.bounds는 기본값(updateWhenOffscreen = false)에서 임포트 시점에 계산된
        // 고정 박스를 루트 본 기준으로 변환해 돌려준다 — 매번 읽어도 같은 값이라 한 번만 재면 된다.
        // updateWhenOffscreen을 켠 대상이 섞여 들어와도, 로컬로 고정해두면 판정 영역이
        // 모션마다 들쭉날쭉해지지 않는다("이 몸통은 발밑 기준 높이 1.8m"는 항상 참).
        // 월드 위치는 매번 Anchor가 알려주므로, 리졸버가 Anchor 기준으로 둘을 합친다.
        Bounds LocalBounds { get; }

        // 사망/비활성 등 지금 고르면 안 되는 상태에서 false.
        //
        // 리졸버가 activeInHierarchy로 대신 검사하면 안 되는 이유:
        // 몬스터는 죽고 나서도 사망 애니메이션이 끝날 때까지 살아있고 활성 상태다.
        // 그 사이에 선택되면 이미 죽은 놈을 계속 때리게 된다.
        bool IsSelectable { get; }

        // 승자로 뽑혔을 때 호출. 한 번의 탭에 최대 한 번만 불린다.
        void OnTapped();
    }
}