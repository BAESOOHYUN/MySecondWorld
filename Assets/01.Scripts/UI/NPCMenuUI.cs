using UnityEngine;
using UnityEngine.UI;
using R3;
using MySecondWorld.AI;

namespace MySecondWorld.UI
{
    // 대화하기/상점 선택 메뉴. NPCDialogueUI/ShopUI와 동일하게 전역 패널 하나를 모든 NPC가 공유한다 —
    // "누구의 메뉴인지"는 NPCInteractable이 자기 cts 토큰으로 구독 범위를 잡아서 구분한다(Open()에 파라미터 불필요).
    public class NPCMenuUI : MonoBehaviour, INPCMenuUI
    {
        [SerializeField] private Canvas _canvas;   // 이 오브젝트의 Canvas — GameObject 자체는 항상 활성 유지
        [SerializeField] private Button _talkButton;
        [SerializeField] private Button _shopButton;

        public Observable<Unit> OnSelectTalk => _talkButton.OnClickAsObservable().AsUnitObservable();
        public Observable<Unit> OnSelectShop => _shopButton.OnClickAsObservable().AsUnitObservable();

        private void Start() => _canvas.enabled = false;

        public void Open() => _canvas.enabled = true;

        public void Close() => _canvas.enabled = false;

    }
}