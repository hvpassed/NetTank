# NetTank Arena：Unity + Mirror 多人坦克同步 Demo

本项目目标是一个 2-4 人多人坦克对战 Demo，用来展示 Mirror 联机、服务端权威、状态同步、RPC 事件广播、死亡复活和排行榜。

## 1. 项目搭建步骤

1. 使用 Unity 6000.5 或 Unity 2022 LTS 打开工程。
2. 手动安装 Mirror。任选一种方式即可：
   - Unity Asset Store 导入 Mirror。
   - 使用 OpenUPM 安装 `com.mirrornetworking.mirror`。
   - 使用 Mirror 官方推荐的安装方式导入最新稳定版。
3. 确认 Project 窗口中能看到 Mirror 相关目录，并且代码里的 `using Mirror;` 不再报错。

4. 打开菜单 `Tools/NetTank Arena/Build Demo Scene`。
5. 工具会自动生成：
   - `Assets/Scenes/MainScene.unity`
   - `Assets/Prefabs/PlayerTank.prefab`
   - `Assets/Prefabs/Bullet.prefab`
   - `Assets/Prefabs/Explosion.prefab`
   - `Assets/Prefabs/MuzzleFlash.prefab`
   - 基础地图、SpawnPoints、NetworkManager、连接 UI、HUD、排行榜
6. 打开 `Assets/Scenes/MainScene.unity` 并运行。

## 2. Unity 场景配置

自动生成的 `MainScene` 包含：

- `NetworkManager`：挂载 `NetTankNetworkManager`、Mirror Transport、`ConnectUI`
- `Arena`：地面、四周墙体、中心障碍物
- `SpawnPoints`：四个出生点，挂载 `SpawnPointGroup`
- `Canvas`：连接面板、血量 HUD、分数排行榜、网络状态文本
- `Main Camera`：俯视角观察整张地图

手动搭建时保持同样结构即可。`SpawnPointGroup` 会自动收集子物体作为出生点，超过出生点数量的玩家会循环使用。

## 3. Mirror 和 NetworkManager 配置

`NetTankNetworkManager` 继承 Mirror `NetworkManager`，核心职责：

- 重写 `OnServerAddPlayer`
- 按连接顺序分配出生点
- 生成 `PlayerTank` 并调用 `NetworkServer.AddPlayerForConnection`
- 初始化玩家编号、连接 ID、血量和同步位置

NetworkManager Inspector 建议配置：

- Player Prefab：`PlayerTank.prefab`
- Auto Create Player：开启
- Max Connections：4
- Registered Spawnable Prefabs：加入 `Bullet.prefab`
- Transport：Telepathy Transport 或 KCP Transport

本项目移动同步使用自定义服务端权威方案，不依赖 `NetworkTransform`：客户端只发输入，服务端移动坦克，并通过 SyncVar 同步位置和旋转。

## 4. Prefab 配置说明

### PlayerTank.prefab

必须包含：

- `NetworkIdentity`
- `Rigidbody`
- `BoxCollider`
- `TankIdentity`
- `TankController`
- `TankShooting`
- `TankHealth`
- 子物体 `FirePoint`

关键引用：

- `TankShooting.bulletPrefab` 指向 `Bullet.prefab`
- `TankShooting.firePoint` 指向子物体 `FirePoint`
- `TankHealth.explosionPrefab` 指向 `Explosion.prefab`

### Bullet.prefab

必须包含：

- `NetworkIdentity`
- `SphereCollider`，勾选 `Is Trigger`
- `Rigidbody`，关闭重力并设为 Kinematic
- `Bullet`

子弹由服务端 `Instantiate` 并调用 `NetworkServer.Spawn`，客户端不能直接生成。

### Explosion.prefab

普通本地特效 Prefab，不需要 `NetworkIdentity`。死亡时由 `TankHealth.RpcPlayExplosion` 在所有客户端播放。

## 5. 核心代码位置

- `Assets/Scripts/Network/NetTankNetworkManager.cs`
- `Assets/Scripts/Network/ConnectUI.cs`
- `Assets/Scripts/Player/TankIdentity.cs`
- `Assets/Scripts/Player/TankController.cs`
- `Assets/Scripts/Player/TankShooting.cs`
- `Assets/Scripts/Player/TankHealth.cs`
- `Assets/Scripts/Combat/Bullet.cs`
- `Assets/Scripts/UI/PlayerHUD.cs`
- `Assets/Scripts/UI/ScoreboardUI.cs`
- `Assets/Scripts/Utils/SpawnPointGroup.cs`
- `Assets/Scripts/Editor/NetTankProjectBuilder.cs`

网络权限设计：

- 输入：客户端读取本地键盘，使用 `[Command]` 发给服务端。
- 移动：服务端计算位置和旋转，SyncVar 同步到所有客户端。
- 开火：客户端请求，服务端检查冷却并生成子弹。
- 命中：只在服务端执行碰撞判定和 `TakeDamage`。
- 血量、死亡、分数：使用 `[SyncVar]`。
- 爆炸和枪口火光：使用 `[ClientRpc]` 广播瞬时事件。

## 6. 测试方法

1. 在 Unity Editor 打开 `MainScene`，点击 Play。
2. 点击 `Host`，启动本机 Host。
3. 打包一个 Windows Client，或使用 ParrelSync 克隆工程再开一个 Editor。
4. 第二个客户端输入 Host IP：
   - 同机测试用 `localhost` 或 `127.0.0.1`
   - 局域网测试用 Host 机器的局域网 IP
5. 点击 `Client` 加入。
6. 操作：
   - W/S：前进/后退
   - A/D：左转/右转
   - Shift：加速
   - Space：开火
7. 验收：
   - 双方能看到彼此坦克
   - 只有本地玩家能控制自己的坦克
   - 子弹、扣血、死亡、爆炸、复活和分数能同步

## 7. 常见问题排查

### 找不到 Mirror 命名空间

Mirror 还没有安装成功。先用 Asset Store、OpenUPM 或 Mirror 官方推荐方式导入 Mirror，再等待 Unity 重新编译。

### Client 点连接没有反应

确认 Host 已经启动，IP 填写正确。局域网连接时确认防火墙没有拦截 Unity/打包程序端口。

### 子弹生成但客户端看不到

确认 `NetworkManager` 的 Registered Spawnable Prefabs 里加入了 `Bullet.prefab`。

### 坦克死亡后没有复活

确认场景里存在 `SpawnPoints`，并且其子物体至少有一个出生点。也可以检查 `TankHealth.respawnDelay` 是否被设得过长。

### 输入无效

项目同时兼容旧 Input Manager 和新 Input System。若键盘输入无效，检查 Player Settings 的 Active Input Handling，建议设为 Both。

## 8. 简历项目描述

NetTank Arena 是一个基于 Unity 与 Mirror 的多人坦克对战同步 Demo，支持 2-4 名玩家在局域网内 Host/Client 联机。项目采用服务端权威架构：客户端只上传移动与开火输入，服务端负责玩家生成、移动结算、子弹生成、命中判定、扣血、死亡复活和计分；血量、死亡状态、分数和位姿通过 SyncVar 同步，爆炸与枪口火光通过 ClientRpc 广播。项目包含自动场景/Prefab 构建工具、连接 UI、HUD、排行榜和基础地图，重点展示 Unity 多人网络同步、状态复制、RPC 事件和可演示的工程化组织能力。
