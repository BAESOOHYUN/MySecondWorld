using System;

namespace MySecondWorld.Network
{
    /// <summary>
    /// 방(match) 단위로 몬스터 웨이브를 켜고 끄는 스포너의 계약.
    ///
    /// 이 인터페이스가 존재하는 이유는 asmdef 순환참조 때문이다.
    /// MySecondWorld.Combat → MySecondWorld.Network 참조는 있지만 역방향은 없다.
    /// 그래서 Network 쪽(GameNetworkManager)이 Combat의 MonsterWaveSpawner를 직접 부를 수 없다.
    /// 계약만 Network에 두고 Combat이 구현하게 해서 방향을 뒤집지 않고 호출한다.
    /// </summary>
    public interface IRoomWaveSpawner
    {
        /// <summary>해당 match(방)의 웨이브를 시작한다. 이미 돌고 있으면 무시한다.</summary>
        void StartWaves(Guid matchId);

        /// <summary>해당 match(방)의 몬스터를 전부 정리하고 웨이브를 멈춘다.</summary>
        void StopWaves(Guid matchId);
    }
}
