# 普通营业

房主 H、客机 C 读同一份存档，两端 `worktime SECONDS=1200` 后共同进入普通营业。脚本名指 `actions/` 下的文件。用例 1–8 在 0.31.4（#138）全部通过（运行时实测，同机两实例）。

## 1 开店等待

- 操作：C 提交备菜后执行 `block SECONDS=45`，H 先进夜间。
- 期望：C 阻塞期间 H 的 `night` 为 `wait=True`、`occ=0`；C 恢复后两端 `wait=False`，`cd` 下降。
- 依据：H 日志依次为 `gameTotalSeconds set to`、来自 C 的 `Received BusinessStartMessage: {"Start":false`、`Send BusinessStartMessage: {"Start":true`；C 日志有 `Received BusinessStartMessage: {"Start":true`。

## 2 开店等待中退出联机

- 操作：同用例 1，H 等待中执行 `console CMD="mp stop"`。
- 期望：H 数秒内 `wait=False`，`cd` 下降并开始上客，本轮不发 `BusinessStartMessage`。C 恢复后断线，转为单机营业，倒计时为原版时长。

## 3 全员时停

- 操作：营业中 H `timestop MODE=Freeze` → C `timestop MODE=Freeze` → H `timestop MODE=Resume`；再两端 `Freeze` 后由 C `Resume`。
- 期望：只有一端请求时不冻结；两端都请求时两端 `frozen=True ts=0`，`cd` 不变；任一端解除后两端 `frozen=False ts=1`。
- 依据：两端日志 `room time frozen`、`room time resumed`。

## 4 换场景清理

- 操作：用例 3 之后打烊，`result` 推进到次日白天，再共同营业。
- 期望：次日两端 `timeScale=1`、`frozen=False`，房间保持；用例 1 仍成立。

## 5 离桌重放

- 操作：H 侧让客人分别上菜后离开（`serve MODE=send`）、被驱赶（`serve MODE=repell`）、耐心耗尽离开。
- 期望：C 对应桌位的客人离开一次，桌位清空，无重复离开和残留；之后两端同一桌的客人编号一致（入座中的桌除外）。
- 依据：H `Send GuestLeaveMessage`，`Reason` 依次为 `Desk`、`Repel`、`PatientDepleted` → C `Received GuestLeaveMessage` 并重放对应离场。

## 6 面板被离桌打断

- 操作：C `serve MODE=open DESK=D`、`serve MODE=stage DESK=D`；H 侧该客人离开（`serve MODE=repell DESK=D`）；随后 C 给另一桌 `serve MODE=send`。
- 期望：C `panel=-1`，物品不写入已离开客人的订单；后续上菜正常同步，客人离开。
- 依据：H 日志有来自 C 的 `Received ConfirmServeMessage`。

## 7 打烊同步

- 操作：H `close_izakaya`。
- 期望：店内客人离开后两端进入结算（实测 80–125 秒），C 不停在营业场景。
- 依据：C 日志 `Received close command from host`。

## 8 单机面板不暂停

- 操作：单机，`MetaMystia.ConfigManager.NoPanelPause.Value = true`。对一桌有订单的客人 `serve MODE=open`、`serve MODE=stage`，等该客人离开。结束后恢复设置。
- 期望：面板打开期间 `ts=1`，`cd` 下降；客人离开后 `panel=-1`，托盘里有放入的料理和酒水。
