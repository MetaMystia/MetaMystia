# Gateway 与实例部署

只有一个 Gateway 和多个游戏实例。客户端连接 `play.metamystia.net:40815`；入口读取原有 Hello 的游戏、模组版本，原样转发到对应实例。每条连接固定使用同一后端，任一端断开或超时会关闭另一端。协议号仍由实例校验。

| 进程 | TCP | HTTP 管理 |
|---|---|---|
| Gateway | 公网 `40815` | 本机 `40900` |
| 游戏实例 | 本机 `41000–41099` | 本机 `42000–42099` |

管理 HTTP 强制绑定回环地址。外部通过 Nginx HTTPS 访问 Gateway，再由 Gateway 转发实例管理请求。Nginx 示例见 `tools/deploy/nginx-admin.conf.example`；只读接口使用 `api.metamystia.net`，只转发 `/api/`，示例见 `tools/deploy/nginx-api.conf.example`。没有 Directory 或定时查询目录。

## 管理接口

所有请求使用 `Authorization: Bearer <token>`。Gateway 使用管理令牌，实例使用单独的实例令牌；转发时 Gateway 自己携带实例令牌。令牌只保存在服务器配置，不进入 Git 或客户端。Gateway 另有管理令牌签发的只读令牌，只能访问 `/api/v1/`，访问其他路径返回 `403`。

管理面板是 `src/AdminPanel/` 下的静态文件，由 Nginx 托管在管理域名根路径，接口仍走 `/admin/`；域名不写入仓库。页面不含密钥，登录时输入管理令牌，保存在浏览器中。旧 `/v1/servers` 已删除。`play.metamystia.net` 仍用于直连游戏 TCP。Cloudflare 可能拦截默认脚本 User-Agent，接入时使用正常客户端 User-Agent，不关闭证书验证。

| Gateway 接口 | 行为与成功状态 |
|---|---|
| `GET /admin/status` | `200`，`{"connections":0}`，当前转发连接数 |
| `GET /admin/instances` | `200`，登记数组，包含 `id`、`registration`、`state`、`targetModVersion` |
| `PUT /admin/instances/{id}` | 验证实际实例身份与版本后登记并启用路由；`204` |
| `DELETE /admin/instances/{id}` | 删除登记，不关闭已有连接、不停止实例进程；`204` |
| `GET /admin/instances/{id}/status` | 转发实例状态查询；`200` |
| `POST /admin/instances/{id}/drain` | 先持久化停止接客，再转发退役请求；`202` |
| `GET\|POST\|PUT\|DELETE /admin/instances/{id}/api/{path}` | 校验实例身份后原样转发到实例 `/admin/{path}`；路径仅限小写字母、数字、`-`，不含 `status`、`drain`；实例无此接口时 `501` |
| `GET /admin/gateway` | 进程信息、当前连接数、累计接入 / 转发 / 无效 / 超限及按原因的拒绝数 |
| `GET /admin/connections?limit=` | 最近 2000 条有效握手的连接记录：时间、客户端 IP 与端口、版本、实例、结果、流量 |
| `GET /admin/logs?after=&limit=` | Gateway 日志（含管理操作），按序号增量读取 |
| `GET /admin/history` | 每分钟采样的连接数、转发数、拒绝数，保留 24 小时 |
| `GET /admin/tokens` | 只读令牌列表：`id`、`name`、`createdAt`、`lastUsedAt`，不含明文 |
| `POST /admin/tokens` | `{"name":"bot"}`，名称 1–64 字符；`201`，返回 `id`、`name`、`token`，明文只返回这一次；超过 100 个 `409` |
| `DELETE /admin/tokens/{id}` | 吊销，立即生效；`204` |

连接记录、日志与采样只在内存中保留，重启清空。客户端 IP 不写入 journald。

登记正文示例：

```json
{"gameVersion":"RELEASE 4.4.0e","modVersion":"0.30.1","host":"127.0.0.1","port":41000,"adminUrl":"http://127.0.0.1:42000"}
```

