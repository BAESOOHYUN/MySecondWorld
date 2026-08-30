using UnityEngine;

namespace MySecondWorld.Interaction
{
    /// <summary>
    /// ITapTargetable 구현체들이 LocalBounds를 만들 때 쓰는 공용 계산.
    /// NPC와 몬스터가 같은 규칙으로 바운드를 잡게 하려고 한 곳에 모았다.
    /// </summary>
    public static class TapBoundsUtility
    {
        // 8개 코너의 부호 조합. 매번 new 하지 않으려고 미리 만들어 둔다.
        private static readonly Vector3[] _cornerSigns =
        {
            new(-1f, -1f, -1f), new(1f, -1f, -1f), new(-1f, 1f, -1f), new(1f, 1f, -1f),
            new(-1f, -1f,  1f), new(1f, -1f,  1f), new(-1f, 1f,  1f), new(1f, 1f,  1f),
        };

        // 렌더러를 하나도 못 찾았을 때 쓰는 값 — 사람 크기 정도의 상자.
        private static readonly Bounds _fallback = new(new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 1f));

        /// <summary>
        /// anchor 아래 모든 렌더러의 월드 바운드를 합산해 anchor 로컬 공간의 바운드로 변환한다.
        /// **최초 1회만 호출할 것** — GetComponentsInChildren는 비싸고, 값은 어차피 안 변한다.
        /// </summary>
        public static Bounds CalculateLocalBounds(Transform anchor)
        {
            if (anchor == null)
                return _fallback;

            // includeInactive를 켜지 않는다 — 지금 보이는 몸만 재야 한다.
            // NPCInteractable은 Awake에서 _highlightUI를 꺼두는데, 켜면 그 렌더러까지 합산돼 상자가 부푼다.
            var renderers = anchor.GetComponentsInChildren<Renderer>(false);
            bool hasAny = false;
            Bounds local = default;

            foreach (var r in renderers)
            {
                // 이펙트는 몸이 아니다. 파티클/트레일은 순간적으로 바운드가 크게 튀어서
                // 클릭 판정 영역을 엉뚱하게 부풀린다.
                if (r is ParticleSystemRenderer || r is TrailRenderer)
                    continue;

                Bounds world = r.bounds;

                // 월드 AABB의 8개 코너를 anchor 로컬로 옮겨 담는다.
                // 월드 중심/크기를 그대로 나누면 anchor가 회전해 있을 때 값이 어긋난다.
                for (int i = 0; i < _cornerSigns.Length; i++)
                {
                    Vector3 worldCorner = world.center + Vector3.Scale(world.extents, _cornerSigns[i]);
                    Vector3 localCorner = anchor.InverseTransformPoint(worldCorner);

                    if (!hasAny)
                    {
                        local = new Bounds(localCorner, Vector3.zero);
                        hasAny = true;
                    }
                    else
                    {
                        local.Encapsulate(localCorner);
                    }
                }
            }

            if (!hasAny)
            {
                Debug.LogWarning($"[TapBoundsUtility] {anchor.name}에서 렌더러를 못 찾음 — 기본 크기로 대체", anchor);
                return _fallback;
            }

            return local;
        }
    }
}