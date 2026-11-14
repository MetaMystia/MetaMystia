# Gateway 与实例部署

只有一个 Gateway 和多个游戏实例。客户端连接 `play.metamystia.net:40815`；入口读取原有 Hello 的游戏、模组版本，原样转发到对应实例。每条连接固定使用同一后端，任一端断开或超时会关闭另一端。协议号仍由实例校验。

| 进程 | TCP | HTTP 管理 |
|---|---|---|
| Gateway | 公网 `40815` | 本机 `40900` |
| 游戏实例 | 本机 `41000–41099` | 本机 `42000–42099` |

管理 HTTP 强制绑定回环地址。外部通过 Nginx HTTPS 访问 Gateway，再由 Gateway 转发实例管理请求。Nginx 示例见 `tools/deploy/nginx-admin.conf.example`。没有 Directory、公开订阅或定时查询目录。

## 管理接口

所有请求使用 `Authorization: Bearer <token>`。Gateway 使用管理令牌，实例使用单独的实例令牌；转发时 Gateway 自己携带实例令牌。令牌只保存在服务器配置，不进入 Git 或客户端。

`GET /admin/` 为管理面板，页面本身不需令牌，登录时输入管理令牌，保存在浏览器会话中；可查看实例、登记、退役、删除。

线上管理入口为 Nginx HTTPS 域名下的 `/admin/`，域名不写入仓库；旧 `/v1/servers` 已删除。`play.metamystia.net` 仍用于直连游戏 TCP。Cloudflare 可能拦截默认脚本 User-Agent，接入时使用正常客户端 User-Agent，不关闭证书验证。

| Gateway 接口 | 行为与成功状态 |
|---|---|
| `GET /admin/status` | `200`，`{"connections":0}`，当前转发连接数 |
| `GET /admin/instances` | `200`，登记数组，包含 `id`、`registration`、`state`、`targetModVersion` |
| `PUT /admin/instances/{id}` | 验证实际实例身份与版本后登记并启用路由；`204` |
| `DELETE /admin/instances/{id}` | 删除登记，不关闭已有连接、不停止实例进程；`204` |
| `GET /admin/instances/{id}/status` | 转发实例状态查询；`200` |
| `POST /admin/instances/{id}/drain` | 先持久化停止接客，再转发退役请求；`202` |

登记正文示例：

```json
{"gameVersion":"RELEASE 4.4.0e","modVersion":"0.30.1","host":"127.0.0.1","port":41000,"adminUrl":"http://127.0.0.1:42000"}
```

同一游戏与模组版本只能有一个接客实例；ID、地址不可覆盖。TCP 和管理地址只允许回环 IP，不接受任意公网转发目标。

退役正文示例：

```json
{"noticeId":"release-123","targetModVersion":"0.30.2"}
```

实例仅提供 `GET /admin/status` 和 `POST /admin/drain`。状态返回 `instanceId`、`gameVersion`、`modVersion`、`port`、`state`（`running` 或 `draining`）、`players`、`noticeId`、`targetModVersion`。退役停止新玩家进入，通知已有玩家，无人后正常退出；没有强制下线期限。

错误状态：无效或缺少令牌 `401`，参数无效 `400`，登记不存在 `404`，版本或地址冲突 `409`，实例无法访问 `502`。退役转发失败时路由仍保持 `draining`，调用方可用相同正文重试；不运行后台重试任务。实例停止后可删除登记并清理实例目录。

## 配置

| 进程 | 环境变量 | 默认值或用途 |
|---|---|---|
| Gateway | `MM_GATEWAY_ADDRESS`、`MM_GATEWAY_PORT` | `0.0.0.0`、`40815` |
| Gateway | `MM_ADMIN_URL` | `http://127.0.0.1:40900` |
| Gateway | `MM_GATEWAY_DATA` | 本地路由文件；部署时为 `/opt/metamystia-hosting/data/instances.json` |
| Gateway | `MM_GATEWAY_TOKEN`、`MM_INSTANCE_TOKEN` | 管理令牌与实例令牌，必填 |
| Gateway | `MM_GATEWAY_MAX_CONNECTIONS` | `256` |
| 实例 | `MM_INSTANCE_ID`、`MM_ADMIN_URL`、`MM_INSTANCE_TOKEN` | 实例身份、管理监听地址、令牌 |
| 实例 | `MM_GAME_ADDRESS` | 独立运行默认 `0.0.0.0`，托管实例设为 `127.0.0.1` |

路由变更原子写入文件，运行中生效，已有连接不变；重启从文件恢复。无后台健康轮询，后端连接失败直接拒绝，不跨版本重试。首帧及连接后端限时 3 秒，双向读写限时 30 秒，心跳沿用游戏协议。

## 发布

`tools/deploy/` 提供 Gateway、实例 systemd 单元及部署脚本。`/etc/metamystia-hosting/deploy.json` 仅 root 可读，包含 `gatewayUrl`、`gatewayToken`、`instanceToken`。Gateway 环境配置放在同目录的 `gateway.env`。

游戏 Release 或手动 `Deploy server` 启动新实例，完成实际握手后向 Gateway 登记，再依次发送旧实例退役请求。部署锁避免并发，失败不覆盖已有实例目录。`replaces=auto` 选择当前接客实例，`-` 表示不退役。普通实例发布不停止或部署 Gateway。

Gateway 只有独立的 `Deploy gateway (manual)` 按钮，仅允许 main 分支并需勾选断线确认。先构建、测试、上传，之后才停止旧入口、替换并启动；保留路由文件。启动检查失败恢复上一版。此操作断开所有转发连接，不承诺无缝更新。systemd 不自动重启 Gateway。

部署 SSH forced command 只接受 `deploy-instance` 和 `deploy-gateway`；sudo 只授权对应 Python 脚本。Nginx 继续使用已有 HTTPS 证书，对公网只开放 `40815` 和 HTTPS，实例端口不开放防火墙。

## 验证

`MetaMystia.Hosting.Tests` 使用实际本机 TCP/HTTP 验证管理鉴权、注册、版本隔离、房间操作、路由持久化、退役及双向断线清理。Linux 上运行 `python3 -m unittest discover -s tools/deploy -p 'test_*.py'` 检查部署脚本。游戏画面另需实测。
