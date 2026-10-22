> Historical development note. Older defaults, limits, and procedures may be superseded. See the current root README and AGENTS.md. Local test attachments are intentionally omitted.

# UmaDesktop 3.14.0：启动背景与角色名称

## 启动背景

Made with Unity 的启动背景改为你提供的 `QQ20261020-232901.jpg`，原图为 1217 × 995。保留 Unity 标识，关闭背景模糊与缩放动画，沿用 Unity 原生的对比度遮罩。因此启动画面的图片会比原 JPG 略暗。图片已嵌入应用，运行时不依赖原 JPG 的位置。

构建后直接读取成品的 PlayerSettings 与启动背景纹理，确认图片内容与原图一致（仅经过 Unity 原生的统一对比度处理），宽高与纵横比正确。这不是模拟截图；核对的是应用实际打包使用的纹理与启动设置。

## 角色名称

当前数据中的 136 个角色均有日文、中文和英文名称，离线可用。

- 切换应用语言后，角色筛选列表、在场模型选择框、指定上场列表、上场队列、操作消息中的角色名和模型窗口标题随之切换。
- 可以使用中文、日文、英文或编号搜索，不需要先切换到对应语言。
- 同名模型仍以实例编号和服装区分；切换语言不重新加载模型、不更换衣服、不改动勾选名单。
- **皮肤/服装名称保留游戏数据中的日语原文**，包括服装名中出现的角色日文名。
- 日文资源身份与角色编号保持稳定，用于去重、筛选、素材读取和日志排查；诊断快照额外显示当前语言的 displayName。

中文名称沿用原项目已有的常用简体译名，英文沿用原项目已有的英文表。补齐了最新的蝴蝶兰 / Phalaenopsis，并将骏川手纲恢复为正式名称，去掉原表中的昵称。新版本游戏如果增加尚未收录的角色，会先显示数据原名；更新名称表后即可补齐。

## 验证

已检查全部 136 个角色的三语言覆盖与搜索，并实测六只同角色不同服装同时存在时切换三种语言：窗口和动作保持连续，服装及资源身份保持一致。另回归了 Include 多服装、换装、上场队列、筛选切换和原生鼠标路由。

名称表来源：项目 `Assets/Scripts/localfy/Localize.cs`、`LocalizeEn.cs`。缺项参考 蝴蝶兰中文资料 (historical attachment)、Phalaenopsis (historical attachment)、Tazuna Hayakawa (historical attachment)。
