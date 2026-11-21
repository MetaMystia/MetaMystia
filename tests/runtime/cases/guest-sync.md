# 顾客同步

房主 H、客机 C 读同一份存档，各端 `worktime SECONDS=1200` 后共同进入普通营业。三人用例另加客机 C2，H 在开房前执行 `console CMD="mp maxplayers 3"`。N 为等待上菜的顾客编号，D 为其桌号，Q 为排队顾客编号，均从 `guests` 读取。

通用判定：

- 每个用例前后各端取一次 `guests`。各端一致指同一编号的阶段和订单序号相同；入座、吃饭途中可暂时不同，5 秒内应一致。
- 结束后各端 `combo`、`lose`、`earned` 相同，`night` 的 `occ` 相同。
- 各端日志没有异常堆栈，H 没有 `dialog hook missed`，没有 `from unexpected phase`。除用例 25、27 外，客机没有 `still held by`、`not queued`、`missing locally`。

## 生成与入座

### 1 生成、入座与点单

- 操作：H `spawn MODE=normal COUNT=1`，等 15 秒（到座与首单延迟）。
- 期望：两端出现同一编号，桌号相同，阶段 `WaitingServe`，序号 1；两端 `guest MODE=info` 的 order 相同。
- 依据：H 依次 `Send GuestSpawnMessage`、`Send GuestSeatMessage`（`"FromQueue":false`）、`Send GenerateOrderMessage`（`"OrderSeq":1`）；C 有对应的 `Received`。

### 2 排队与出队入座

- 操作：H 重复 `spawn MODE=normal COUNT=4` 直到 `queue=` 不为 0；H 对任一 N `serve MODE=repell DESK=D`。
- 期望：Q 在两端为 `Queued`；D 空出后两端 Q 坐到同一桌。
- 依据：H `Send MoveToQueueMessage`；之后 `Send GuestSeatMessage` 带 `"FromQueue":true`。

### 3 排队耐心耗尽

- 操作：有 Q 时 H `patience ID=Q VALUE=0`。
- 期望：两端 Q 离开，`queue=` 相同。
- 依据：H `Send GuestLeaveMessage` 带 `"Reason":"Unseated"`（`MoveToSpawn` Hook 生效）。

### 4 桌上耐心耗尽

- 操作：H `patience ID=N VALUE=0`。
- 期望：两端 N 各离开一次。
- 依据：H `Send GuestLeaveMessage` 带 `"Reason":"PatientDepleted"`。

### 5 多人一组

- 操作：H `spawn MODE=group COUNT=1`，到座点单后两端 `guest MODE=info`；H 对其 `serve MODE=open`、`serve MODE=good`，续单时再查一次。
- 期望：两端人数相同，每一单的 `by=` 相同。
- 依据：H `Send GuestSpawnMessage` 的 `Ids` 有两个编号；`Send GenerateOrderMessage` 的 `OrderingGuest` 与两端 `by=` 的序号一致。

## 点单与评价

### 6 好评续单

- 操作：C `serve MODE=open DESK=D`、`serve MODE=good DESK=D`。
- 期望：两端评价结果相同，之后两端都续单（序号加 1、`WaitingServe`）或都离开。
- 依据：H `Received ConfirmServeMessage`、`Send EvaluateOrderMessage`（`EvalResult` 为 `Normal` 及以上）；C `Received EvaluateOrderMessage`。续单时 H `Send GenerateOrderMessage` 序号加 1；离开时 `"Reason":"Desk"`。两种结局分开记录。

### 7 差评离场

- 操作：H `serve MODE=open DESK=D`、`serve MODE=bad DESK=D`。
- 期望：两端 N 离开一次，两端 `combo=0`（没有护盾时）。
- 依据：H `Send EvaluateOrderMessage` 带 `"EvalResult":"Exbad"`，之后 `Send GuestLeaveMessage`（`"Reason":"Desk"`）；C 先 `Received EvaluateOrderMessage`，后 `Received GuestLeaveMessage`，中间 N 不离桌。

### 8 首单点不起

- 操作：H `spawn MODE=broke COUNT=1`，等 15 秒。
- 期望：两端该顾客到座后都没有订单并离开一次；C 显示差评台词。
- 依据：H `Send GenerateOrderMessage` 带 `"Result":"NoMoney"`，没有 `Send EvaluateOrderMessage`，之后 `"Reason":"Desk"`。

### 9 稀客点单与评价

- 操作：H `spawn MODE=special COUNT=1`，到座点单后 C `serve MODE=open`、`serve MODE=good`。
- 期望：两端稀客的 order 相同；评价结果相同，之后两端都续单或都离开。
- 依据：H `Send GenerateOrderMessage` 带 `"OrderType":"Special"`；`Send EvaluateOrderMessage`。

