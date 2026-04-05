using UnityEngine;
using UnityEngine.UI;

namespace MySecondWorld.Network
{
    /// <summary>
    /// Canvas UI 패널의 컨트롤러. 버튼 눌렀을 때 동작을 연결하는 역할.
    /// </summary>
    public class SharedScreenUI : MonoBehaviour
    {
        [SerializeField] private SharedScreenController _controller;
        [SerializeField] private Toggle _presenterToggle;
        [SerializeField] private Button _prevPageButton;
        [SerializeField] private Button _nextPageButton;

        private void Awake()
        {
            _presenterToggle.onValueChanged.AddListener(OpenFileDialog);
            _prevPageButton.onClick.AddListener(PrevPage);
            _nextPageButton.onClick.AddListener(NextPage);
        }
        private void OnDestroy()
        {
            _presenterToggle.onValueChanged.RemoveListener(OpenFileDialog);
            _prevPageButton.onClick.RemoveListener(PrevPage);
            _nextPageButton.onClick.RemoveListener(NextPage);
        }

        public void Init(SharedScreenController controller)
        {
            if (_controller != null)
                _controller.onPresenterChanged -= UpdateButtons;

            _controller = controller;
            _controller.onPresenterChanged += UpdateButtons;
            _presenterToggle.isOn = false;
            UpdateButtons(_controller.PresenterId);
        }
        private void UpdateButtons(uint presenterId)
        {
            uint myNetId = Mirror.NetworkClient.localPlayer?.netId ?? 0;
            bool canControl = presenterId == 0 || presenterId == myNetId;
            _prevPageButton.interactable = canControl;
            _nextPageButton.interactable = canControl;
        }

        private void OpenFileDialog(bool isOn)
        {
            if (isOn)
            {
                _controller.CmdSetPresenter();
                // TODO: 파일 다이얼로그로 교체
                string testPath = @"C:\Users\BaeSoohyun\Documents\버킷리스트.pdf";
                FileType type = FileType.PDF;

                FileChunkSender.SendFile(_controller, testPath, type);
            }
            else
            {
                _controller.CmdReleasePresenter();
            }
        }

        private void PrevPage()
        {
            _controller.RequestPrevPage();
        }

        private void NextPage()
        {
            _controller.RequestNextPage();
        }
    }
}