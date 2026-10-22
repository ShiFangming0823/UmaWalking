> Historical development note. Older defaults, limits, and procedures may be superseded. See the current root README and AGENTS.md. Local test attachments are intentionally omitted.

# UmaDesktop 3.7 动作表情分配

| 动作 | 抽取规则 |
|---|---|
| 步行 | Warai 组 80%，组内 A～E 等概率（各 16%）；Sleep 组 20%，组内 A/B 等概率（各 10%） |
| 专属动作 | WaraiA～WaraiE 各 1/5 |
| 跑步 | KiriA/B/C 各 30%；GuruguruA、FightA 各 5% |

新宇宙、金镇之光、气槽做专属动作时保持游戏原生表情。步行／跑步照常抽取。双人互动按专属动作规则处理。SleepA/B 作为表情加入步行池，身体仍使用步行动作。

每次进入新动作抽取一次，允许重复。一次专属动作的开始、循环、结束片段不重抽；自动动作因拥挤延后时仍保留步态表情。鼠标点击专属动作会立即切换到专属动作的表情规则。

采用与 36 套表情图鉴相同的左右眉、左右眼、嘴编号。随机表情写入渲染器实际使用的属性覆盖，默认模式清除这些覆盖，恢复原生脸部轨道。耳朵动作与物理逻辑沿用原型已有行为。

3.7 概率检查：300,000 次步行采样中 Warai 组为 79.921%，Sleep 组为 20.079%；各组成员等概率及 80% 分界检查通过。表情套件共 900,921 项断言通过。

此前 3.4 的验证包括 900,910 项逻辑断言、五个实际模型的 245 次动作状态切换和五次专属片段切换。300,000 次跑步采样得到 90.053% Kiri、9.947% 稀有组；12 套表情均已用 Unity 实际渲染，输出互不相同。三个例外角色的默认脸也已单独渲染检查。验收服装为角色可用的个人服装，新宇宙、菱曙为 01，其余为 00。完整资料在 UmaDesktop验证记录/v7。

实现参考 Unity 官方 Renderer.GetPropertyBlock (historical attachment) 与 MaterialPropertyBlock.HasVector (historical attachment)。
