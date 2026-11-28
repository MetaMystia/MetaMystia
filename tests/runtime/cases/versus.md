# 料理对决

房主 H、客机 C 读同一份存档（需 DLC3），白天同房。脚本名指 `actions/` 下的文件。用例 1–5 在第 1 阶段（骨架）通过（运行时实测，同机两实例）。

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
