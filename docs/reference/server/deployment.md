# 版本目录与实例退役

客户端按游戏、模组版本精确匹配实例，TCP 握手仍校验全部版本。协议号保持 0，不参与目录选择。目录服务不转发游戏消息，目录重启不影响已建立的游戏连接。

## 客户端

配置 `Multiplayer.ServerDirectoryUrl` 为 `https://game-version.metamystia.net/v1/servers`，`/mp play` 连接前查询一次。测试允许本机 HTTP 地址。留空沿用现有官方地址，便于域名和证书配置完成前过渡；配置了目录后，查询失败不会回退到其他服务器。手动 `/mp connect` 与局域网入口不变。

只有 `ready` 条目提供连接地址。`retiring`、`retired` 提示更新，`unavailable` 提示当前版本不可用。没有匹配版本时不选择最新版代替。

## HTTP 接口

| 服务 | 接口 | 认证与用途 |
|---|---|---|
| 目录 | `GET /v1/servers` | 公开，只含玩家需要的信息 |
| 目录 | `GET /admin/instances` | 部署令牌，查询完整登记 |
| 目录 | `PUT /admin/instances/{id}` | 部署令牌，验证实际身份后登记 |
| 目录 | `POST /admin/instances/{id}/activate` | 部署令牌，发布并记录退役任务 |
| 目录 | `GET /internal/instances/{id}` | 实例令牌，启动前核对生命周期 |
| 目录 | `POST /internal/instances/{id}/stopped` | 实例令牌，确认退役退出 |
| 实例 | `GET /admin/status` | 实例令牌，查询版本、端口和人数 |
| 实例 | `POST /admin/drain` | 实例令牌，停止接纳新连接并等待无人 |

认证使用 `Authorization: Bearer <token>`。部署令牌与实例令牌分别配置，不发给游戏客户端。实例管理端口默认只绑定本机。通过反向代理公开目录时，只开放 `/v1/servers`；远程管理需要 HTTPS。

登记正文为 `gameVersion`、`modVersion`、`host`、`port`、`adminUrl`。实例 ID 和地址不可覆盖。激活正文为 `{"replaces":["旧实例ID"]}`，空数组不退役其他实例。部署脚本自动选出当前 `active` 实例并传入 ID；手动部署也可指定列表，不根据版本字符串排序推测。同一游戏和模组版本只能有一个活跃实例，不支持覆盖正在使用的同版本服务。

退役正文为 `{"noticeId":"发布ID","targetModVersion":"目标模组版本"}`。重复请求不重复广播。通知是独立系统事件，由客户端按自己的语言显示；普通玩家不能发送。已连接的大厅玩家也计入在线人数，心跳超时才清理异常断线。没有强制下线时限，持续在线者会延长旧实例运行时间。

## 运行配置

| 进程 | 环境变量 | 含义 |
|---|---|---|
| 目录 | `MM_DIRECTORY_LISTEN` | 默认 `http://127.0.0.1:40900` |
| 目录 | `MM_DIRECTORY_DATA` | 登记数据文件，默认程序目录 `data/instances.json` |
| 目录 | `MM_DIRECTORY_TOKEN` | 部署接口令牌，必填 |
| 目录、实例 | `MM_INSTANCE_TOKEN` | 实例管理及内部登记接口令牌，必填 |
| 实例 | `MM_ADMIN_URL` | 管理 HTTP 监听地址；不设置则不启用管理接口 |
| 实例 | `MM_INSTANCE_ID` | 不复用的实例 ID |
| 实例 | `MM_DIRECTORY_URL` | 目录服务内部根地址 |

目录原子保存登记和退役任务，周期查询实例并重试通知。失联实例不会作为可用地址返回，也不会仅凭失联判定已正常退出。退出回报失败时保留退役登记，不重新开放连接。

托管实例启动前查询目录；已退役实例直接正常退出，目录不可达时启动失败并由 systemd 重试。新实例尚未登记时可启动，验证后再登记。配置 `Restart=on-failure`，正常退役不重启。

## 部署

`tools/deploy/` 提供独立的目录服务单元、实例服务模板和部署脚本。目录根为 `/opt/metamystia-hosting`，不会修改旧 `metamystia.service` 或 `/opt/metamystia/releases/current`。