### 10 稀客资金不足

- 操作：稀客 N 等待上菜时 H `fund ID=N MODE=nomoney VALUE=0`；H `serve MODE=open`、`serve MODE=good`。
- 期望：评价后两端 N 不续单并离开一次；C 显示普通评价的没钱台词。
- 依据：H 下一条 `Send GenerateOrderMessage` 带 `"OverrideResult":"NoMoney"`；之后 `"Reason":"Desk"`。`fund` 返回 `cannot fit` 时本例不适用，换一位稀客。

### 11 稀客超出承受

- 操作：稀客 N 等待上菜时 H `fund ID=N MODE=max VALUE=0`；H `serve MODE=open`、`serve MODE=good`。
- 期望：评价后两端 N 愤怒离场一次。
- 依据：H 下一条 `Send GenerateOrderMessage` 带 `"OverrideResult":"ExceedEndurance"`，约 1 秒后 `"Reason":"Desk"`。另记两端是否出现离场负面 buff（各端本地触发，不同步）。

## 上菜

### 12 房主上菜

- 操作：H `serve MODE=open DESK=D`、`serve MODE=good DESK=D`。
- 期望：同用例 6；C 在评价前 `guest MODE=info` 的 served 与 H 相同。

### 13 分次上菜

- 操作：C `serve MODE=open DESK=D`、`serve MODE=goodfood DESK=D`；两端 `guest MODE=info ID=N`；C `serve MODE=open DESK=D`、`serve MODE=goodbev DESK=D`。
- 期望：第一次关闭后两端 served 为 `料理/-`，N 未评价；第二次关闭后评价一次。C 的托盘不增加。
- 依据：H 两条 `Received ConfirmServeMessage`，只有第二条之后有 `Send EvaluateOrderMessage`。

### 14 取消关闭面板

- 操作：C `serve MODE=open DESK=D`、`serve MODE=stage DESK=D`，`tray`；C `serve MODE=cancel DESK=D`，`tray`。
- 期望：C 托盘多出放入的料理和酒水各一份（原版只退料理）；H `guest MODE=info ID=N` 的 slot 为 `-/-`；N 未评价。
- 依据：C 两条 `Send ServeSellableMessage` 带 `"Requested":null`。

### 15 面板打开时顾客离开

- 操作：C `serve MODE=open DESK=D`、`serve MODE=stage DESK=D`，`tray`；H `patience ID=N VALUE=0`。
- 期望：C `panel=-1`，托盘收回两件；N 离开时没有评价。

### 16 放入被拒

- 操作：H `serve MODE=open DESK=D`、`serve MODE=stage DESK=D`（不关闭）；C `guest MODE=unslot ID=N`，`serve MODE=open DESK=D`、`serve MODE=stage2 DESK=D`，`tray`。
- 期望：C 托盘收回 `stage2` 的两件，C 面板为空；H `guest MODE=info ID=N` 的 slot 仍为 H 放入的物品。
- 依据：H `Serve #N/... rejected`；C `Received ServeRejectMessage` 带 `"Confirm":false`。

### 17 确认被拒

- 操作：C `serve MODE=open DESK=D`、`serve MODE=stage DESK=D`；H `guest MODE=unslot ID=N`；C `serve MODE=close DESK=D`，`tray`。
- 期望：C 托盘收回两件；C `guest MODE=info ID=N` 的 served、conf 均为 `-/-`；N 未评价。
- 依据：H `Confirm #N/... rejected`；C `Received ServeRejectMessage` 带 `"Confirm":true`。

### 18 代为确认

- 操作：C `serve MODE=open DESK=D`、`serve MODE=stage DESK=D`（不关闭），`tray`；H `serve MODE=open DESK=D`、`serve MODE=close DESK=D`。
- 期望：N 评价一次；C `panel=-1`，C、H 托盘都不增加。
- 依据：H `Send ConfirmServeMessage`；C `Received ConfirmServeMessage`、`Received EvaluateOrderMessage`。

## 赶客

### 19 客机赶客

- 操作：C `serve MODE=repell DESK=D`。
- 期望：C 的 N 立即离开；H 的 N 随后离开一次；`earned` 只变化一次；没有护盾时两端 `combo=0`。
- 依据：C `Send PlayerRepellMessage`（`"Branch":"Normal"`）；H `Send GuestLeaveMessage`（`"Reason":"Repel"`）；C 收到后不再重放离场。

### 20 房主赶客

- 操作：H `serve MODE=repell DESK=D`。
- 期望：C 的 N 离开一次。
- 依据：H `Send GuestLeaveMessage` 带 `"Reason":"Repel"` 与赶客后的 `Mood`。

### 21 两端同时赶客

