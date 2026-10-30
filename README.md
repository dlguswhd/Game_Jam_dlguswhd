# KILL OR DEAD : Black Ledger

Unity 6000.3.27f1 / URP 기반 잠입 FPS 프로젝트.

## 프로젝트 열기

1. Git LFS를 설치한 뒤 저장소를 복제합니다. 이미 복제한 경우 저장소에서 `git lfs install`과 `git lfs pull`을 실행합니다.
2. Unity Hub에서 Unity **6000.3.27f1**로 프로젝트를 엽니다.
3. 저장소에서 제외된 다음 에셋을 원래 경로로 임포트합니다.
   - `Assets/KINEMATION` — KINEMATION Tactical Shooter Pack
   - `Assets/Abandoned_Asylum` — Abandoned Asylum
   - `Assets/Gang members - characters pack` — Gang members - characters pack
4. [진행상황.md](진행상황.md)의 **6장·13장 KINEMATION 원본 수정 사항**을 적용합니다. 해당 에셋 폴더는 Git에서 제외되어 원본 수정이 자동으로 복원되지 않습니다.
5. `Assets/Scenes/Title.unity`를 열고 실행합니다.

Flooded Grounds 에셋과 차량 모델은 이 저장소에 포함되어 있습니다. 대용량 `.tif` 텍스처와 `.blend` 차량 모델은 Git LFS로 관리합니다.

## 게임 흐름

타이틀 → 작전 브리핑 → 폐허 마을 → 폐병원에서 열쇠카드·장부 회수 → 차량으로 탈출.

문 앞에서는 휠로 문 열기·내시경을 선택하고 F로 실행합니다. 내시경은 A/D로 좌우 이동, 마우스로 둘러보기, F로 종료합니다. 얇은 문·벽 관통 사격과 문 탄흔을 지원합니다.

상세 조작법·구현 상태·알려진 이슈는 [진행상황.md](진행상황.md), 맵 구성은 [레벨디자인.md](레벨디자인.md)를 참고하세요.
