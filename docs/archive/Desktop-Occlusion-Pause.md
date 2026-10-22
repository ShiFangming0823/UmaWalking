> Historical development note. Older defaults, limits, and procedures may be superseded. See the current root README and AGENTS.md. Local test attachments are intentionally omitted.

# UmaDesktop 3.2 桌面层与自动暂停

修复前，角色使用普通顶层窗口；新窗口即使没有 TOPMOST 标志，也可能排到其他应用前。修复后，每只角色都直接创建为 Explorer 桌面图标宿主的子窗口（WS_CHILD），没有顶层窗口或置顶标志，因此新角色也由应用窗口自然遮挡。未修改壁纸或重启 Explorer。

桌面工作区完全被不透明窗口盖住后，隐藏所有角色，停止动作、移动、出场调度、淡入淡出与模型渲染。暂停时降低主循环频率到 2 FPS，保留资源以便快速继续。露出桌面后自动继续；手动暂停不会被自动恢复清除。

## 本机验证

- 全屏下启动：第一只角色的加载与出场等待桌面露出。
- 原生 Win32 检查：角色有 WS_CHILD、无 WS_POPUP / WS_EX_TOPMOST，实际父窗口等于桌面宿主，坐标仍使用物理像素。
- 全屏、最大化、两个拼接窗口完全覆盖工作区：自动暂停。
- 部分覆盖、遮挡窗口最小化、透明全屏叠加：继续或恢复运行。
- 完全遮挡期间：模拟时钟、已加载角色数量和呈现帧计数保持不变，所有角色窗口隐藏；挂起的异步回读未把窗口重新显示。
- 手动暂停与自动暂停组合：桌面露出后仍保持手动暂停。
- 窗口覆盖算法通过 3,012 项断言，其中 3,000 组随机矩形与独立逐像素算法比较。

六角色运行与完全遮挡后各采样 8 秒：CPU 总占用约 **3.85% → 0.18%**（16 个逻辑处理器归一化）。机器为 i9-11980HK / RTX 3080 Laptop；这个短样本用于验证待机降载，不能代表所有电脑。暂停并不释放常驻模型内存。模型呈现帧计数停止是渲染暂停的直接验证，没有宣称整台显卡零负载。

结构化测试：`UmaDesktop验证记录/v5/desktop-visibility-report.json`。运行程序集 SHA-256：`DAD177AE296F636B7F62EB1B4B71461EAC311F4D84F1D3184B265AA0FE845ECC`。

本轮使用自动化测试夹具与 Win32 属性核验，没有做实体桌面截图验收。Explorer 被外部重启后的恢复逻辑已实现，本轮未主动重启用户 Explorer 做验收。当前只针对主屏幕；透明窗口的复杂逐像素形状保守视为不能确认完全遮挡。

## 实现依据

Windows 对子窗口层级与透明合成的行为参见 Microsoft 的 Window Features (historical attachment) 和 UpdateLayeredWindow (historical attachment)。可见窗口边界与系统隐藏状态使用 DWM window attributes (historical attachment)。
