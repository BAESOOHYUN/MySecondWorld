using Cysharp.Threading.Tasks;
using UnityEngine;
using MySecondWorld.Pool;

namespace MySecondWorld.Combat
{
    // 같은 클래스가 서버에서는 판정을, 클라에서는 연출을 담당한다.
    // 클래스를 나누지 않는 이유: 이동·수명·풀 반납·스케일 보정이 전부 공통이고,
    // 실제로 갈리는 건 "판정을 하느냐" 한 줄뿐이기 때문.
    public enum ProjectileMode
    {
        Authoritative, // 서버 — 스윕으로 명중을 판정하고 결과를 데미지 파이프라인에 돌려준다
        Presentation,  // 순수 클라 — 보여주기만. 클라엔 몬스터 콜라이더가 없어 판정 자체가 불가능하다
    }

    [DisallowMultipleComponent]
    public class Projectile : MonoBehaviour
    {
        // 스윕 시작점. 루트 피벗은 노킹 때 손이 쥐는 '깃'이라 거기서 재면
        // 화살촉이 이미 대상 안으로 파고든 뒤에야 판정된다.
        [SerializeField] private Transform _tip;

        private ProjectileMode _mode;
        private Vector3 _direction;
        private float _speed;
        private float _sweepRadius;
        private LayerMask _hitMask;
        private LayerMask _blockMask;
        private float _lifetime;
        private float _elapsed;

        private bool _isFlying; // Launch() 전(노킹 대기)이나 결과 확정 후에는 false
        private bool _resolved; // 결과 확정 1회 보장 — 중복 반납/중복 통지 차단
        private Vector3 _originalLocalScale;

        private UniTaskCompletionSource<IDamageable> _completionSource;

        private void Awake()
        {
            // 프리팹 원본 스케일. 손 본에 붙일 때 부모 스케일을 상쇄하는 기준값이 된다.
            _originalLocalScale = transform.localScale;

            if (_tip == null)
            {
                Debug.LogError($"Projectile: _tip 미할당 — 스윕 시작점을 알 수 없다. {name}", this);
                _tip = transform; // 최소한 동작은 하도록. 판정이 화살 길이만큼 늦어진다
            }
        }

        private void Update()
        {
            if (!_isFlying) return;

            float step = _speed * Time.deltaTime;

            // 권위 모드만 판정한다. 표현 화살은 클라에 몬스터 콜라이더 자체가 없어
            // 스윕을 돌려봐야 아무것도 안 걸린다 — 낭비라서 아예 건너뛴다.
            if (_mode == ProjectileMode.Authoritative && TrySweep(step))
                return; // 명중했으면 이번 스텝 이동은 하지 않는다(SnapTipTo가 이미 위치를 잡았다)

            transform.position += _direction * step;

            _elapsed += Time.deltaTime;
            if (_elapsed >= _lifetime)
                Resolve(null); // 아무것도 못 맞힘으로 확정
        }

        private void OnDestroy()
        {
            // 풀 Dispose(씬 언로드·에디터 종료)나 maxSize 초과 폐기로 파괴될 수 있다.
            // 그 경우에도 대기 중인 await를 깨워 매달린 실행을 남기지 않는다.
            _completionSource?.TrySetResult(null);
        }

        // 손 소켓에 정지 상태로 붙인다. 이 상태에서는 이동도 판정도 하지 않는다.
        // 회전 오프셋을 밖에서 받는 이유: 노킹 자세와 비행 방향은 별개 문제이기 때문.
        // (발사 시 LookRotation이 회전을 통째로 덮어쓰므로 비행은 루트 +Z가 지배한다)
        public void Nock(Transform socket, Quaternion localRotation)
        {
            _isFlying = false;
            _resolved = false;

            var t = transform;
            t.SetParent(socket, false);
            t.localPosition = Vector3.zero;
            t.localRotation = localRotation;

            // 캐릭터 본은 FBX 임포트 스케일 때문에 1이 아닌 경우가 많다.
            // 부모 스케일을 역산해 상쇄, 항상 원본 크기로 보이게 한다.
            var parentScale = socket.lossyScale;
            t.localScale = new Vector3(
                _originalLocalScale.x / parentScale.x,
                _originalLocalScale.y / parentScale.y,
                _originalLocalScale.z / parentScale.z);
        }

