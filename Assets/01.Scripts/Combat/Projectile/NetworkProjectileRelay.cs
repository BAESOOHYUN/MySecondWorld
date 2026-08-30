using UnityEngine;
using Mirror;

namespace MySecondWorld.Combat
{
    // 투사체의 네트워크 배선. 플레이어·몬스터 공용.
    //
    // 몬스터가 이게 필요한 이유: 클라이언트의 몬스터 사본은 Mirror가 '순수 프리팹'으로 만든 것이라
    // RangedMonsterHitbox 같은 스크립트가 아예 없다. 애니메이션 이벤트를 받을 컴포넌트가 없으니
    // "쐈다"는 사실을 서버가 직접 알려주는 수밖에 없다.
    //
    // (Phase 5에서 플레이어용 CmdRequestShoot / LastAimDirection이 여기에 추가된다)
    public class NetworkProjectileRelay : NetworkBehaviour
    {
        // 투사체 프리팹의 단일 소유자.
        // 스탯 SO가 아니라 여기 있는 이유: 클라이언트 사본은 프리팹에 authoring된 것만 갖는다.
        // 스탯 SO에도 같은 참조를 두면 둘이 어긋날 수 있어 한 곳으로 모았다.
        [SerializeField] private GameObject _projectilePrefab;

        public GameObject ProjectilePrefab => _projectilePrefab;

        // 이 피어에서 마지막으로 띄운 표현 화살.
        // 한 발만 추적하는 이유: 몬스터 발사 쿨다운(1.5초+)이 비행 시간(~0.5초)보다 길어
        // 두 발이 동시에 떠 있는 경우가 사실상 없다. 겹치더라도 이전 화살은 수명으로 사라진다.
        private Projectile _lastPresentation;

        // 이 피어가 방금 띄운 표현 화살을 등록한다. 명중 Rpc가 오면 이 화살을 그 지점에서 정리한다.
        // 몬스터는 RpcLaunchPresentation이 내부에서 직접 채우고, 플레이어는 컨트롤러가 이걸 호출한다.
        public void TrackPresentation(Projectile projectile) => _lastPresentation = projectile;


        [ClientRpc]
        public void RpcLaunchPresentation(Vector3 origin, Vector3 direction, float speed, float lifetime)
        {
            // 호스트(서버 겸 클라)에는 이미 권위 화살이 날고 있다 — 여기서 또 만들면 화살이 두 개가 된다.
            if (isServer) return;

            if (_projectilePrefab == null)
            {
                Debug.LogError($"[NetworkProjectileRelay] _projectilePrefab 미할당 on {name}", this);
                return;
            }

            var request = new ProjectileRequest
            {
                Prefab = _projectilePrefab,
                NockSource = null, // 몬스터는 손 화살 개념이 없다
                Origin = origin,
                Direction = direction,
                Speed = speed,
                MaxLifetime = lifetime,
                // 표현 화살은 판정을 하지 않으므로 반경·마스크는 쓰이지 않는다
            };

            // 런처가 NetworkServer.active를 보고 Presentation 모드로 만든다.
            // 반환된 UniTask는 아무도 기다리지 않는다 — 클라엔 데미지 파이프라인이 없다.
            TrackPresentation(ProjectileLauncher.Launch(request, gameObject.scene));
            _lastPresentation = null; // 아래 주석 참고
        }

        [ClientRpc]
        public void RpcConfirmHit(Vector3 hitPoint)
        {
            if (isServer) return; // 서버 화살은 이미 자기 판정으로 정리됐다

            if (_lastPresentation != null)
                _lastPresentation.ForceResolveAt(hitPoint);

            _lastPresentation = null;
        }
    }
}