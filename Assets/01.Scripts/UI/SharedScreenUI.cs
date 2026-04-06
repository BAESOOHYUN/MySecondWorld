using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MySecondWorld.Network
{
    /// <summary>
    /// Canvas UI 패널의 컨트롤러. 버튼 눌렀을 때 동작을 연결하는 역할.
    /// </summary>
    public class SharedScreenUI : MonoBehaviour
    {
        [Header("Controller")]
        [SerializeField] private SharedScreenController _controller;

        [Header("FileLoad Panel")]
        [SerializeField] private GameObject _fileLoadPanel;
        [SerializeField] private TMP_InputField _urlInputField;    // URL 입력
        [SerializeField] private TMP_Text _localFilePathText;      // 로컬 경로 표시용
        [SerializeField] private Button _localFileSearchButton;    // 파일 탐색기 열기
        [SerializeField] private Button _clearLocalFileButton;     // 선택한 로컬파일 경로를 초기화 함
        [SerializeField] private Button _fileLoadButton;           // URL & 로컬 공용 다운로드 시작
        [SerializeField] private Toggle _allowDownloadToggle;
        [SerializeField] private Button _closeFileLoadPannelButton;

        [Header("Open View Button")]
        [SerializeField] private Button _openViewButton;

        [Header("View Panel")]
        [SerializeField] private GameObject _viewPanel;
        [SerializeField] private RawImage _pageImage;
        [SerializeField] private Button _prevButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Slider _pageSlider;
        [SerializeField] private TMP_InputField _pageInputField;  // 인풋필드에서 Content Type을 Integer Number로 설정
        [SerializeField] private TMP_Text _totalPageText;       // 총 페이지 수 표시
        [SerializeField] private Button _closeViewPannelButton;
        [SerializeField] private Button _endPresentButton;    // 발표자만 보임
        [SerializeField] private Button _saveButton;          // 관객 + allowDownload일 때만 보임

        private bool _isPresenter;
        private int _localPage;          // 관객의 독립 페이지
        private string _localFilePath;   // 선택된 로컬 경로 저장

        private void Awake()
        {
            _urlInputField.onValueChanged.AddListener(OnUrlInputChanged);
            _localFileSearchButton.onClick.AddListener(OnLocalFileSelect);
            _clearLocalFileButton.onClick.AddListener(OnClearLocalFile);
            _fileLoadButton.onClick.AddListener(OnLoad);
            _closeFileLoadPannelButton.onClick.AddListener(OnCloseFileLoad);
            _openViewButton.onClick.AddListener(OnOpenView);
            _prevButton.onClick.AddListener(OnPrevPage);
            _nextButton.onClick.AddListener(OnNextPage);
            _pageSlider.onValueChanged.AddListener(OnSliderChanged);
            _pageInputField.onEndEdit.AddListener(OnPageInputChanged);
            _closeViewPannelButton.onClick.AddListener(OnCloseView);
            _endPresentButton.onClick.AddListener(OnEndPresent);
            _saveButton.onClick.AddListener(OnSave);
        }

        private void OnDisable()
        {
            UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        }

        private void OnDestroy()
        {
            _urlInputField.onValueChanged.RemoveListener(OnUrlInputChanged);
            _localFileSearchButton.onClick.RemoveListener(OnLocalFileSelect);
            _clearLocalFileButton.onClick.RemoveListener(OnClearLocalFile);
            _fileLoadButton.onClick.RemoveListener(OnLoad);
            _closeFileLoadPannelButton.onClick.RemoveListener(OnCloseFileLoad);
            _openViewButton.onClick.RemoveListener(OnOpenView);
            _prevButton.onClick.RemoveListener(OnPrevPage);
            _nextButton.onClick.RemoveListener(OnNextPage);
            _pageSlider.onValueChanged.RemoveListener(OnSliderChanged);
            _pageInputField.onEndEdit.RemoveListener(OnPageInputChanged);
            _closeViewPannelButton.onClick.RemoveListener(OnCloseView);
            _endPresentButton.onClick.RemoveListener(OnEndPresent);
            _saveButton.onClick.RemoveListener(OnSave);

            if (_controller != null)
            {
                _controller.onPresenterChanged -= OnPresenterChanged;
                _controller.onAllowDownloadChanged -= OnAllowDownloadChanged;
                _controller.onFileReceived -= OnFileReceived;
                _controller.onPageChanged -= OnPageChanged;
            }
        }

        public void Init(SharedScreenController controller)
        {
            // 이전 구독 해제
            if (_controller != null)
            {
                _controller.onPresenterChanged -= OnPresenterChanged;
                _controller.onAllowDownloadChanged -= OnAllowDownloadChanged;
                _controller.onFileReceived -= OnFileReceived;
                _controller.onPageChanged -= OnPageChanged;
            }

            _controller = controller;
            _controller.onPresenterChanged += OnPresenterChanged;
            _controller.onAllowDownloadChanged += OnAllowDownloadChanged;
            _controller.onFileReceived += OnFileReceived; 
            _controller.onPageChanged += OnPageChanged;

            uint myNetId = Mirror.NetworkClient.localPlayer?.netId ?? 0;
            _isPresenter = _controller.IsPresenter(myNetId);

            // 진입 시 UI 분기
            if (!_controller.HasPresenter)
            {
                // 발표자 없음 → FileLoadPanel
                ShowFileLoadPanel();
            }
            else
            {
                // 발표자 있음 → OpenViewButton만
                ShowOpenViewButton();
                // 이미 파일이 있으면 로컬 페이지 초기화
                if (_controller.FileBuffer != null)
                    _localPage = _controller.CurrentPage;
            }
        }

        // 패널 전환 메서드 
        private void ShowFileLoadPanel()
        {
            _fileLoadPanel.SetActive(true);
            _openViewButton.gameObject.SetActive(false);
            _viewPanel.SetActive(false);
        }

        private void ShowOpenViewButton()
        {
            _fileLoadPanel.SetActive(false);
            _openViewButton.gameObject.SetActive(true);
            _viewPanel.SetActive(false);
        }

        // 관객이 ViewPanel을 열 때 저장 버튼 표시 여부 결정
        private void ShowViewPanel() 
        {
            _fileLoadPanel.SetActive(false);
            _openViewButton.gameObject.SetActive(false);
            _viewPanel.SetActive(true);

            // 발표자/관객에 따라 버튼 표시
            _endPresentButton.gameObject.SetActive(_isPresenter);
            _saveButton.gameObject.SetActive(!_isPresenter && _controller.AllowDownload);

            // 페이지 UI 갱신
            UpdatePageUI();
        }


        // 이벤트 핸들러 — 파일 로드
        private void OnLocalFileSelect()
        {
            // TODO: OpenFileDialog로 교체
            _localFilePath = @"C:\Users\BaeSoohyun\Documents\버킷리스트.pdf";
            _localFilePathText.text = _localFilePath;
            _urlInputField.text = "";
        }
        private void OnClearLocalFile()
        {
            _localFilePath = null;
            _localFilePathText.text = "";
        }

        private void OnUrlInputChanged(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                _localFilePath = null;
                _localFilePathText.text = "";
            }
        }

        private void OnLoad()
        {
            if (_controller.IsReceiving) return;  // 수신 중이면 무시

            bool allowDownload = _allowDownloadToggle.isOn;

            // 로컬 경로가 있으면 로컬 우선
            if (!string.IsNullOrEmpty(_localFilePath))
            {
                FileType type = FileType.PDF; // TODO: 확장자로 판별
                // 로컬 파일 로드(`SendFileFromLocalstore`)도 "SendFileFromUrl"와 동일한 타이밍 문제가 있으나,
                // 동기 호출이라 실제로는 완료 시점이 같아 문제가 없다.
                FileChunkSender.SendFileFromLocalstore(_controller, _localFilePath, type, allowDownload);
                _isPresenter = true;
                return;
            }

            // 아니면 URL
            string url = _urlInputField.text;
            if (!string.IsNullOrEmpty(url))
            {
                FileType type = FileType.PDF; // TODO: 확장자로 판별
                StartCoroutine(FileChunkSender.SendFileFromUrl(_controller, url, type, allowDownload));
                return;
            }
        }

        // 이벤트 핸들러 — 페이지 조작
        private void OnOpenView()
        {
            Debug.Log($"[SharedScreenUI] OpenView - TotalPageCount: {_controller.TotalPageCount}, CurrentPage: {_controller.CurrentPage}, FileBuffer null: {_controller.FileBuffer == null}");

            // 발표자 현재 페이지에서 시작
            _localPage = _controller.CurrentPage;
            ShowViewPanel();
            RenderLocal(); // ViewPanel 열 때 현재 페이지 렌더
        }

        private void OnPrevPage()
        {
            if (_isPresenter)
            {
                if (_controller.IsReceiving) return;
                _controller.RequestPrevPage();
            }
            else
            {
                // 관객: 로컬 독립 조작
                if (_localPage > 0)
                {
                    _localPage--;
                    RenderLocal();
                }
            }
        }

        private void OnNextPage()
        {
            if (_isPresenter)
            {
                if (_controller.IsReceiving) return;
                _controller.RequestNextPage();
            }
            else
            {
                if (_localPage < _controller.TotalPageCount - 1)
                {
                    _localPage++;
                    RenderLocal();
                }
            }
        }

        private void OnSliderChanged(float value)
        {
            int page = Mathf.RoundToInt(value);
            if (_isPresenter)
            {
                _controller.CmdChangePage(page);
            }
            else
            {
                _localPage = page;
                RenderLocal();
            }
        }

        private void OnPageInputChanged(string text)
        {
            // 빈 문자열 크래시 방지
            if (!int.TryParse(text, out int displayPage)) return;
            int page = Mathf.Clamp(displayPage - 1, 0, _controller.TotalPageCount - 1); // 표시값 → 내부값

            if (_isPresenter)
            {
                _controller.CmdChangePage(page);
            }
            else
            {
                _localPage = page;
                RenderLocal();
            }
        }

        // 이벤트 핸들러 — 닫기/종료/저장
        private void OnCloseFileLoad()
        {
            _fileLoadPanel.SetActive(false);
        }

        private void OnCloseView()
        {
            // ViewPanel만 숨김 (발표/열람 상태 유지)
            ShowOpenViewButton();
        }

        private void OnEndPresent()
        {
            _controller.CmdReleasePresenter();
            _isPresenter = false;
            ShowFileLoadPanel();
        }

        private void OnSave()
        {
            // TODO: 2차 — 파일 저장 다이얼로그
            Debug.Log("[SharedScreenUI] 저장 기능은 2차 구현 예정");
        }

        // Controller 이벤트 콜백
        private void OnPresenterChanged(uint newPresenterId)
        {
            uint myNetId = Mirror.NetworkClient.localPlayer?.netId ?? 0;
            _isPresenter = (newPresenterId == myNetId);

            // 발표자가 해제되면 관객도 FileLoadPanel으로
            if (newPresenterId == 0)
                ShowFileLoadPanel();
        }

        // 관객이 ViewPanel을 이미 보고 있는 중에 발표자가 다운로드 허용을 변경했을 때 실시간 갱신
        private void OnAllowDownloadChanged(bool allowDownload) 
        {
            // 관객의 저장 버튼 갱신
            if (!_isPresenter)
                _saveButton.gameObject.SetActive(allowDownload);
        }

        private void OnFileReceived()
        {
            uint myNetId = Mirror.NetworkClient.localPlayer?.netId ?? 0;
            _isPresenter = _controller.IsPresenter(myNetId);
            _localPage = 0;
            RenderLocal();  // ← 첫 페이지를 _pageImage에 렌더
            ShowViewPanel();
        }

        private void OnPageChanged(int page)
        {
            if (!_isPresenter) return;  // 관객은 독립 조작이니까 무시
            _localPage = page;
            RenderLocal();
        }


        // 로컬 렌더 + 페이지 UI 갱신
        private void RenderLocal()
        {
            byte[] buffer = _controller.FileBuffer;
            if (buffer == null) return;

            Texture2D tex = SharedScreenRenderer.Render(buffer, _controller.CurrentFileType, _localPage);
            if (tex != null)
            {
                // 이전 텍스처 해제
                if (_pageImage.texture != null)
                {
                    Destroy(_pageImage.texture);
                    _pageImage.texture = null;
                }

                _pageImage.texture = tex;
            }

            UpdatePageUI();
        }

        private void UpdatePageUI()
        {
            int page = _isPresenter ? _controller.CurrentPage : _localPage;
            int total = _controller.TotalPageCount;

            _pageSlider.maxValue = Mathf.Max(0, total - 1);
            _pageSlider.SetValueWithoutNotify(page);
            _pageInputField.SetTextWithoutNotify((page + 1).ToString()); // 표시는 1부터
            _totalPageText.text = $"/ {total}";
        }
    }
}