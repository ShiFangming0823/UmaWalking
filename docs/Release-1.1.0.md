# UmaWalking 1.1.0

## 简体中文

- 新增全桌面正交地面：无需绘制地面，在整个主屏幕漫步，保持固定投影尺寸，从任意屏幕边缘进入或退出。
- 改进正交地面实验性避让的朝向稳定性，减少相向或拥挤时的高频左右摆头。
- 修复 Issue #1 报告的 Win11 24H2 桌面挂载兼容问题：区分桌面根窗口与模型父窗口，自动选择合适的挂载位置。设置新增 Auto／Root／IconView 手动兼容选项。
- 完善窗口重建及桌面诊断信息；保留桌面遮挡暂停、点击、拖拽和原有地面模式。

已通过本机 Windows 10 的逻辑、窗口、真实模型及发布包回归测试。Win11 24H2 原环境的实际显示仍需报告者确认，不能用本机测试替代。运行版不含游戏素材。请完整解压 Windows-x64 ZIP，退出旧版后启动新版；原用户配置独立保留。旧版自定义地面不会自动改为正交模式，请在面板中自行选择。

## English

- Add Full desktop · Orthographic ground: roam the primary desktop at a constant projected scale, entering and exiting along any edge, without drawing a ground.
- Stabilize experimental orthographic avoidance headings to reduce rapid left/right oscillation.
- Address the desktop-parent compatibility issue reported in #1 on Windows 11 24H2. Separate the desktop root from the actor parent and select adaptively; provide Auto/Root/IconView overrides in Settings.
- Improve parent recreation and diagnostics while preserving coverage pause, clicks, dragging and existing grounds.

Local Windows 10 logic, native-window, real-model and release-package regression checks passed. Actual visibility on the reporting Windows 11 24H2 environment still requires confirmation. No game assets are included. Extract the complete Windows-x64 ZIP and exit the older version before launching. Existing per-user settings are retained; choose the new ground mode explicitly.

## 日本語

- 全デスクトップ正投影平面を追加。地面を描かず、主画面全域を一定の投影サイズで移動し、任意の辺から入退場します。
- 正投影モードの実験的な回避処理を安定化し、向きが小刻みに左右へ揺れる現象を軽減。
- Issue #1のWindows 11 24H2互換問題に対応。デスクトップのルートとモデルの親ウィンドウを分離し、自動選択。設定にAuto／Root／IconViewを追加。
- 親ウィンドウ再作成と診断を改善。遮蔽時の一時停止、クリック、ドラッグ、従来の地面を維持。

Windows 10でロジック、ネイティブウィンドウ、実モデルおよび配布パッケージの回帰テストを実施しました。報告者のWindows 11 24H2環境で実際に表示されるかは再確認が必要です。ゲーム素材は含みません。Windows-x64 ZIPをすべて展開し、旧版を終了してから起動してください。ユーザー設定は引き継がれ、新モードはパネルで選択できます。
