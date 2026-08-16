# Phyzzle Unity migration

현재 마이그레이션의 첫 번째 목표는 C++ 자체 엔진 버전의 플레이 감각과 규칙을 먼저 재현하는 것이다. 이 단계에서는 플레이어 이동/카메라, Attach, Rewind를 서로 분리된 Unity 컴포넌트로 구현했다.

## 실행

`Assets/Phyzzle/Scenes/PlayerMigrationSandbox.unity`를 열고 Play한다. 씬을 다시 생성하려면 Unity 메뉴에서 `Phyzzle > Migration > Create Player Sandbox`를 실행한다.

Play하지 않고 HUD를 확인하려면 `Phyzzle > Migration > HUD Preview`에서 상태를 고른다. `Attach Target`, `Hold Island`, `Rotate Island`, `Can Attach`, `Rewind Target` 등을 즉시 Game View에서 볼 수 있다. 현재 씬에는 `Attach Target` 프리뷰가 저장되어 있다.

## 기본 입력

- 이동: `WASD` / 게임패드 왼쪽 스틱
- 카메라: 방향키 / 게임패드 오른쪽 스틱
- 점프: `Space` / A
- Attach 선택: `1`, Rewind 선택: `2`
- 선택한 능력 진입: `Q` / LB
- 대상 확정 및 결합: `F` / B
- 취소 또는 놓기: `Space`, `Z`, `Q` / A, X, LB
- Attach 상태에서 분리: `R` / Y
- Attach 물체 이동: 카메라 입력, 게임패드 D-pad
- Attach 물체 회전: `E` / RB를 누른 채 게임패드 D-pad

Rewind 대상을 고르는 동안에는 원본과 같이 월드 시간이 멈추며 카메라와 입력은 계속 동작한다. 대상이 2개 이상의 물리 스냅샷을 가진 경우 `F` / B로 최대 5초간 역재생한다.

## 능력 HUD

- 원본 C++ 프로젝트의 조준선과 게임패드 PNG 10개를 `UI/Legacy`에 Sprite로 이식했다.
- 마지막으로 수행된 입력이 키보드인지 게임패드인지 감지하여 해당 안내 UI로 자동 전환한다.
- 게임패드 UI는 원본 PNG와 1920x1080 배치를 사용한다.
- 키보드 UI는 교체용 이미지가 준비될 때까지 텍스트 키캡을 사용한다.
- `CanvasScaler`와 `PlayerHudSafeArea`가 16:9, 16:10, 울트라와이드와 안전 영역을 처리한다.
- HUD는 Attach/Rewind 상태를 읽기만 한다. 점프, 접지, 타기팅, 취소, 결합 규칙은 변경하지 않았다.

## 결정이 필요한 원본 조작 차이

- 원본 HUD의 `Stick_Off_X`는 X 분리를 안내하며 임시 키보드 HUD도 이에 맞춰 `Z`를 표시한다.
- 현재 Unity `AttachAbilityController`의 실제 `DetachHeldObject` 호출은 `ActionY`, 즉 `R` / Y에 연결되어 있다. `Z` / X는 홀드 종료로 처리된다.
- 어느 쪽이 의도된 최종 규칙인지 임의 판단하지 않고 현재 상태를 유지했다. 결정 후 입력 또는 HUD 한쪽을 맞춰야 한다.
- 원본 씬의 `rotationArow`는 그래픽 컴포넌트가 없는 빈 오브젝트다. Unity도 같은 빈 표시 루트만 이식했으며 새 이미지는 임의 제작하지 않았다.

## 코드 경계

- `Runtime/Player`: 입력, 이동, 접지, 카메라, 애니메이션 연결
- `Runtime/Abilities/Attach`: 탐색, 홀드 스프링, 회전 스냅, 결합 그래프, Joint 생성
- `Runtime/Abilities/Rewind`: 고정 크기 기록 버퍼, 대상 탐색, 역재생, 물리 상태 복원
- `Runtime/Physics`: 기존 엔진의 실제 PhysX force-mode 매핑 호환 계층
- `Runtime/UI`: HUD 상태 해석, 최근 입력 장치 감지, View/Presenter, Safe Area
- `Editor/PlayerSandboxBuilder.cs`: 설정 에셋과 검증용 씬 자동 생성
- `Editor/PlayerHudBuilder.cs`: 원본 HUD 배치 생성과 에디터 프리뷰 메뉴
- `Tests`: 순수 계산 EditMode 테스트와 실제 Rigidbody PlayMode 테스트

## 다음 이식 범위

현재 샌드박스는 핵심 조작과 능력 HUD 검증용이다. 원본 콘텐츠 전체 동등성을 위해서는 `.pzscene` 자동 임포터, 게임 오브젝트별 컴포넌트 변환, 키보드 HUD 이미지, 아웃라인, 애니메이션 및 사운드 연결이 다음 단계로 남아 있다.
