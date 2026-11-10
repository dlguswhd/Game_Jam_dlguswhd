# KILL OR DEAD : Black Ledger

Unity 6000.3.27f1 / URP 기반 잠입 FPS 프로젝트.

## 프로젝트 열기

1. Git LFS를 설치한 뒤 저장소를 복제합니다. 이미 복제한 경우 저장소에서 `git lfs install`과 `git lfs pull`을 실행합니다.
2. Unity Hub에서 Unity **6000.3.27f1**로 프로젝트를 엽니다.
3. Unity가 에셋을 임포트할 때까지 기다립니다.
4. `Assets/Scenes/Title.unity`를 열고 실행합니다.

프로젝트 루트는 `Assets`, `Packages`, `ProjectSettings`가 바로 들어 있는 폴더입니다. 이 PC에서는 **`C:\GitHub\Game_Jam_dlguswhd`**를 Unity Hub에서 열면 됩니다.

GitHub Desktop에 기존 프로젝트를 등록할 때는 **File → Add local repository**에서 위 폴더를 선택하세요. 이미 있는 프로젝트 폴더 안에 같은 저장소를 다시 Clone하면 `Game_Jam_dlguswhd\Game_Jam_dlguswhd`처럼 중복 프로젝트가 생깁니다. 새로 복제할 때는 상위 저장 위치를 `C:\GitHub`로 선택해 한 단계의 프로젝트 폴더만 만드세요.

KINEMATION Tactical Shooter Pack, Abandoned Asylum, Gang members - characters pack, Flooded Grounds와 차량 모델은 모두 저장소에 포함되어 있습니다. `.meta` 파일과 KINEMATION 내부 수정본도 함께 포함되어 있어 팀원이 같은 파일을 내려받을 수 있습니다. 원본 에셋을 다시 임포트하면 프로젝트에서 수정한 코드·프리팹이 덮어써질 수 있으므로 저장소의 수정본을 기준으로 작업하세요.

대용량 텍스처·모델·오디오는 Git LFS로 관리합니다. GitHub의 ZIP 다운로드 대신 Git LFS가 설치된 Git으로 복제하세요. 이전에 복제한 팀원은 다음 명령으로 원본 에셋까지 받습니다.

```sh
git lfs install
git pull
git lfs pull
```

LFS 파일이 작은 텍스트 포인터 상태로 남아 있으면 Unity에서 원본 에셋을 읽지 못합니다. 이 경우 `git lfs pull`이 오류 없이 완료되는지 먼저 확인하세요.

## 게임 흐름

타이틀 → 작전 브리핑 → 폐허 마을 → 폐병원에서 열쇠카드·장부 회수 → 차량으로 탈출.

문 앞에서는 휠로 문 열기·내시경을 선택하고 F로 실행합니다. 내시경은 A/D로 좌우 이동, 마우스로 둘러보기, F로 종료합니다. 얇은 문·벽 관통 사격과 문 탄흔을 지원합니다.

상세 조작법·구현 상태·알려진 이슈는 [진행상황.md](진행상황.md), 맵 구성은 [레벨디자인.md](레벨디자인.md)를 참고하세요.
