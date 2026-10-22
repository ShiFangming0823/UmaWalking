> Historical development note. Older defaults, limits, and procedures may be superseded. See the current root README and AGENTS.md. Local test attachments are intentionally omitted.

# 地面模型比例修复 · 3.9.1

修复狭长、斜向四边形导致模型横向拉伸、压扁和斜切的问题。此前地面的单应变换直接用于整个三维网格，把地面形状的畸变带到了身体上。

现在四点映射决定落脚位置和远近倍率，模型使用原有独立相机等比例缩放；行进朝向由路径在屏幕上的方向确定。地面角点、入口、出口和原有大小设置不需要重新配置。

实际渲染验证使用用户保存的两块地面，在每块三个位置固定相同角色、相同动画时间与相同朝向，对比矩形地面的参考轮廓，并测试 50%、100%、150% 模型大小。检查宽高比例、归一化透明轮廓、远近缩放及桌面子窗口挂载。另行回归二维直线、最多三地面、点击动作、入口出口、保存重启及原生透明点击。

具体结果见验证记录中的 ground-proportions-report.json、ground-report.json。测试恢复原配置后，正常启动修复版。