- 操作：两端对同一 D 紧接着各发一次 `serve MODE=repell DESK=D`。
- 期望：两端 N 各离开一次；`earned` 只变化一次。
- 依据：分开记录先后：H 先赶走时，H 有 `Repel of #N ... ignored`；C 的请求先到时，H 本地赶客不执行。两种情况 H 都只有一条 `"Reason":"Repel"`。

## 时序

### 22 客机卡顿

- 操作：C `block SECONDS=20`；期间 H `spawn MODE=normal COUNT=2`，并对一位 N `serve MODE=open`、`serve MODE=good`。
- 期望：C 恢复后 5 秒内两端 `guests` 一致，没有残留或重复的顾客。
- 依据：C 恢复后集中出现对应的 `Received`；桌位被占时可有 `still held by`，之后应一致。

### 23 快照重建

- 操作：C `guest MODE=forget ID=N`；H `serve MODE=open DESK=D`、`serve MODE=good DESK=D`。
- 期望：C 在同一桌重建 N；之后与 H 一起续单或离开。
- 依据：C `Guest #N missing locally, requesting snapshot`；H `Received GuestQueryMessage`；C `Received GuestSnapshotMessage`。

### 24 数值覆盖

- 操作：H `patience ID=N VALUE=40`，C `guest MODE=info ID=N`；C `patience ID=N VALUE=90`，等 2 秒后 C 再查。
- 期望：C 的耐心跟随 H（不高于 40）；C 自设的值在 H 下一次耐心变化后回到 H 的值；两端 mood、fund、remain 相同。

## 场景边界

### 25 打烊时有排队

- 操作：有 Q 时 H `close_izakaya`。
- 期望：两端排队顾客离开；桌上顾客离开后两端进入结算。
- 依据：H 对每位排队顾客 `Send GuestLeaveMessage` 带 `"Reason":"Unseated"`；C `Received close command from host`。

### 26 客机断线

- 操作：C 有等待上菜、吃饭中和排队的顾客时，C `console CMD="mp stop"`。
- 期望：C 的顾客按原版继续点单、评价、离开，排队顾客能入座；C 打烊后正常结算，没有残留占桌。
- 依据：C 对每位在场顾客记 `handed back to vanilla at <阶段>`。

### 27 房主断线

- 操作：同用例 26 的局面，H `console CMD="mp stop"`。
- 期望：同用例 26，C 转为单机继续营业。

### 28 连续两夜

- 操作：用例 25 结算后推进到次日，再共同营业，H `spawn MODE=normal COUNT=1`。
- 期望：两端编号从 1 开始且一致；前一夜的顾客事件不影响本夜。

## 三人

### 29 三端一致

- 操作：H `spawn MODE=normal COUNT=3`、`spawn MODE=special COUNT=1`，等 20 秒。
- 期望：三端 `guests` 一致。

### 30 客机上菜，另一客机同步

- 操作：C `serve MODE=open DESK=D`、`serve MODE=good DESK=D`。
- 期望：C2 的 N 评价一次，结果与 H 相同；C 的 slot 没有被自己的回传重复写入，C 托盘不增加。
- 依据：H `Send ServeSellableMessage` 与 `Send ConfirmServeMessage`（`ActorUid` 为 C）；C2 有对应的 `Received`。

### 31 两客机争用同一槽位

- 操作：C、C2 都 `serve MODE=open DESK=D`；C `serve MODE=stage DESK=D`，紧接着 C2 `serve MODE=stage2 DESK=D`；三端 `guest MODE=info ID=N`，C、C2 各 `tray`。
- 期望：三端 slot 相同，为先到的一端放入的物品；后到的一端托盘收回两件、面板为空。
- 依据：H 对后到的一端 `Serve #N/... rejected`；该端 `Received ServeRejectMessage`。

### 32 客机赶客，另一客机同步

- 操作：C `serve MODE=repell DESK=D`。
- 期望：C2 的 N 离开一次；`earned` 只变化一次。
- 依据：C2 `Received GuestLeaveMessage` 带 `"Reason":"Repel"`。

### 33 一个客机断线

- 操作：C2 `console CMD="mp stop"`；之后 H `spawn MODE=normal COUNT=1`，C 对一位 N 上菜。
- 期望：H 与 C 继续一致；C2 交还原版后独立营业。

## 未覆盖

- 剧情期间暂存与剧情顾客、灵梦保护：需要触发夜间剧情的存档。
- 带 buff 赶客（计次免罚、琪斯美）、符卡驱逐（`FlyToSpawn`）、投掷上菜：暂无获取 buff、发动符卡、投掷的动作。
- 同桌两端同时确认或取消造成的复制：已知，未处理。
- 幽幽子挑战：按[幽幽子挑战同步](../../../docs/reference/multiplayer/yuyuko-challenge.md)回归。
