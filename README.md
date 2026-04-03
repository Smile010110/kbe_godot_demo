# Godot Demo

版本：Godot Engine v4.4.1.stable.mono

引擎版本：[dev-2.6.x](https://github.com/KBEngineLab/KBEngine-Nex/tree/dev-2.6.x)

服务端：https://github.com/KBEngineLab/demo_kbengine_nex_assets


# 当前整理结果

项目现已按 `kbe_csharp_plugins/` 里的生成内容收敛。

- 当前生成 SDK 只包含：`Player`、`GameMgr`、`Server`、`Space`、`WebServer`
- 客户端主流程整理为：`autoload App -> Login UI -> Player -> World`
- 旧的 `Account/Avatar/Monster/NPC/Gate/Test` 客户端脚本已移除，因为它们和当前生成 SDK 不匹配
- 世界内保留的是 `Player` 表现层与基础移动/镜头能力

# 当前支持

- [x] 连接 KBEngine
- [x] 登录
- [x] 创建 Player 实体并进入世界
- [x] 本地玩家移动
- [x] 远端 Player 同步显示
- [ ] Account/Avatar 选角链路
- [ ] Monster/NPC/Gate 客户端实体
- [ ] 死亡/复活
- [ ] 攻击/攻击动画

# sdk 生成

start "" "%KBE_BIN_PATH%/kbcmd.exe" --clientsdk=csharp --outpath="%~dp0/kbe_csharp_plugins"

将最新生成结果覆盖到 `/kbe_csharp_plugins`


# 说明

`kbe_csharp_plugins` 是这份工程的事实来源。每次服务端实体定义变化后，都应该先重新生成 SDK，再按生成出来的 `*Base.cs` 和 `EntityDef.cs` 调整手写层。
