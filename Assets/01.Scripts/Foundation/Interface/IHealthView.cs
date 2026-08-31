using System;
using UnityEngine;

namespace MySecondWorld.Foundation
{
    /// <summary>
    /// HP바가 읽는 최소 계약. 구현체가 몬스터인지 컴패니언인지 플레이어인지 UI는 알 필요가 없다.
    ///
    /// 이벤트가 값을 안 실어 보내는 이유: 값이 두 개(현재/최대)라
    /// 하나만 실으면 UI가 짝이 안 맞는 다른 하나를 쓰게 된다. 통보만 하고 둘 다 다시 읽는다.
    /// </summary>
    public interface IHealthView
    {
        Transform Anchor { get; }   // UI가 따라갈 기준점 (화면 고정 바에서는 안 씀)
        float CurrentHP { get; }
        float MaxHP { get; }
        event Action HPChanged;
    }
}