# 料理对决

房主 H、客机 C 读同一份存档（需 DLC3），白天同房。脚本名指 `actions/` 下的文件。用例 1–5 在第 1 阶段（骨架）、6–9 在第 2 阶段（对抗）通过（运行时实测，同机两实例）。`versus MODE=desks` 输出本端占桌表（玩家方、Boss 方）、比分与连击。

## 1 邀请与选队

- 操作：C 入房后 H `console CMD="mp versus normal"`；两端 `versus MODE=menu INDEX=0`。
- 期望：两端自动弹出选队菜单（红、蓝、旁观、暂不参加）；H 为红队，C 为蓝队。
- 依据：C 日志 `Received VersusStateMessage: {"Open":true`。

## 2 入口

- 操作：H `versus MODE=pick INDEX=0`，C `versus MODE=pick INDEX=1`。
- 期望：两端进入夜间，房间保持，`versus MODE=state` 为 `dest=Versus difficulty=Normal waiting=True`，顶层面板为伙伴选择。
- 依据：两端日志 `Confirmed day destination Versus`、`versus: boss partner skipped`，无 `禁止关闭当前面板`。

## 3 开场等待

- 操作：H `versus MODE=partner INDEX=0`，等数秒后 C 同样操作。
- 期望：只有 H 选完时两端 `waiting=True`、倒计时不动；C 选完后两端 `waiting=False` 并开始上客，两端顾客编号与桌号一致。
- 依据：H 日志来自 C 的 `Received BusinessStartMessage: {"Start":false` 后 `Send BusinessStartMessage: {"Start":true`。

## 4 结束与返回

- 操作：等房主倒计时归零，两端 `versus MODE=result INDEX=0`。
- 期望：两端同时出现结算面板；关闭后回到同一天白天，房间保持，`dest=None`；日期、资金、剩余行动次数与开局前相同。
- 依据：H `Send VersusEndMessage` → C `Received VersusEndMessage`。

## 5 暂不参加与命令选队

- 操作：H `mp versus fast`，H 选红队，C 选「暂不参加」；随后 C `console CMD="mp team blue"`。
- 期望：C 暂不参加时不开局，C 仍在蓝队；C 用命令选队后进入快节奏对局（`challenge=Story_Futo_TempleCuisineCompetition`）。

## 6 占桌与计分

- 操作：开场后 H 给桌 A `serve MODE=good`，C 给桌 B `serve MODE=good`，两位顾客付款离开后两端 `versus MODE=desks`。
- 期望：H 上菜后 H `player=A`、C `boss=A:<Boss ID>`；C 上菜后反之。付款后 H `earned` 为红队、`bossScore` 为蓝队，C 相反；两队连击分开；顾客离开后两端占桌表清空。
- 依据：H 日志 `versus: boss side registered`、`Send VersusDeskMessage` 依次为占桌与 `"Team":"None"` 释放；C 对应 `Received`。

## 7 他队的桌

- 操作：H 给桌 A `serve MODE=goodfood`（只上菜，占桌保持）；C `serve MODE=open DESK=A`。再由 C 打开空桌 B 的面板，H 给桌 B `serve MODE=goodfood`。
- 期望：C 打不开桌 A（`panel=-1`）；H 占下桌 B 时 C 的面板被关闭（`panel=-1`），C 放入的物品退回托盘。房主 `VersusMatch.CanServe(蓝队成员, A)` 为 false。

## 8 结算

- 操作：用例 6 或 7 之后等房主倒计时归零。
- 期望：结算前 C 收到最终比分；两端结算面板「我方得分」为本队、「对手得分」为他队；两端控制台公告获胜队与比分。
- 依据：H `Send VersusScoreMessage: {... "Final":true` → C `Received`。

## 9 平局

- 操作：开局后两队都不上菜，等结束。
- 期望：两端公告平局（双方各 0 分）。

