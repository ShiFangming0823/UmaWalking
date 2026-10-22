> Historical implementation guide; current portable build instructions are in Build.md. Machine-specific paths and test attachments were removed.

# UmaWalking 3.16.0 源码与验证工具

3.16.0 新增 1～10 的全局模型上限、控制面板 UmaWalking 标题和默认关闭的实验性避让。`DesktopPopulationLimit.cs` 调整容量并安排超额角色退出；`DesktopLimits` 管理容量、Include 阈值及十个独立渲染层；`UmaWalkingPopulation.cs` 提供面板数量控件。`Tests/PopulationLimitTests.cs` 与 `Tests/verify_population_limits.py` 覆盖不同上限、队列、手动选择、换装、暂停、重启和自然轮换。

3.15.0 新增三维地面预测避让及持久化总开关。`DesktopAvoidance.cs` 为无 Unity 依赖的平面占地/速度采样与同时移动段校验；`DesktopPrototypeAvoidance.cs` 负责透视地面、暂停/拖拽/互动与路线衔接。开关位于“设置与诊断”。二维直线和默认底边不启用新规划器。

3.14.0 新增指定 JPG 启动背景和 136 个角色的离线中日英名称显示。`DesktopCharacterNames.cs` 是运行程序与控制面板共用的名称表；原日文身份不变。`Assets/DesktopSplash/Background.jpg` 由构建工具导入为完整 Sprite，设置原生启动背景并在构建结束后恢复上游工程原 PlayerSettings。

3.13.0 新增 Include 少量角色多服装实例：有效名单为 1～5 个角色时可重复角色、服装不重复，最多六只。`DesktopIncludeVariants.cs` 集中管理容量、可用服装与模式切换退场；`Tests/verify_include_variants.py` 验证真实六实例及其渲染和控制。

3.12.0 新增实时性能选项、诊断导出、数据目录切换、迷你素材依赖提取与中日英界面。数据提取复用资源索引，逐文件校验；成功切换到精简数据后，才允许通过用户确认删除原包。发布包仍不含游戏素材。

3.11.0 控制面板新增“立即退场”和“指定角色上场”：角色走向出口后渐隐，满六只时按顺序排队，成功上场后重置后续计时。入退场渐变覆盖默认底边、二维直线和三维地面。详情见《Manual-Entry-and-Exit.md》。


3.10.3 修复跨地面拖到右边缘后反复回弹及半透明卡住：地面限制双鞋水平占地，抬脚的竖直动画可正常播放；受阻后转向内部可达目标，活动时不套用通道淡出。详情见《Cross-Ground-Edge-Fix.md》。


3.10.2 以实际双鞋中心、步态鞋底接触高度和鞋部占地范围约束四点地面，拖动共用同一规则。过窄地面额外等比适配模型大小，不改保存的滑块值。详情见《Foot-Placement-and-Boundaries.md》。


目标工程为 `<local-path>`，本轮源码已经安装至该工程。数据从用户选择的 Persistent 或精简目录读取，不打包游戏素材。使用方式见上一级 UmaDesktopLegacy-User-Guide.md。

## 构建

Unity Editor **2022.3.62f3 / Windows x64 / Mono**，需要有效许可证。控制面板使用 Windows 自带 .NET Framework 的 csc.exe 编译，不需要 Visual Studio、NuGet 或 Web Build Support。运行版不需要 Unity。

```powershell
& .\Apply-Prototype.ps1 -ProjectPath '<local-path>
& .\Build-Prototype.ps1 -UnityPath '<local-path> Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe'
```

构建前关闭同项目编辑器和 UmaDesktop。构建从 Version2 生成独立 DesktopPrototype 场景，保留加载器序列化引用并关闭查看器原交互，原 Version2 不保存改动。配置 D3D11 BitBlt、Windows x64 Mono，完成后恢复 PlayerSettings。Build-Prototype 同时编译 UmaDesktopController.exe，发布时两个 EXE 及运行目录必须一起保留。