同一游戏与模组版本只能有一个接客实例；ID、地址不可覆盖。TCP 和管理地址只允许回环 IP，不接受任意公网转发目标。

退役正文示例：

```json
{"noticeId":"release-123","targetModVersion":"0.30.2"}
```

`noticeId` 只用于去重：同一 ID 客户端只提示一次，相同 ID 重发不会重复广播。发布流程使用新实例 ID，面板默认生成 `retire-to-<目标版本>`。

实例接口：

| 实例接口 | 行为 |
|---|---|
| `GET /admin/status` | 见下 |
| `POST /admin/drain` | 退役 |
| `GET /admin/details` | 进程信息、人数上限、玩家（UID、昵称、房间、阶段、地图、DLC、资源包、连接时间）、房间、退役与文本配置摘要 |
| `GET /admin/logs?after=&limit=` | 最近 2000 条实例日志，按序号增量读取 |
| `GET /admin/history` | 每分钟采样的人数、房间数、CPU、内存，保留 24 小时 |
| `POST /admin/players/{uid}/kick`、`POST /admin/players/{uid}/leave` | 踢出 / 移出房间；`204` |
| `PUT /admin/max-players` | `{"value":16}`，1–256；`204` |
| `POST /admin/say` | `{"message":"…"}`，服务器广播；`204` |
| `POST /admin/reload` | 重新读取 `server.json` 与 `chat-filter.json`；`204`，文件无效 `400` |
| `GET /admin/config/{name}` | 读取配置原文 `{"content":"…"}`；name 仅允许 `server`、`chat-filter` |
| `PUT /admin/config/{name}` | `{"content":"新原文","previousContent":"读取时的原文"}`；校验后保存并应用，成功 `204`，配置无效 `400`，原文件已变化 `409` |

操作失败返回 `409` 与 `{"error":"PlayerMissing"}` 这类错误码。实例新增接口只需随实例发布，Gateway 无需更新。

`GET /admin/status` 返回 `instanceId`、`gameVersion`、`modVersion`、`port`、`state`（`running` 或 `draining`）、`players`、`noticeId`、`targetModVersion`、`maxPlayers`、`rooms`。退役停止新玩家进入，通知已有玩家，无人后正常退出；没有强制下线期限。

错误状态：无效或缺少令牌 `401`，参数无效 `400`，登记不存在 `404`，版本或地址冲突 `409`，实例无法访问 `502`。退役转发失败时路由仍保持 `draining`，调用方可用相同正文重试；不运行后台重试任务。实例停止后可删除登记并清理实例目录。

## 只读接口

只读令牌与管理令牌均可访问。

| 接口 | 行为 |
|---|---|
| `GET /api/v1/status` | 当前转发连接数、在线人数合计、各登记实例的版本、路由状态与人数；结果缓存 5 秒 |
| `GET /api/v1/events?after=&wait=` | 长轮询路由变化事件 |

```json
{"time":"…","seq":3,"connections":2,"players":2,"instances":[{"id":"…","gameVersion":"RELEASE 4.4.0e","modVersion":"0.30.1","state":"active","targetModVersion":null,"reachable":true,"players":2,"maxPlayers":16,"rooms":1}]}
```

`state` 为路由状态 `active` 或 `draining`。实例无法访问时 `reachable` 为 `false`，人数字段为 `null`；旧版实例的 `maxPlayers`、`rooms` 为 `null`。不返回 IP、UID、昵称与配置。

事件 `type` 为 `registered`、`draining`、`removed`，字段为 `seq`、`time`、`type`、`instanceId`、`gameVersion`、`modVersion`、`targetModVersion`。`after` 必填；有更新的事件时立即返回，否则最多等待 `wait` 秒（默认 30，上限 60）后返回空数组。返回 `{"next":4,"reset":false,"events":[…]}`，每次最多 100 条，下次以 `next` 作为 `after`。`after` 大于当前序号或早于保留范围时返回 `reset: true`，应重新读取 `status`，从其 `seq` 继续。事件保留最近 500 条，Gateway 重启后序号延续。同一令牌同时最多 4 个事件请求，超出返回 `429`。

## 配置

