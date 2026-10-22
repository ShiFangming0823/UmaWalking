> Historical development note. Older defaults, limits, and procedures may be superseded. See the current root README and AGENTS.md. Local test attachments are intentionally omitted.

# UmaDesktop 3.6 · Alt+Tab 与 FancyZones

本次修复临时窗口管理界面导致模型主动隐藏的问题，不改变模型的桌面层级。

## 行为

- 从正常播放状态打开 Alt+Tab，或拖动窗口时显示 FancyZones 分区：保留模型窗口与动画，不因这些临时界面或它们产生的全屏预览立即隐藏。
- 从已被全屏应用遮挡的暂停状态打开上述界面：保留暂停，不强行让模型重新出现。
- 操作结束后，依据真正的应用窗口重算遮挡。桌面露出就恢复；新的全覆盖必须持续约 0.75 秒才暂停，减少切窗、吸附过程中的闪烁。
- 手动暂停独立保留。普通应用、PowerToys 设置窗口，以及分区排列完成后完全铺满桌面的应用，继续触发原有自动暂停。

“保持显示”指程序不主动调用隐藏窗口。模型仍位于桌面层，系统界面或应用的实际不透明区域可以自然遮住模型；不会把模型置顶到应用或 Alt+Tab 面板之上。

## 实现依据

本机已只读确认 FancyZones 的覆盖层类为 `FancyZones_ZonesOverlay`，属于 `PowerToys.FancyZones` 进程，与 PowerToys 源代码 (historical attachment) 一致。只排除该专用覆盖层，不排除整个 PowerToys。

Alt+Tab 结合 Windows 的 EVENT_SYSTEM_SWITCHSTART / SWITCHEND 通知 (historical attachment)、Explorer 的切换界面窗口类和切换界面标识判断。通用 Shell XAML 窗口不会仅因类名相同就全部排除。事件监听在退出时释放；未收到结束事件时有按键／窗口状态恢复路径。

临时界面出现时保留上一次稳定的自动暂停状态，并清除尚未确认的全覆盖计时。正常全屏自动暂停仍有效。

## 验证范围

- 新增 28 项窗口分类与状态转换断言；桌面覆盖套件共 3,040 项通过，其他动作、筛选、速度及表情逻辑套件通过。
- 20 项原生覆盖层兼容检查：FancyZones、Windows 10 与 Windows 11 同类切换界面；窗口保持可见、渲染帧和时间继续增长；临时全屏预览不触发暂停；退出后真实全屏仍暂停；原先已暂停时不误恢复；模型保持非置顶桌面子窗口。
- 17 项真实原生窗口覆盖回归，包含全屏、平铺、透明覆盖、手动暂停和六角色自动休眠。
- 17 项原生鼠标命中检查通过，未改变已有点击触发规则。

覆盖层兼容测试在隔离测试目录创建本程序自己的原生窗口，使用模拟进程名称和窗口类；不替换、控制或修改已安装的 Explorer 与 PowerToys。不注入系统快捷键。因此这些测试验证应用的检测与暂停行为，实际 Alt+Tab 与 FancyZones 的桌面观感仍需实际操作核对。

无新增运行依赖。便携包双击“Launch-UmaWalking.cmd”。开发复测入口为补丁中的 `Test-TransientVisibility.ps1`、`Tests/verify_visibility.py` 和 `Test-NativeInput.ps1`。详细记录在 UmaDesktop验证记录/v9。