安装脚本保留第二版的上游兼容修复：Config 的进程级配置覆盖、UmaAssetManager 的桌面瞬时 bundle 释放入口，以及 20 个重组后的新旧重复源码的条件编译排除等。原文件保存在 OriginalSourceBackup；这些不是删除用户工程资源。

## 实现入口

| 文件 | 用途 |
|---|---|
| DesktopPrototype.cs | 多角色生命周期、边缘出入、命令协议、相遇抽签及同步双人演出 |
| DesktopActor.cs | 单个迷你模型、动画预载、整套末尾裁去 1 秒、脚部步幅和镜头校准、每角色渲染 |
| DesktopRules.cs | Include／Exclude、同名规范化、相遇条件、FIFO 离场事务与补位等待 |
| DesktopSchedule.cs | 步态 3:1、服装类别 50/30/20、洗牌、专属截止时间 |
| MiniMotionCatalog.cs | master.mdb 服装、真实依赖检查、动作匹配及目录 |
| MiniClipPlayer.cs | 手动 PlayableGraph、0.10 秒混合、可选面部／耳朵轨道及克隆缓存 |
| WindowsOverlay.cs | 每角色 Win32 分层窗口、逐像素 alpha、可见区域点击、原生资源释放 |
| Assets/Resources/UmaDesktop/LayeredAlpha.shader | 透明安全的内部 FXAA、覆盖率保留及预乘 gamma 输出 |
| Controller/UmaDesktopController.cs | WinForms 控制面板、任务栏入口、托盘菜单、多选名单 |
| Assets/Editor/UmaDesktopBuild.cs | 独立场景及构建入口 |

相机分别使用 25～30 层隔离六个模型。隐藏 Unity 源窗口后，使用 URP SingleCameraRequest 显式渲染，各相机 MSAA RT 经透明安全处理后进入三个异步读回缓冲，传给 UpdateLayeredWindow。DIB 正高度配合 Unity 底向上的原始像素布局；背景 alpha 为零，没有颜色键。原生回调 WM_LBUTTONDOWN 请求下一次更新即刻播放专属。默认 Windows 层级不是 WorkerW。

控制程序与渲染程序通过 DesktopCommands 中的原子 JSON 文件通信。DesktopLogs/state.json 原子替换并共享删除读取，避免刷新面板时发生文件占用。面板隐藏／最小化时停止读取和刷新角色列表，只监测父进程退出。正式命令有 exit、pause、next、costume、special、filter；测试模式才接受 testNativeClick 和 testEncounter。测试命令直接进入本程序回调，不注入 OS 鼠标输入。

服装从 dress_data/text_data 以及 mini/body 的身体、贴图和依赖建立目录。个人衣服 use_race=1 为胜负服；通用 body_type 2、3、16 为制服／训练服，其余为其他。菱曙默认个人衣服 01，避免硬编码全角色 00。全部目录 full-costume-catalog.json 含 136 角色、4,080 组合；mini_catalog_audit.json 为第一版历史审计。

## 当前验证入口

```powershell
& .\Test-Logic.ps1
& .\Test-Fleet.ps1 -Seconds 100
& .\Test-Filters.ps1
& .\Test-Performance.ps1 -FrameRate 15 -RenderScale 0.75 -MaxActors 2 -Msaa 2 -OutputFile '<local-path>
```

Test-Fleet 使用运行时加速参数和八名代表角色，不写入正式配置。Test-Filters 临时修改测试设置，finally 恢复原文。Test-Performance 的命令行覆盖也不持久化。Measure-Desktop 可单独测量两个进程。Test-Prototype 和 Test-Costumes 是第二版单角色历史测试，不能作为第三版验收入口。Controller --selftest --root 指向包含 costume-catalog.json、单角色 token=88 的 state.json 测试夹具，覆盖菜单命令与独立名单状态，不代表物理鼠标测试。

最终六角色回归、名单／换装测试、逻辑抽样和抗锯齿像素检查均通过；记录见上一级 UmaDesktop验证记录/v3。系统窗口截图接口超时及坐标输入接口不可用，因此本轮没有真实指针点击和 OS 合成截图验收。原生回调测试与引擎 PNG 的范围不同，不能混称。

