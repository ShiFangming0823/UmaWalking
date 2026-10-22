> Historical development note. Older defaults, limits, and procedures may be superseded. See the current root README and AGENTS.md. Local test attachments are intentionally omitted.

# UmaDesktop 3.1 描边与面片同步修复

已更新原项目、构建和便携包。目标是修复用户截图中的厚重棕色轮廓以及大幅动作时眼睛／嘴巴等面片与模型错位。

## 原因与修复

1. **描边参数不适合正交相机。** 旧版 _GlobalCameraFov 固定为 30。在实际游戏 MiniCharaBody DX11 顶点程序中，描边扩张含有 `(0.5 × _GlobalCameraFov + 0.75)` 因子，因此原本应很细的轮廓被放大。同时 _GlobalOutlineOffset 会单独修改描边深度；旧值会把背面向相机拉近，可能覆盖模型前方。修复取消该深度偏移，按每个相机的 orthographicSize 与输出窗口尺寸，将 OutlineWidth=0.45 标定为约 0.45 输出像素的参考宽度，并关闭附加 BoldOutline。材质／顶点数据及 MSAA 会影响实际覆盖。
2. **手动采样与蒙皮缓存不同步。** 显式渲染时，刚性眼嘴面片已处在新姿势，蒙皮头部／头发／身体可能仍使用缓存矩阵。已启用 forceMatrixRecalculationPerRender、updateWhenOffscreen 和四骨骼权重；固定姿势对比中，缺失的眼睛、异常嘴部横条及部件不同步随之消失。使用方法参照 Unity 2022.3 官方文档 (historical attachment)。

修复位于 DesktopActor.cs；不修改游戏资源本身，也不通过缩小动作幅度掩盖问题。MSAA／FXAA 保留。

## 验证

代表组合：目白阿尔丹 1071/00、名将怒涛 1058/40、特别周 1001/00、菱曙 1028/0016_01_00。每组覆盖步行、两段跑步、可用专属动作，取三个时间点及正面／65°侧面，共 96 个姿势。每个姿势渲染旧全局参数、修复后、无描边参考三张，合计 288 张。

对当前蒙皮同步后的相同姿势比较，旧参数在无描边轮廓外的像素总数为 948,644，新参数为 66,985，减少 **92.94%**。以 alpha>8/255 定义边缘，最大外扩距离从约 9.85 像素降为 2 像素（含抗锯齿覆盖）；该指标是轮廓外扩覆盖，不等于所有位置线宽都正好减少相同比例。独立检查了修复前缓存未刷新的画面，确认眼嘴与蒙皮的差异。

最终六角色回归约 101.7 秒：最多六只、9 次加载、3 次完整离场、81 次专属提前截断、3 次双人互动，FIFO、补位间隔、同名唯一及原生持续输出均通过。没有记录到图形异常。

本轮图像由引擎直接导出；没有将其冒充系统桌面截图。仍未逐一验收全部角色／服装组合，也不承诺消除游戏模型原本存在的所有衣物穿插。当前确认修复的是可复现的描边异常和刚性／蒙皮姿势同步问题。

渲染程序集 SHA-256：`F5517D60923A729B548E530141F78248B1B057B40C78987900762A8F2D26E7B1`。

## 文件与复现

- UmaDesktop描边与面片修复对比.png：旧设置及缓存问题复现／修复后对照。
- UmaDesktop验证记录/v4/96姿势检查总览.jpg：全部 96 个最终姿势。
- final-poses/report.json、visual-metrics.json：每个姿势的动作、采样时间及比较指标。
- before-skin-refresh：未即时刷新蒙皮时的诊断画面。
- fleet-report.json、performance-six-render-fix.json：回归与资源采样。
- Test-Visual.ps1：重新生成 96 个固定姿势检查。

OutlineWidth 仍可在 DesktopSettings.json 调整；设为 0 可关闭几何轮廓。当前 0.45 已是很细的参考宽度，贴图中画出的线条不会被该参数移除。
