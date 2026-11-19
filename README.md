# MySecondWorld

**Unity 6 기반 크로스플랫폼 서버 권위 3D 멀티플레이어 액션 RPG** — 1인 개발 (2026.01 ~ 2026.11)

AWS EC2(Ubuntu Linux) 환경에서 24/7 헤드리스 데디케이티드 서버(Dedicated Server)를 직접 운용하며, Windows(PC)와 Android(APK, 구글 플레이 비공개 테스트 완료) 크로스플랫폼을 지원합니다.  
서버 권위 네트워킹, 0.9초 저지연 음성 대화형 AI NPC, 4축 모듈러 스킬 시스템, 552개 모듈러 파츠 외형 동기화, 18개 런타임 어셈블리(asmdef) 모듈화 구조를 직접 설계하고 구현했습니다.

| 구분 | 링크 |
|---|---|
| 📄 **포트폴리오 기술 문서 (Notion)** | [MySecondWorld 포트폴리오 기술 명세서](https://app.notion.com/p/MySecondWorld-3D-RPG-419c12defa4981838af7d853933281a5) |
| 🏗️ **시스템 아키텍처 도면 (15장)** | [draw.io 도면 바로보기](https://viewer.diagrams.net/?lightbox=1&nav=1&layers=1&title=MySecondWorld#Uhttps%3A%2F%2Fraw.githubusercontent.com%2FBAESOOHYUN%2FMySecondWorld-Architecture%2Fmain%2Farchitecture.drawio) · [도면 GitHub 저장소](https://github.com/BAESOOHYUN/MySecondWorld-Architecture) |
| 🎬 **시연 및 개발 영상** | [YouTube 플레이리스트 바로가기](https://youtube.com/playlist?list=PLbzoJ0fZJVF8&si=ITH6kSl-xUE1n3N-) |

---

## 🏆 핵심 기술 5건 (Core Tech)

각 항목의 문제 분석, 근본 원인 규명, 해결 코드 및 실환경 검증 내역은 Notion 기술 문서에서 확인하실 수 있습니다.

| 주제 | 핵심 내용 및 엔지니어링 성과 |
|---|---|
| **심리스 기반 4인 룸 인스턴싱 & 공간 분할**<br>*(모바일 마비노기 '우연한 만남' 모티브)* | Unity의 동일 씬 Additive 중복 로드 불가 한계를 극복하기 위해 `matchId(Guid)` 기반 `MatchInterestManagement`로 동일 씬 내 4인 방 단위 트래픽을 O(1) 격리.<br>씬 전환 시 발생하던 "유령 몬스터 피격" 버그의 원인(서버가 확정한 소속 씬을 클라이언트에 통보하지 않아 클라이언트가 이전 씬 객체로 처리)을 규명하고, 전용 통보 메시지 신설 및 스폰 오브젝트 씬 이전으로 해결. |
| **0.9초 음성 파이프라인과 서버 영속 기억 — AI NPC** | 마이크 음성 입력(STT) → LLM 응답(Groq는 전체 응답 수신, 로컬 LLM은 토큰 스트리밍) → 문장 종결자 단위 분할 & TTS 비동기 프리페치 큐(`Queue<UniTask<AudioClip>>`) → 256샘플 진폭 RMS 기반 4단계 머티리얼 스왑 립싱크.<br>STT 이후 질문 → 첫 소리 예약까지 중앙값 약 0.9초(EC2 서버 접속 Client 실측) 및 문장 간 무음(Gapless) 0초 달성.<br>온디바이스 C++ P/Invoke 생명주기 가드로 `0xC0000005` 메모리 크래시를 방어하고 Groq 클라우드 API와의 런타임 하이브리드 분기 구축.<br>10쌍 대화 히스토리 및 친밀도를 `NPCMemorySO` 캐시와 서버 세이브(`SaveData.npcMemories`)로 씬/세션 간 영속화 및 상점 추천 거래 서버 권위 검증. |
| **기획·아트를 위한 노코드(No-Code) 제작 환경 — 4축 모듈러 스킬 시스템** | 발동 조건(Trigger) · 타겟팅(Targeting) · 효과(Effect) · 시퀀스(Sequence)를 Strategy + Composite 패턴으로 직교 분리하여 ScriptableObject 조합만으로 신규 스킬을 제작하는 환경 구축.<br>UI Toolkit 기반 3단 분할 커스텀 에디터(`SkillEditorWindow`, 타임라인, 프리뷰) 구현.<br>서버는 SphereCast 스윕 판정 및 타격 판정만 권위 처리하고, 클라이언트는 타임라인 연출을 비동기 재생. |
| **552개 모듈러 파츠 동기화와 0ms 클라이언트 예측 — 서버 권위 아바타** | 외형 원본(SSOT)을 서버 세이브로 일원화하고, 네트워크 RTT(50~100ms) 지연을 감추기 위해 장착 즉시 화면에 반영하는 0ms 클라이언트 예측 렌더링 적용.<br>데디서버 환경에서 아바타가 사라지던 원인이 VContainer DI 주입 누락임을 규명하여 서버 안전망 구축.<br>장착 연타 시 낡은 서버 응답이 덮어쓰며 생기던 깜빡임(소멸/역행) 버그를 인벤토리 슬롯 번호 기반 신원 체계와 타임스탬프 필터링으로 해결. |
| **12KB 청크 스트리밍과 실시간 대화면 파일 공유** | Mirror 패킷 크기 한계(64KB)로 인한 크래시를 분석하여 12,000바이트(12KB) 청크 슬라이싱 스트리밍으로 극복.<br>Outer Zone 관심 영역(AOI) 분할로 불필요한 클라이언트 트래픽을 차단하고, 지각 입장자(Late-Joiner)를 위한 서버 캐시 동기화 및 악성 파일/확장자 검증 구현. |

---

## 🏛️ 아키텍처 및 엔지니어링 표준

### 1. 개발 표준 프로세스 (4-Step Engineering SOP)
1인 대규모 개발 환경에서 발생할 수 있는 아키텍처 붕괴와 기술 부채를 방어하기 위해 엄격한 4단계 개발 표준을 준수했습니다:
- **1단계 (사전 설계):** 플랜(Plan) / 컨텍스트(Context) / 체크리스트(Checklist)를 문서화하여 요구사항과 제약 조건을 확정
- **2단계 (구현 및 디버깅):** 엔진 저수준 메커니즘 분석 및 가설 교차 검증을 통한 견고한 코드 구현
- **3단계 (품질 검증):** 사전 작성된 테스트 시나리오 기반의 QA 문서를 통해 기능 정상 작동 및 부작용(Side-effect) 검증
- **4단계 (지식 자산화):** 트러블슈팅 과정과 아키텍처 결정을 노션(Notion)에 영구 아카이빙

### 2. 모듈러 어셈블리 분리 (18개 Runtime + 1개 Editor `.asmdef`)
- **단방향 의존성:** `Foundation`(중립 인터페이스/공유 구조체) 계층을 두어 상위 계층 간 순환 참조를 원천 차단하고 컴파일 시간을 단축했습니다.
- **주요 어셈블리:**
  - `MySecondWorld.Foundation`: 공용 인터페이스 및 코어 정의
  - `MySecondWorld.Network`: Mirror 네트워크 패킷, 동기화, 존 관리
  - `MySecondWorld.AI`: 대화형 AI NPC, 립싱크, 컴패니언 및 몬스터 FSM
  - `MySecondWorld.Combat`: 4축 스킬 시스템 및 전투 판정 로직
  - `MySecondWorld.Customization`: 552개 모듈러 파츠 본 리매핑 및 외형 데이터
  - `MySecondWorld.UI`, `MySecondWorld.Inventory`, `MySecondWorld.Camera`, `MySecondWorld.Input` 등

### 3. VContainer 순수 의존성 주입 (Pure DI)
- `ProjectLifetimeScope` → `NPCLifetimeScope` → `NPCScope`로 이어지는 계층형 스코프 설계.
- 물리 마이크 등 싱글톤 자원과 개별 NPC 인스턴스 자원을 분리하여 멀티플레이 환경에서의 크로스토크(Cross-talk)를 3중 방어.
- 서버와 클라이언트가 공유하는 스코프에는 한쪽 전용 객체를 주입하지 않는 원칙을 적용.

### 4. 반응형 비동기 & 메모리 최적화
- **UniTask & R3:** UI 이벤트 및 네트워크 비동기 흐름을 Reactive Stream으로 처리하여 가비지 발생 최소화.
- **ZString:** 런타임 문자열 조합 시 GC Alloc을 0으로 억제.
- **Stateless:** 상태 전이 규칙을 선언적으로 정의한 FSM 적용 (몬스터 AI 및 전투 상태 관리).

---

## 🛠️ 커스텀 에디터 도구 및 배포 자동화 (20종+)

1인 개발 생산성을 극대화하기 위해 에디터 확장 도구군을 직접 제작하여 운용했습니다:

- **원클릭 서버 빌드 & 배포 (`ServerBuildTool`):**  
  Linux Headless 데디케이티드 서버 바이너리와 Addressables 번들을 빌드하고, SCP/SSH를 통해 AWS EC2 인스턴스로 자동 전송한 뒤 `systemd` 서비스를 무중단 재시작.
- **로컬 번들 호스팅 서버 (`LocalBundleServerLauncher`):**  
  Unity 6에서 제거된 'Addressables Hosting Services'를 대체하기 위해 에디터 컴파일 완료 시 8080 포트 기반 로컬 HTTP 파일 서버를 자동 기동.
- **UI Toolkit 스킬 에디터 (`SkillEditorWindow`):**  
  스킬 부품 보관함, 타임라인 뷰어, 실시간 프리뷰를 갖춘 비주얼 스킬 저작 도구.
- **데이터 파이프라인 자동화:**  
  CSV 기획 데이터를 ScriptableObject로 일괄 변환하는 파서, 모듈러 파츠 본 베이커, UI 바인딩 자동화 도구 등.

---

## 💻 기술 스택

| 분류 | 기술 및 라이브러리 |
|---|---|
| **엔진 & 렌더링** | Unity 6 (6000.0.x), Universal Render Pipeline (URP), Addressables, AI Navigation |
| **네트워크 & 동기화** | Mirror Networking, Unity Dedicated Server 패키지, KCP Transport |
| **구조 & 아키텍처** | VContainer (Pure DI), MessagePipe, 18개 Runtime + 1개 Editor `.asmdef` |
| **비동기 & 반응형** | UniTask, R3, Stateless (FSM) |
| **성능 최적화 & 직렬화** | ZString (Zero GC String), Newtonsoft.Json |
| **차세대 AI 파이프라인** | Groq API (Llama 3.3 / Groq Whisper), Typecast TTS, whisper.unity (로컬 STT), LLM for Unity (로컬 Gemma 4B GGUF), NativeWebSocket |
| **미디어 & 파일 I/O** | Paroxe PDFRenderer, SimpleFileBrowser, NativeFilePicker |
| **인프라 & DevOps** | AWS EC2 (Ubuntu Linux c5a.large 헤드리스), nginx, systemd, SSH/SCP 자동 배포 |
| **개발 & 테스트 도구** | ParrelSync (에디터 멀티 클라이언트 테스트), NuGetForUnity |
| **지원 플랫폼** | Windows (PC), Android (APK) |

---

## ⚠️ 안내 사항 (Transparency)

- **에셋 저작권 보호:** 유료 서드파티 에셋(`Assets/External/`)은 라이선스 보호를 위해 저장소에 포함되어 있지 않습니다. 따라서 저장소 클론만으로는 즉시 빌드가 되지 않을 수 있습니다.
- **보안 키 관리:** 상용 AI API Key 및 서버 접속 키는 저장소에 커밋되지 않으며, `Assets/Resources/Secrets/` 경로(gitignore)를 통해 로컬에서 안전하게 로드됩니다.
- **실측치 기재 기준:** 
  - AI NPC 응답 속도: EC2 서버 접속 모바일/PC 클라이언트 실측 중앙값 약 0.9초.
  - 파일 스트리밍 청크: Mirror 전송 안정성을 고려해 12,000바이트(12KB)로 실측 조정.
  - 모듈러 파츠: 파츠 파이프라인 기준 552개.
  - 어셈블리: 자체 도메인 런타임 18개 + 에디터 1개.

---

## 👤 Author

**배수현 (Bae Soohyun)**  
- Role: Game Programmer (Client / Server)  
- Contact: bsh9277@naver.com  
- Portfolio: [Notion 포트폴리오 바로가기](https://app.notion.com/p/MySecondWorld-3D-RPG-419c12defa4981838af7d853933281a5)