耳朵、头发、尾巴存在绑定骨骼及弹簧配置，但迷你完整物理解算未恢复。双人互动使用三套配对 A/B 身体演出并尝试可选面部轨道，没有原场景位置轨道、语音或对话文字。未承诺所有服装／面部绑定及长时间资源稳定性。

## 技术依据

- Microsoft 分层窗口与 alpha 命中测试 (historical reference)
- UpdateLayeredWindow (historical reference)
- DIB 的行方向 (historical reference) 与 Unity 像素行顺序 (historical reference)
- 显式 URP 渲染请求 (historical reference)
- URP 抗锯齿方式 (historical reference)
- GPU 内存计数器误差说明 (historical reference)


## 3.1 描边与蒙皮同步修复

OutlineWidth 的含义改为参考输出像素。DesktopActor 在每个显式相机请求前设置正交投影所需的全局参数，_GlobalCameraFov=0、_GlobalOutlineOffset=0，并按输出尺寸和 orthographicSize 换算 _GlobalOutlineWidth；限制材质附加宽度并禁用 BoldOutline。不能再在多相机渲染完成后统一覆盖宽度。

手动姿势采样后显式渲染需要同一姿势的蒙皮矩阵。SkinnedMeshRenderer 启用 updateWhenOffscreen 和 forceMatrixRecalculationPerRender，采用 Bone4，避免刚性眼嘴面片与蒙皮网格不同步。依据 Unity 2022.3 官方 API (historical reference)。

Test-Visual.ps1 启动 --desktop-visual-audit，对 1071/00、1058/40、1001/00、1028/0016_01_00 共 96 个姿势分别导出旧全局参数、修复后、零描边三种图像。不会修改正式设置，也不启动控制面板；不要将其误用为正常启动选项。对比 PNG 来自引擎，不是 OS 桌面截图。MiniClipPlayer.Seek 仅用于固定采样时刻。结果与限制详见上一级 UmaDesktopRendering-Fixes.md。

## 3.2 桌面暂停

新增 DesktopEnvironment.cs：寻找 Explorer 的桌面图标宿主，按顶层窗口 Z 顺序和可见矩形计算剩余桌面面积。WindowsOverlay 只创建 WS_CHILD 窗口，在得到第一张透明画面之前保持隐藏，异步回读也不能重新显示已暂停的角色。Explorer 桌面宿主不可用时隐藏并等待，重新找到宿主后重建子窗口。AlwaysOnTop 及置顶快捷键停用。

DesktopPrototype 分开记录手动暂停与桌面遮挡暂停，暂停时冻结模拟时钟和淡入淡出，跳过角色姿态与渲染，主循环降到 2 FPS。state.json 新增 desktopPaused、manualPaused、desktopAvailable、desktopHost，以及每个角色的 desktopAttached / desktopParent / windowVisible，便于独立核验。

Test-Logic.ps1 增加 3,012 项矩形覆盖检查，其中 3,000 组与逐像素独立算法比较。Tests/verify_visibility.py 是 Python 3 标准库开发测试：只操作它自己创建的遮挡夹具，覆盖全屏、最大化、两个拼接窗口、透明窗口、全屏下启动及 CPU 对比，完成后关闭测试实例。运行前先退出 UmaDesktop；测试过程中会短暂出现遮挡测试窗口。Test-Fleet.ps1 额外要求所有角色始终属于桌面宿主。

```powershell
python .\Tests\verify_visibility.py --build-directory '<local-path> --evidence-directory '.\visibility-test-results'
```

## 3.3 随机速度、延后演出与避让

DesktopSpacing.cs 提供速度采样、间隔与扫掠移动限制。DesktopActor 为每次出场分别采样步行/跑步倍率，换装时由调度器保留；位移速度与播放倍率一起调整。加载时对已有步态、个人动作及配套片段采样、烘焙网格包围范围，再加入余量。单人和双人范围独立，按正交相机投影中心定位，不把透明画布的中心偏移重复计入宽度。

