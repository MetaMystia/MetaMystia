# 料理对决

房主 H、客机 C 读同一份存档（需 DLC3），白天同房；用例 16 另加客机 W。脚本名指 `actions/` 下的文件。用例 1–5 在第 1 阶段（骨架）、6–9 在第 2 阶段（对抗）、10–16 在第 3 阶段（隔离与公平）通过（运行时实测，同机两到三实例）。`versus MODE=desks` 输出本端占桌表（玩家方、Boss 方）、比分与连击。慢节奏（`mp versus slow`）留给脚本操作的时间较多；用 `patience ID=<编号> VALUE=125` 防止顾客中途离开。

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

## 10 统一库存与增益

- 操作：开场后两端 `versus MODE=stock`。
- 期望：两端食材、酒水条目数与校验和相同；`qteOff=True speedBuffs=0`。
- 依据：两端日志 `versus: stock applied`。

## 11 保温箱

- 操作：H `versus MODE=warmer INDEX=<料理ID>`，两端 `versus MODE=warmers`；C 同样放入后再查。
- 期望：H 放入后 H 为 1、C 为 0；C 放入后两端各为 1。

## 12 厨具成品

- 操作：H `versus MODE=cook INDEX=<格子>`，等 C `versus MODE=cooker` 为 `Finished`；C `versus MODE=take INDEX=<格子>`，再 H 同样操作。
- 期望：C 侧 `canExtract=False`，取后仍为 `Finished`、托盘不变；H 取后托盘多 1 件，两端厨具为 `Idle`、`canExtract=True`。

## 13 放入即占桌

- 操作：C `serve MODE=open DESK=A`、`serve MODE=stage DESK=A`（不关闭）；两端 `versus MODE=desks`；H `serve MODE=open DESK=A`。
- 期望：C `player=A`，H `boss=A:<Boss ID>`；H 打不开桌 A（`panel=-1`）。

## 14 差评、赶客与耐心耗尽

- 操作：两端各上一桌 `serve MODE=good` 使两队连击为 1；H `patience ID=<未占桌顾客> VALUE=0`；两队再各上一桌；C `serve MODE=repell DESK=<桌>`；C 给另一桌 `serve MODE=bad`，H 给另一桌 `serve MODE=bad`。
- 期望：未占桌顾客耐心耗尽后两队连击都为 0；C 赶客后只有蓝队连击归零；差评付款分别计入上菜者的队。

## 15 伙伴外观

- 操作：伙伴选择时 H `versus MODE=partners` 后 `versus MODE=pickpartner INDEX=<伙伴ID>`，C `versus MODE=partner`；两端 `versus MODE=skins`。结束回白天后再查。
- 期望：H `partnerSkin=<伙伴ID>`，C 看到 H 的皮肤为该 ID，且 C 端 H 角色显示该伙伴；回白天后 `partnerSkin` 为空，皮肤恢复为 -1。

## 16 旁观

- 操作：三实例同房，W 选「旁观」，H、C 分选红、蓝。开场后 W `versus MODE=interact`、H 同样操作；W 给一桌 `serve MODE=good`；H、C 各给一桌 `serve MODE=goodfood` 后三端 `versus MODE=desks`；等结束。
- 期望：W `interact started=False`，H `started=True`；W 的上菜被房主拒绝；W 的占桌表与 H 相同（红队为玩家方）；三端结算面板与公告相同。
- 依据：H 日志 `from <W 的 uid> rejected`。
