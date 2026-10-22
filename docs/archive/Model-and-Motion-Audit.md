> Historical development note. Older defaults, limits, and procedures may be superseded. See the current root README and AGENTS.md. Local test attachments are intentionally omitted.

# 迷你模型骨骼与双人动作检查

本次检查直接读取用户 Persistent 数据中的 AssetBundle，并对照 UmaViewer 的迷你角色组装代码。

## 耳朵、头发、尾巴

三类都有实际绑定的骨骼，并非只有静态网格。以特别周为代表：

| 部位 | 已确认的网格骨骼 | 已确认的物理配置 |
|---|---|---|
| 耳朵 | Sp_He_Ear0_L/R、Ear_01/02/03_L/R 等 | 头发 cloth00 配置包含左右耳弹簧根、刚度、阻尼、重力及角度约束 |
| 头发 | Sp_He_Hair0_C、Hair2_L/R、Hair3_L/R、Hair4_C/L/R 等多节骨骼 | 头部 cloth00 含 14 个弹簧根（包括耳朵及附属项）、13 个碰撞配置 |
| 尾巴 | Sp_Hi_Tail0_B_00/01/02，与 Hip 绑定 | mtail0001_00 的 cloth00 含尾巴弹簧链 |

资源示例为 `3d/chara/mini/head/mchr1001_00/clothes/pfb_mchr1001_00_cloth00` 与 `3d/chara/mini/tail/mtail0001_00/clothes/pfb_mtail0001_00_cloth00`，已实际解读 springParam、collisionParam 和 SkinnedMeshRenderer 的 bones。

**存在物理骨骼和参数，不代表当前已经在进行物理求解。** 原工程 LoadMiniUma 会实例化尾巴 cloth 资源，但没有接入标准角色完整的 LoadPhysics() 初始化流程，也未载入头部 spring 配置；UmaContainerCharacter 的 SetDynamicBoneEnable、ResetDynamicBone、LateUpdate 对 IsMini 直接返回。因此本轮保留动画驱动及耳朵配套轨道，没有宣称已恢复游戏原版迷你 CySpring 物理。后续若要启用，需要专门接入迷你配置、碰撞体和动画／物理更新顺序，再评估六角色的开销。

## 双人动作

找到三组可配对的真实迷你动作：

- `mini/set/body/type01/anm_min_set_type01_004_01_a` 与 `_b`
- `mini/set/body/type02/anm_min_set_type02_004_01_a` 与 `_b`
- `mini/set/body/type03/anm_min_set_type03_004_01_a` 与 `_b`

配套 A/B 轨道具有匹配的时长、面部／耳朵及位置资源。已解析 type01 的双方主动作，均约 9.367 秒。实际播放中可以看到一方做手势、另一方回应的配套演出，适合用作桌面上的聊天／交流互动；这个用途判断来自轨道结构和实际姿势，不是把文件名当作官方的“聊天”标签。

程序只在双方均为步行、方向相向且首次跨过相遇距离时，以 50% 概率启动配套 A/B 动作；运行中、正在入场／离场或已经互动的角色不会参与。每次相遇只抽一次，冷却期间不重复抽取。双人演出同步开始、一起结束，再恢复独立步行／跑步。点击其中一方或请求其离场时，会解除双方的互动状态，避免另一方永久等待。

桌面里复用了双方身体轨道，并尝试叠加配套表情和耳朵轨道（全部哈希绑定尚未逐一验收），站位由桌面移动系统管理，没有照搬原游戏场景相机和全局位置轨道。该功能没有对话文字或语音。

原始骨骼和配置数据见 `UmaDesktop验证记录/v3/mini-physics-objects.json` 与 `physics-and-pair-detail.json`。完整角色池的所有骨骼组合尚未逐一完成物理模拟验收。
