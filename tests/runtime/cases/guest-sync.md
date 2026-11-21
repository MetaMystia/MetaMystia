# 顾客同步

房主 H、客机 C 读同一份存档，两端 `worktime SECONDS=1200` 后共同进入普通营业。N 为等待上菜的顾客编号，D 为其桌号，Q 为排队顾客编号，均从 `guests` 读取。

通用判定：

- 每个用例前后两端各取一次 `guests`。两端一致指同一编号的阶段和订单序号相同；入座、吃饭途中可暂时不同，5 秒内应一致。
- 两端日志没有异常堆栈，H 没有 `dialog hook missed`。除用例 15 外，C 没有 `still held by`、`not queued`、`missing locally`。

## 1 生成、入座与点单

- 操作：H `spawn COUNT=1`，等 15 秒（到座与首单延迟）。
- 期望：两端出现同一编号，桌号相同，阶段 `WaitingServe`，序号 1；两端 `guest MODE=info` 的 order 相同。
- 依据：H 依次 `Send GuestSpawnMessage`、`Send GuestSeatMessage`（`"FromQueue":false`）、`Send GenerateOrderMessage`（`"OrderSeq":1`）；C 有对应的 `Received`。

## 2 排队与出队入座

- 操作：H 重复 `spawn COUNT=4` 直到 `queue=` 不为 0；H 对任一 N `serve MODE=repell DESK=D`。
- 期望：Q 在两端为 `Queued`；D 空出后两端 Q 坐到同一桌。
- 依据：H `Send MoveToQueueMessage`；之后 `Send GuestSeatMessage` 带 `"FromQueue":true`。

## 3 排队耐心耗尽

- 操作：有 Q 时 H `patience ID=Q VALUE=0`。
- 期望：两端 Q 离开，`queue=` 相同。
- 依据：H `Send GuestLeaveMessage` 带 `"Reason":"Unseated"`（`MoveToSpawn` Hook 生效）。

## 4 桌上耐心耗尽

- 操作：H `patience ID=N VALUE=0`。
- 期望：两端 N 各离开一次，`night` 的 `occ` 相同。
- 依据：H `Send GuestLeaveMessage` 带 `"Reason":"PatientDepleted"`。

## 5 客机上菜与评价

- 操作：C `serve MODE=open DESK=D`、`serve MODE=send DESK=D`。
- 期望：两端评价结果相同；之后两端都续单（序号加 1、`WaitingServe`）或都离开；两端 `combo`、`lose`、`earned` 相同。
- 依据：H `Received ConfirmServeMessage`、`Send EvaluateOrderMessage`（`EvalResult`）；C `Received EvaluateOrderMessage`。续单时 H `Send GenerateOrderMessage` 序号加 1；离开时 `"Reason":"Desk"`。两种结局分开记录。

## 6 房主上菜与评价

- 操作：H `serve MODE=open DESK=D`、`serve MODE=send DESK=D`。
- 期望：同用例 5；C 在评价前 `guest MODE=info` 的 served 与 H 相同。

## 7 客机赶客

- 操作：C `serve MODE=repell DESK=D`。
- 期望：C 的 N 立即离开；H 的 N 随后离开一次；H 的 `earned` 只变化一次，C 与 H 相同；没有护盾时两端 `combo=0`。
- 依据：C `Send PlayerRepellMessage`（`"Branch":"Normal"`）；H `Send GuestLeaveMessage`（`"Reason":"Repel"`）；C 收到后不再重放离场。

## 8 房主赶客

- 操作：H `serve MODE=repell DESK=D`。
- 期望：C 的 N 离开一次。
- 依据：H `Send GuestLeaveMessage` 带 `"Reason":"Repel"` 与赶客后的 `Mood`；C `Received GuestLeaveMessage`。

## 9 两端同时赶客

- 操作：两端对同一 D 紧接着各发一次 `serve MODE=repell DESK=D`。
- 期望：两端 N 各离开一次；H 的 `earned` 只变化一次。
- 依据：分开记录先后：H 先赶走时，H 有 `Repel of #N ... ignored`；C 的请求先到时，H 本地赶客不执行。两种情况 H 都只有一条 `"Reason":"Repel"`。

## 10 取消关闭面板