DesktopSchedule.Tick 增加 allowSpecial，空间不够时保留原步态与 pending 状态。角色额外移动一小段后重试。自动动作播放时为其保留区间；其他 Active 角色在跨入前转向，Entering / Exiting 角色限制移动距离并同步限制步态播放，以防滑步。双人互动同样不能侵入自动演出范围。BeginSpecial 的 automatic 参数只标记自动触发，鼠标和面板请求直接调用默认的用户触发路径。

Test-Logic.ps1 增加 20,012 项速度、延后与扫掠区间断言。Tests/verify_spacing.py 验证原生点击回调例外、延后到空位触发、接近者转向、演出提前结束及换装保留速度。

开发回归可显式同时使用 --desktop-test-mode 和 --desktop-test-ignore-occlusion，让模拟计时不受当前桌面被应用遮住影响。窗口仍是桌面子窗口，不会显示在应用上方；state.json 的 testIgnoresOcclusion 标记该测试条件。普通启动不携带这两个参数，仍执行全屏自动暂停。Test-Fleet.ps1 使用此开发隔离条件，以便无人值守测试角色调度。

## 3.4 动作表情

MiniExpressionPolicy.cs 的 12 套参数与本机 master.mdb 的 mini_face_type_data 逐项核对。纯逻辑选择按动作状态分组，跑步使用 90:10 分支，支持相邻状态抽到同一表情。例外使用游戏角色编号：1018、1040、1105。

MiniExpressionController 在模型加载后缓存默认脸部参数；每次动作入口恢复基线并选择一次表情。随机表情在手动 PlayableGraph 更新后写入四个脸部渲染器的材质与 material-index property block，避免动画参数覆盖。默认模式移除随机覆盖、保留游戏原始脸部／耳部轨道。诊断读取有效 property block 的左右眉、左右眼与嘴五个编号，验证渲染参数和标签一致。专属动作的 _s / _loop / _e 内部切换不会重抽。

Test-Logic.ps1 增加 900,910 项表情断言；其中 300,000 次跑步抽样为 90.053% Kiri / 9.947% 稀有组。Test-Expressions.ps1 实际加载特别周、气槽、金镇之光、新宇宙、菱曙，验证 245 次动作状态切换、五次内部片段切换，并渲染 12 套候选表情及实际状态肖像。新宇宙、菱曙验收使用可用的个人服装 01。表情专项模式只在显式 --desktop-expression-audit 时启动，不影响普通运行。


## 3.9 自定义地面

DesktopGroundMath.cs 是 Unity、WinForms 编辑器和测试共享的纯几何：凸四边形校验、单应映射、边缘通道与平面内导航。Controller/UmaDesktopGroundEditor.cs 提供主屏幕半透明画布、直线／四点绘制、角点拖动和最多三块地面及入口出口编辑。DesktopActorGround.cs 使用四点映射定位脚点，用独立相机等比例缩放身体，并更新朝向和通道渐变；DesktopPrototypeGround.cs 处理配置应用、专属动作空间检查和桌面子窗口前后排序。配置保存在 DesktopSettings.Ground，原有尺寸和重启仍保留。

Test-Logic.ps1 新增 81,807 项地面断言。Tests/verify_ground.py 运行真实 Unity 程序，验证二维路径、三地面随机分配、配置验证与保存、透视渲染、桌面层、点击动作、离场和重启恢复。控制面板 --ground-selftest 校验编辑点击状态与自身画布渲染，未注入物理鼠标输入。


## 3.9.1 倾斜狭长地面的模型比例

移除将地面单应矩阵直接作用于三维网格的投影方法。保留模型原有相机，只围绕校准的鞋底锚点等比例缩放画面；地面映射仍控制位置与远近倍率。朝向从地面路径的屏幕切线推导，避免任意四边形的轴方向被当作相机方向。

Tests/verify_ground_proportions.py 使用保存的两块用户地面，在相同模型、相同动画时间与相同朝向下，对比矩形地面的参考轮廓；验证六个位置的宽高比、归一化 alpha 轮廓，以及 50%、100%、150% 大小滑块下的等比例缩放。所有测试在退出时按原字节恢复 DesktopSettings.json。