| 用途 | 端口 | 对外开放 |
|---|---|---|
| 原有游戏服务 | `40815/tcp` | 保留现有规则 |
| 新游戏实例 | `41000–41099/tcp` | UFW 开放此范围 |
| 实例管理 HTTP | `42000–42099/tcp` | 只绑定 `127.0.0.1`，不开放 UFW |
| 目录服务内部 HTTP | `40900/tcp` | 只绑定 `127.0.0.1`，不开放 UFW |
| Nginx 公开目录 | `443/tcp` | 使用现有 HTTPS 入口 |

CD 在正式发布 Release 时自动触发，也接受将正式版设为最新版本的编辑事件。草稿和预发布不部署。执行时核对最新正式 Release，避免排队的旧发布替换新版本。手动入口仍可使用；`production` 环境原有审批规则继续生效。

端口默认 `auto`，在部署锁内从上述范围选取空闲端口，同时跳过未退役登记占用的端口。手动指定端口遇到占用则失败，不停止占用端口的服务。每个实例使用独立目录、日志及 systemd 单元；两个实例不能共用一个监听地址和端口。

退役列表默认 `auto`，只选取当前 `active` 实例，不改变已在退役或尚未激活的实例。首次部署可以没有旧实例。手动填写 ID 列表可限定退役对象，`-` 表示不退役。新实例握手、登记和激活成功后才通知旧实例。

UFW 增量规则为 `sudo ufw allow 41000:41099/tcp comment 'MetaMystia instances'`，不重置现有规则。若云平台另有安全组，也需允许同一 TCP 范围。

首次安装由管理员完成，不能直接用旧的停服部署脚本替代：

1. 发布目录服务到独立目录，安装 `metamystia-directory.service` 与 `metamystia-instance@.service`；仅启动目录服务。
2. 配置 `/etc/metamystia-hosting/directory.env`，指定两个令牌及可写的 `MM_DIRECTORY_DATA`。数据目录归 `metamystia` 用户所有。
3. 安装部署脚本到 `/opt/metamystia-hosting/tools/`，目录和脚本归 root 管理。部署配置 `/etc/metamystia-hosting/deploy.json` 仅 root 可读，含 `directoryUrl`、`directoryToken`、`instanceToken`、`publicHost`。
4. 将 `ssh-command.sh` 安装到 `/usr/local/bin/metamystia-hosting-ssh`，作为部署 SSH 公钥的 forced command；只授予执行两个部署 Python 脚本所需的 sudo 权限。旧 `deploy` 命令不会被新流程调用。
5. 现有配置可放在新根目录的 `server.json`、`chat-filter.json`，实例链接到这两份配置；每个实例独立记录日志。

CD 依次构建、测试、发布独立实例、完成实际 TCP 握手、HTTP 登记和激活。目录接管退役通知，CD 不等待玩家下线。目录服务产物单独保存，首次安装和目录升级单独安排。

目录尚未安装或新的 SSH 入口尚未配置时，新 CD 会失败，不会回退到重启旧服务。目标实例目录已存在时不覆盖文件，可用 `activate-instance <id> <旧id列表或->` 重试登记和激活。部署失败留下的新实例需单独检查，不停止任何旧实例。

新实例命令格式：`deploy-instance <id> <TCP端口或auto> <管理端口或auto> <逗号分隔旧id、auto或->`，部署包从标准输入读取。服务端的 `--probe <IP> <端口>` 使用本构建版本完成一次真实握手并退出，只用于新实例验证。

旧版没有管理 HTTP 和系统通知能力，首次迁移需保留其原服务，等待玩家自然离线后再另行退役；不得把新通知发给旧客户端。

## 验证

运行 `MetaMystia.Network.Tests`、`MetaMystia.Hosting.Tests`、`MetaMystia.Flow.Tests` 与 `tools/check.py`，并编译模组和服务端。Linux 上运行 `python3 -m unittest discover -s tools/deploy -p 'test_*.py'` 验证部署脚本。Hosting 测试使用本机随机端口启动实际 HTTP、TCP 服务，验证登记、版本匹配、退役期间通信、重复通知、最后一人离线和目录重启。游戏内显示效果另需实测。
