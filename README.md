# MySecondWorld

**Unity 6 기반 크로스플랫폼 멀티플레이어 액션 RPG** — 1인 개발

서버 권위 네트워킹, 음성으로 대화하는 AI NPC, 552개 모듈러 파츠 커스터마이징, 18개 어셈블리(asmdef)로 분리한 구조를 직접 설계하고 구현했습니다. PC와 Android를 지원하고, 서버는 AWS EC2의 Linux 헤드리스 빌드로 돌립니다.

| | |
|---|---|
| 📄 기술 문서 (Notion) | [MySecondWorld 포트폴리오 기술서](https://scythe-lavender-093.notion.site/2026-3d4c12defa4980f994a8da0cf10573a0) |
| 🏗️ 아키텍처 도면 (13장) | [도면 바로 보기](https://viewer.diagrams.net/?lightbox=1&nav=1&layers=1&title=MySecondWorld#Uhttps%3A%2F%2Fraw.githubusercontent.com%2FBAESOOHYUN%2FMySecondWorld-Architecture%2Fmain%2Farchitecture.drawio) · [도면 저장소](https://github.com/BAESOOHYUN/MySecondWorld-Architecture) |
| 🎥 플레이 영상 | [YouTube 재생목록](https://youtube.com/playlist?list=PLbzoJ0fZJVF8&si=b4vfPgADLcIZXj6A) |

---

## 핵심 기술 5건

각 항목의 문제 → 원인 → 해결 → 검증 과정은 Notion 기술 문서에 정리해 두었습니다.

| 주제 | 한 줄 요약 |
|---|---|
| **던전 룸 인스턴싱과 유령 몬스터** | 씬 하나를 4인 방으로 나누는 `MatchInterestManagement`(마비노기 모바일 ‘우연한 만남’ 모티브). 유령 몬스터 피격은 "서버가 확정한 소속 씬을 클라이언트에 알리는 경로"가 없던 것이 원인이라, 메시지를 신설하고 스폰 오브젝트를 해당 씬으로 옮겨 해결 |
| **대화형 AI NPC** | 음성 입력(STT) → LLM → TTS → 립싱크 → 영속 기억을 한 흐름으로 연결. 문장 단위로 잘라 TTS를 미리 요청해 첫 소리까지 중앙값 약 1.1초(에디터, 10회 측정) |
| **4축 모듈러 스킬 시스템과 18개 asmdef** | Strategy + Composite로 스킬을 4개 축으로 조립. 어셈블리를 18개로 나눠 참조 방향을 한쪽으로 고정하고 순환 참조를 해소 |
| **아바타 동기화와 552개 모듈러 파츠** | 데디서버에서 아바타가 사라지던 버그의 원인이 VContainer DI 주입 누락임을 규명. 552개 파츠 파이프라인과 0ms 클라이언트 예측 |
| **대화면 파일 공유** | Mirror 패킷 한계로 64KB 시도 후 크래시를 발견해 12,000바이트 청크로 조정. Outer Zone 관심 영역 기반 공유, 전송 중 입장자 처리, 서버 입력 검증 |

### 그 밖에 직접 구현한 것

| 영역 | 내용 |
|---|---|
| **터치 입력 · 카메라** | New Input System 위에 터치 입력(`TouchEventListener`)을 직접 구현해 UI 위 터치와 월드 터치를 가르고(`OnUIChecker`), Cinemachine 3의 `OrbitalFollow`·`FollowZoom`을 코드로 제어해 PC·모바일의 회전·줌 속도를 따로 조절. NPC 대화용 카메라도 별도 구성 |
| **애니메이션** | 이동·점프·착지를 이벤트로 Animator에 전달(`MovementController`는 Animator를 모름), 속도 하나로 Idle·Walk·Sprint를 잇는 1D Blend Tree. 무기마다 `AnimatorOverrideController` 5종(한손·양손·장병기·활·캐스터)을 `WeaponAnimationMapSO`로 매핑해 장착 시 교체하고, 궁수는 **Avatar Mask + UpperBody 레이어**로 상체(조준)와 하체(이동)를 분리 |
| **컴패니언 · 몬스터 AI** | 컴패니언은 플레이어를 따라다니다(Follow) 몬스터를 감지하면 추격(Chase)·공격(Attack)하는 자동 사냥형. 몬스터는 Idle → Chase → Attack → HitStun 상태 머신에 보스 패턴, 웨이브 스폰. 모두 Stateless 상태 머신 + NavMesh |
| **렌더링 최적화** | 모바일 기준 점검(메시 LOD·HLOD, ASTC 텍스처 압축, 섀도우·오버드로우), SpriteAtlas, 오브젝트 풀링 |
| **무기 장착 · 인벤토리** | 인벤토리에서 무기를 장착하면 서버 검증 후 소켓(손·손목·손가락·목)에 모델을 붙이고, 인벤토리는 서버 세이브에 저장·복원. 무기 255종은 CSV에서 에디터 툴로 일괄 생성 |
| **그 밖** | 접속·인증·채팅, 포탈 기반 필드↔던전 전환 |

---

## 기술 스택

| 분류 | 기술 · 라이브러리 |
|---|---|
| 엔진·렌더링 | Unity 6, URP, Addressables, AI Navigation |
| 입력·카메라 | New Input System, Reactive Input System, Cinemachine |
| 네트워킹 | Mirror, Unity Dedicated Server 패키지 |
| 구조·DI | VContainer, MessagePipe, 18개 `.asmdef` |
| 비동기·반응형 | UniTask, R3 |
| 문자열·직렬화 | ZString, Newtonsoft.Json |
| AI | LLM for Unity(llama.cpp, Gemma 3 4B), whisper.unity(로컬 STT), Groq(LLM·Whisper STT), Typecast(TTS), NativeWebSocket |
| PDF·파일 | Paroxe PDFRenderer, SimpleFileBrowser, NativeFilePicker |
| 서버·배포 | AWS EC2(Linux 헤드리스 서버), nginx(번들 호스팅), systemd, 원클릭 배포 메뉴 |
| 개발 도구 | ParrelSync(멀티 클라이언트 테스트), NuGetForUnity |
| 플랫폼 | Windows, Android(APK) |

---

## 구조 한눈에

- **어셈블리**: 참조는 위에서 아래로만 흐릅니다(조립 루트 → 화면 → AI·전투 → 네트워크 → 기능 모듈 → 기반). 순환 참조는 인터페이스를 아래 계층에 두는 방식으로 풀었습니다. → 도면 3·4장
- **DI**: `ProjectLifetimeScope` → `NPCLifetimeScope` → `NPCScope`로 이어지는 VContainer 스코프 계층. 서버와 클라이언트가 함께 쓰는 스코프에는 한쪽 전용 객체를 주입하지 않는 규칙을 둡니다. → 도면 2장
- **AI NPC**: `UseServerMode`로 로컬 LLM(Gemma)과 클라우드 LLM(Groq)을 같은 `ILLMService` 인터페이스 뒤에서 바꿉니다. TTS도 `ITTSService`로 추상화해서, Edge TTS가 HTTP 403으로 막혔을 때 구현체 1개와 등록 한 줄 교체로 Typecast로 바꿨습니다. → 도면 6장
- **빌드·배포**: 번들 빌드 → 서버 빌드 → EC2에 업로드 → 서비스 재시작까지 메뉴 하나로 실행합니다. → 도면 13장

전체 13장의 도면(전체 구조 · DI 스코프 · 어셈블리 · 네트워크 · AI NPC · 전투 · 몬스터 · 인벤토리 · 입력/카메라/UI · 쉐어드 스크린 · 렌더링/풀링 · 빌드/배포)은 [여기서 넘겨 볼 수 있습니다](https://viewer.diagrams.net/?lightbox=1&nav=1&layers=1&title=MySecondWorld#Uhttps%3A%2F%2Fraw.githubusercontent.com%2FBAESOOHYUN%2FMySecondWorld-Architecture%2Fmain%2Farchitecture.drawio).

---

## 개발 방식

1인 개발에서 구조가 무너지는 것을 막으려고, 기능마다 **사전 설계(Plan · Context · Checklist) → 구현·디버깅 → QA 검증 → 문서화** 4단계를 지켰습니다. 해결 과정과 아키텍처 결정은 Notion에 세션별로 기록했습니다.

---

## 알아 두실 점

- 유료 서드파티 에셋(`Assets/External/`)은 이 저장소에 포함하지 않아, 클론만으로는 그대로 빌드되지 않을 수 있습니다. 사용한 에셋: Cleverous Vault(인벤토리), Kevin Iglesias·Everyday Motion Pack(애니메이션), Layer Lab(UI), Joystick Pack, FlexibleColorPicker 등.
- API 키는 저장소에 없습니다. `Assets/Resources/Secrets/*.txt`(gitignore)에 로컬로 넣어 `SecretKeyLoader`가 읽습니다.
- TTS 음성은 [Typecast](https://typecast.ai)를 사용합니다.

---

## Author

**배수현 (Bae Soohyun)** — Unity 게임 프로그래머 (클라이언트 / 서버)