| 进程 | 环境变量 | 默认值或用途 |
|---|---|---|
| Gateway | `MM_GATEWAY_ADDRESS`、`MM_GATEWAY_PORT` | `0.0.0.0`、`40815` |
| Gateway | `MM_ADMIN_URL` | `http://127.0.0.1:40900` |
| Gateway | `MM_GATEWAY_DATA` | 本地路由文件；部署时为 `/opt/metamystia-hosting/data/instances.json`。同目录保存 `tokens.json`（只读令牌的 SHA-256）与 `events.json` |
| Gateway | `MM_GATEWAY_TOKEN`、`MM_INSTANCE_TOKEN` | 管理令牌与实例令牌，必填 |
| Gateway | `MM_GATEWAY_MAX_CONNECTIONS` | `256` |
| 实例 | `MM_INSTANCE_ID`、`MM_ADMIN_URL`、`MM_INSTANCE_TOKEN` | 实例身份、管理监听地址、令牌 |
| 实例 | `MM_GAME_ADDRESS` | 独立运行默认 `0.0.0.0`，托管实例设为 `127.0.0.1` |

路由变更原子写入文件，运行中生效，已有连接不变；重启从文件恢复。无后台健康轮询，后端连接失败直接拒绝，不跨版本重试。首帧及连接后端限时 3 秒，双向读写限时 30 秒，心跳沿用游戏协议。

## 发布

`tools/deploy/` 提供 Gateway、实例 systemd 单元及部署脚本。`/etc/metamystia-hosting/deploy.json` 仅 root 可读，包含 `gatewayUrl`、`gatewayToken`、`instanceToken`。Gateway 环境配置放在同目录的 `gateway.env`。

游戏 Release 或手动 `Deploy server` 启动新实例，完成实际握手后向 Gateway 登记，再依次发送旧实例退役请求。部署锁避免并发，失败不覆盖已有实例目录。`replaces=auto` 选择当前接客实例，`-` 表示不退役。普通实例发布不停止或部署 Gateway。

Gateway 只有独立的 `Deploy gateway (manual)` 按钮，仅允许 main 分支并需勾选断线确认。先构建、测试、上传，之后才停止旧入口、替换并启动；保留路由文件。启动检查失败恢复上一版。此操作断开所有转发连接，不承诺无缝更新。systemd 不自动重启 Gateway。

管理面板由 `Deploy admin panel` 部署：`src/AdminPanel/` 推到 main 时自动运行，也可手动运行。服务器端 `deploy_panel.py` 只替换 `/var/www/metamystia-admin` 的静态文件，不重启任何服务。

实例操作页可编辑公告、聊天记录开关与敏感词配置，需实例支持配置接口；端口、令牌等启动参数仍通过部署配置修改。实例目录中的 `server.json`、`chat-filter.json` 是指向 `/opt/metamystia-hosting/config/` 的符号链接，各实例共用；该目录归 `metamystia` 所有并列入实例单元的 `ReadWritePaths`。保存只让当前实例立即应用，其他实例在重新加载或重启后生效。退役和删除登记须勾选对应实例的风险确认。Gateway 页可导出筛选后的连接记录和趋势 CSV，实例玩家页可导出当前全部玩家。令牌页签发、吊销只读令牌。连接记录只保留内存中最近 2000 条，趋势只保留最近 24 小时，重启清空；导出不等于服务器长期归档。

部署 SSH forced command 只接受 `deploy-instance`、`deploy-gateway` 和 `deploy-panel`；sudo 只授权对应 Python 脚本。Nginx 继续使用已有 HTTPS 证书，对公网只开放 `40815` 和 HTTPS，实例端口不开放防火墙。

## 验证

`MetaMystia.Hosting.Tests` 使用实际本机 TCP/HTTP 验证管理鉴权、只读令牌、事件长轮询、注册、版本隔离、房间操作、通用转发、连接记录、路由持久化、退役及双向断线清理。Linux 上运行 `python3 -m unittest discover -s tools/deploy -p 'test_*.py'` 检查部署脚本。游戏画面另需实测。
