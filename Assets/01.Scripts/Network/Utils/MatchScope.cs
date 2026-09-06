using Mirror;
using UnityEngine;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 두 오브젝트가 같은 match(= 같은 던전 방 / 같은 씬)에 속하는지 판정한다.
    ///
    /// 던전은 씬 하나 안에 방을 여러 개 두고 <see cref="NetworkMatch"/>로 가시성만 나눈다.
    /// 그런데 물리(콜라이더/트리거/SphereCast)는 matchId를 모르기 때문에,
    /// 두 방의 몬스터가 같은 좌표에 겹쳐 있으면 다음이 그대로 새어나간다.
    ///   - 내 화살이 안 보이는 다른 방 몬스터에 맞고 사라짐
    ///   - 다른 방 몬스터가 나를 감지해서 달려오고, 안 보이는 곳에서 때림
    /// 물리 쿼리 결과를 쓰는 지점마다 이 함수로 한 번 걸러 준다.
    ///
    /// 주의: matchId는 서버에서만 유효하다(클라에는 동기화되지 않아 항상 Guid.Empty).
    /// 반드시 서버 실행 경로에서만 호출할 것.
    /// </summary>
    public static class MatchScope
    {
        public static bool Same(GameObject a, GameObject b)
        {
            // 판단 근거가 없으면 통과시킨다(fail-open).
            // 필드에는 NetworkMatch가 없는 대상이 있을 수 있는데,
            // fail-close로 짜면 기존 필드 전투가 통째로 죽는다.
            if (a == null || b == null) return true;

            var ma = a.GetComponentInParent<NetworkMatch>();
            var mb = b.GetComponentInParent<NetworkMatch>();
            if (ma == null || mb == null) return true;

            return ma.matchId == mb.matchId;
        }
    }
}