        // request의 Prefab/NockSource는 "화살을 어디서 구할지"에 대한 정보라 여기선 쓰지 않는다.
        // 그건 ProjectileLauncher가 발사 전에 이미 해결하고 들어온다.
        public UniTask<IDamageable> Launch(in ProjectileRequest request, ProjectileMode mode)
        {
            // 풀에서 나온 인스턴스는 직전 발사의 값을 그대로 들고 있다 — 전부 새로 채운다
            _mode = mode;
            _direction = request.Direction.normalized;
            _speed = request.Speed;
            _sweepRadius = request.SweepRadius;
            _hitMask = request.HitMask;
            _blockMask = request.BlockMask;
            _lifetime = request.MaxLifetime;
            _elapsed = 0f;
            _resolved = false;

            _completionSource = new UniTaskCompletionSource<IDamageable>();

            var t = transform;
            t.SetParent(null); // 손에서 분리 — 이제부터 월드 기준 독립 비행
            t.SetPositionAndRotation(request.Origin, Quaternion.LookRotation(_direction));
            t.localScale = _originalLocalScale; // 부모가 없어졌으니 로컬 = 월드

            // _isFlying = true를 맨 마지막에 두는 게 의도적이다.
            // 위 대입들 사이에 프레임이 끼어들 일은 없지만,
            // "준비가 끝나기 전엔 절대 안 움직인다"를 코드 순서로 못 박아두면
            // 나중에 누가 중간에 코드를 끼워 넣어도 안전하다.
            _isFlying = true; // 값이 전부 채워진 뒤 마지막에 켠다
            return _completionSource.Task;
        }

        // 이번 스텝에 "지나갈 구간"을 통째로 훑는다.
        // 위치를 옮긴 뒤 겹침을 보는 방식(Trigger)과 달리 스텝 사이 공간이 비지 않아 터널링이 없다.
        private bool TrySweep(float step)
        {
            if (!Physics.SphereCast(_tip.position, _sweepRadius, _direction, out var hit, step,
                    _hitMask | _blockMask, QueryTriggerInteraction.Ignore))
                return false;

            // 벽·지형이 먼저 걸렸으면 데미지 없이 소멸 — 벽 너머 저격 차단
            if (IsInMask(_blockMask, hit.collider.gameObject.layer))
            {
                SnapTipTo(hit.point);
                Resolve(null);
                return true;
            }

            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target == null)
                return false; // IDamageable 없는 오탐 — 명중으로 치지 않고 계속 비행

            SnapTipTo(hit.point);
            Resolve(target);
            return true;
        }

        // 서버가 "여기서 맞았다"고 알려줄 때 표현 화살을 그 지점에서 정리한다.
        // 판정과 연출을 일치시키는 경로(명중 Rpc).
        public void ForceResolveAt(Vector3 hitPoint)
        {
            if (!_isFlying) return;
            SnapTipTo(hitPoint);
            Resolve(null);
        }

        private static bool IsInMask(LayerMask mask, int layer) => (mask.value & (1 << layer)) != 0;

        // 피벗이 아니라 화살촉이 충돌 지점에 놓이도록 보정한다(루트 피벗은 깃이다).
        private void SnapTipTo(Vector3 point) =>
            transform.position = point - (_tip.position - transform.position);

        // 명중 / 벽 / 수명초과 / 강제소멸이 전부 여기로 모인다.
        private void Resolve(IDamageable target)
        {
            if (_resolved) return;
            _resolved = true;
            _isFlying = false;

            // 서버 파이프라인이 await로 매달려 있다 — 반드시 깨운다.
            // 이걸 빠뜨리면 그 스킬 실행이 영원히 완료되지 않는다.
            _completionSource?.TrySetResult(target);

            ReturnToPool();
        }

        private void ReturnToPool()
        {
            // 손/캐스터 밑에 남은 채로 반납되면, 그 부모가 파괴될 때 같이 끌려간다
            transform.SetParent(null);

            if (TryGetComponent(out PooledObject pooled))
                pooled.ReturnToPool();
            else
                Destroy(gameObject); // 풀 밖에서 만들어진 예외적 인스턴스
        }
    }
}