- 操作：C `serve MODE=open DESK=D`、`serve MODE=stage DESK=D`，`tray`；C `serve MODE=cancel DESK=D`，`tray`。
- 期望：C 托盘多出放入的料理和酒水各一份（原版只退料理）；H `guest MODE=info ID=N` 的 slot 为 `-/-`；N 未评价。
- 依据：C 两条 `Send ServeSellableMessage` 带 `"Requested":null`。

## 11 面板打开时顾客离开

- 操作：C `serve MODE=open DESK=D`、`serve MODE=stage DESK=D`，`tray`；H `patience ID=N VALUE=0`。
- 期望：C `panel=-1`，托盘收回两件；N 离开时没有评价。

## 12 放入被拒

- 操作：H `serve MODE=open DESK=D`、`serve MODE=stage DESK=D`（不关闭）；C `guest MODE=unslot ID=N`，`serve MODE=open DESK=D`、`serve MODE=stage2 DESK=D`，`tray`。
- 期望：C 托盘收回 `stage2` 的两件，C 面板为空；H `guest MODE=info ID=N` 的 slot 仍为 H 放入的物品。
- 依据：H `Serve #N/... rejected`；C `Received ServeRejectMessage` 带 `"Confirm":false`。

## 13 确认被拒

- 操作：C `serve MODE=open DESK=D`、`serve MODE=stage DESK=D`；H `guest MODE=unslot ID=N`；C `serve MODE=close DESK=D`，`tray`。
- 期望：C 托盘收回两件；C `guest MODE=info ID=N` 的 served、conf 均为 `-/-`；N 未评价。
- 依据：H `Confirm #N/... rejected`；C `Received ServeRejectMessage` 带 `"Confirm":true`。

## 14 代为确认

- 操作：C `serve MODE=open DESK=D`、`serve MODE=stage DESK=D`（不关闭），`tray`；H `serve MODE=open DESK=D`、`serve MODE=close DESK=D`。
- 期望：N 评价一次；C `panel=-1`，C、H 托盘都不增加。
- 依据：H `Send ConfirmServeMessage`；C `Received ConfirmServeMessage`、`Received EvaluateOrderMessage`。

## 15 客机卡顿

- 操作：C `block SECONDS=20`；期间 H `spawn COUNT=2`，并对一位 N `serve MODE=open`、`serve MODE=send`。
- 期望：C 恢复后 5 秒内两端 `guests` 一致，没有残留或重复的顾客。
- 依据：C 恢复后集中出现对应的 `Received`；桌位被占时可有 `still held by`，之后应一致。

## 16 快照重建

- 操作：C `guest MODE=forget ID=N`；H `serve MODE=open DESK=D`、`serve MODE=send DESK=D`。
- 期望：C 在同一桌重建 N；之后与 H 一起续单或离开。
- 依据：C `Guest #N missing locally, requesting snapshot`；H `Received GuestQueryMessage`；C `Received GuestSnapshotMessage`。

## 17 数值覆盖

- 操作：H `patience ID=N VALUE=40`，C `guest MODE=info ID=N`；C `patience ID=N VALUE=90`，等 2 秒后 C 再查。
- 期望：C 的耐心跟随 H（不高于 40）；C 自设的值在 H 下一次耐心变化后回到 H 的值；两端 mood、fund、remain 相同。

## 18 打烊时有排队

- 操作：有 Q 时 H `close_izakaya`。
- 期望：两端排队顾客离开；桌上顾客离开后两端进入结算。
- 依据：H 对每位排队顾客 `Send GuestLeaveMessage` 带 `"Reason":"Unseated"`；C `Received close command from host`。

## 19 客机断线

- 操作：C 有等待上菜、吃饭中和排队的顾客时，C `console CMD="mp stop"`。
- 期望：C 的顾客按原版继续点单、评价、离开，排队顾客能入座；C 打烊后正常结算，没有残留占桌。
- 依据：C 对每位在场顾客记 `handed back to vanilla at <阶段>`。

## 20 连续两夜

- 操作：用例 18 结算后推进到次日，再共同营业，H `spawn COUNT=1`。
- 期望：两端编号从 1 开始且一致；前一夜的顾客事件不影响本夜。

## 未覆盖

- 剧情期间暂存与剧情顾客、灵梦保护：需要触发夜间剧情的存档。
- 带 buff 赶客（计次免罚、琪斯美）和符卡驱逐（`FlyToSpawn`）：暂无获取 buff、发动符卡的动作。
- 幽幽子挑战：按[幽幽子挑战同步](../../../docs/reference/multiplayer/yuyuko-challenge.md)回归。