## 3.10 鼠标拖拽与跨地面

DesktopMouseInput 区分按下、移动与释放，6 像素阈值区分拖动与手抖，按下目标沿用不透明像素及桌面遮挡校验，跨出身体范围后仍捕获本次手势。WindowsOverlay 以线程安全队列传递事件，合并连续移动。DesktopPrototypeDrag 在主线程冻结动画、表情与动作计时，保持当前片段；DesktopGroundDrag 将脚点投影到最近的有效地面内部，支持默认底边、直线、多个凸四边形和重叠区域的稳定选择。拖动解开双人配对时保留抓取方片段，另一方恢复移动。暂停或取消会释放手势，单击改在鼠标释放时触发专属动作。

Test-Logic.ps1 增加 8,002 项拖拽几何断言，Test-NativeInput.ps1 扩展到 27 项原生输入检查。Tests/verify_drag.py 和 verify_drag_pair.py 在真实 Unity 程序中验证冻结与恢复、边界和跨地面行为及配对处理。


## 3.10.1 低级鼠标移动放行

DesktopMouseInput 对捕获中的 WM_MOUSEMOVE 调用并返回 CallNextHookEx，不再返回 1。WM_LBUTTONDOWN/UP 仍只在不透明模型命中的完整手势中拦截，避免激活桌面图标；移动坐标继续送到合并事件队列。NativeInputTests 校验小幅与大幅移动的回调放行。verify_drag.py 增加大距离位移、释放后的落点提交与不回弹检查。Tests/NativeDragProbe.cs 是可见测试窗口，使用生产钩子与覆盖窗口，只测量系统指针，不发送系统鼠标输入；本轮 Computer Use 截图失败，未完成该项系统拖动验收，记录独立列出。


## 3.12.0 实现入口

`DesktopOptions.cs`：实时渲染设置、语言/目录保存、迷你依赖清单。`DesktopAssetPlan.cs`：与控制面板共用的数据计划。

`Controller/UmaDesktopOptions.cs`：设置页、诊断导出、路径切换及提取工作流。`UmaDesktopData.cs`：路径限制、取消、逐文件哈希与删除保护。`UmaDesktopLanguage.cs`：中日英词条。

`Tests/verify_options.py`：真实 Unity 运行与目录切换恢复验证；控制面板 `--options-selftest`：在隔离工作目录验证界面、诊断及复制/校验/删除保护，仅删除自建测试文件，不删除真实游戏资源。


## 3.14.0 名称与启动图验证

控制面板 `--names-selftest --root <隔离测试目录>` 验证 136 个角色的名称覆盖、三语言搜索、界面刷新和服装不变。`Tests/verify_names.py` 验证真实多实例场景与窗口标题，结束后恢复用户设置。

构建输出 `splash-build-report.json` 包含原图 SHA-256、宽高与启动设置，可进一步读取成品 globalgamemanagers 的背景指针和纹理校验图片。原图拷贝不会修改；Unity 会应用原生对比度遮罩。


## 3.15.0 三维地面避让验证

`Test-Logic.ps1` 包含 `AvoidanceTests`，覆盖 15/30/60/120 FPS 的迎面、交叉、追赶、静止障碍、窄道、六向交叉、跨地面互不影响、互动伙伴豁免、重叠拖放恢复、临时转向不破坏出口。`Tests/verify_avoidance.py` 使用真实 Unity 模型，验证占地分离、绕行、鼠标按住、点击专属、实时开关与持久化、二维绕过和正常退场。测试指令仅在测试模式有效，不注入系统鼠标事件。

预测在水平占地坐标中进行（屏幕 Y 除以固定相机俯角正弦），不使用包含角色竖直身高的透明窗矩形。每个角色的占地半径随体型、滑块、地面透视及鞋部范围变化；绕行目标限制在鞋部侵蚀后的凸地面内。相向步行的既有双人互动先于避让抽签。出口目标和路线阶段不被临时绕行覆盖。
