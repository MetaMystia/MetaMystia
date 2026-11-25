# 符卡同步

准备同 [guest-sync](guest-sync.md)。稀客由 H `spell MODE=spawn NAME=<标签>` 生成，入座后用 `spell MODE=red/black/leave NAME=<编号>` 直接触发；T 为其编号，标签取 [glossary](../../../docs/reference/glossary.md) 的持有者标识。店里先用 `spawn MODE=normal COUNT=3` 坐上普通客人。

通用判定：

- 每个用例前后各端取一次 `spell MODE=state NAME=0` 与 `guests`。执行结束后各端 `tables`、`cookers`、`shield` 相同，`queue=0`，`runs=[]`。
- H 每次触发依次 `Send SpellBeginMessage`、`Send SpellPlanMessage`；C 有对应的 `Received`，C 没有 `Send`。
- 各端日志没有异常堆栈，没有 `Exception in IL2CPP-to-Managed trampoline`。

## 桌子

### 1 比那名居天子 黑卡

- 操作：H `spell MODE=black NAME=T`，T 的标签 `Tenshi`，入座后触发。
- 期望：两端同一批桌子被砸，`tables` 相同；砸中的客人两端各离开一次；锁结束后两端 `tables=[]`，排队客人坐到同一桌。重复三次。
- 依据：`SpellPlanMessage` 的 `Choices` 与两端 `tables` 一致。

### 2 红美铃 黑卡

- 操作：同 1，标签 `Meirin`。连放两次。
- 期望：两端砸同一张桌，第二次不选已锁的桌；结束后两端桌子图块恢复。

### 3 风见幽香 黑卡

- 操作：同 1，标签 `KazamiYuuka`。
- 期望：两端幽香走到同一位置，锁同一列桌，随后离开；两端 `runs=[]`。

## 厨具

### 4 琪露诺、爱丽丝、饕餮尤魔 黑卡

- 操作：各放一次，标签 `Cirno`、`Alice`、`Totetsu`；放之前 C 在一个厨具上烹饪。
- 期望：两端锁同样的厨具；被锁的锅两端都清空，未锁的锅两端都继续。

### 5 灵乌路空 黑卡

- 操作：放一次后 C 烹饪并让 QTE 不满分；之后 H 再让 QTE 不满分。
- 期望：第一次两端炸毁同一厨具；第二次不再触发。
- 依据：C `Send QTENotFullScoreMessage`，H `Received` 后 `Send`，C `Received`。

## 顺序与时机

### 6 客机面板打开

- 操作：C 打开上菜面板，H 让天子放黑卡；10 秒后 C 关闭面板。
- 期望：C 关闭面板后补演，选桌与 H 相同；锁与 H 同时结束（差不超过 2 秒）。

### 7 连放

- 操作：H 连续触发同一稀客红卡、黑卡、红卡。
- 期望：两端按同样顺序演完。

### 8 施法者离场

- 操作：雾雨魔理沙放黑卡（标签 `Marisa`）；另一次在宣言中 C 赶走施法者。
- 期望：两端魔理沙各飞走一次；之后再放其他卡两端仍执行。

### 9 古明地觉

- 操作：标签 `Satori`，红黑各放三次。
- 期望：C 播放觉的宣言和代放卡的宣言；代放锁桌、锁厨具的卡时两端一致。
- 依据：H 每次两条 `Send SpellBeginMessage`，第二条的 `SpellOwner` 不为空。

### 10 护盾

- 操作：博丽灵梦放红卡（标签 `Reimu`）后，其他稀客放黑卡。
- 期望：两端 `shield` 同时加一、减一，黑卡两端都不执行。
- 依据：H `Send SpellDefendedMessage`。

## 生成与营业

### 11 二岩猯藏、露易兹、绵月丰姬 红卡

- 操作：标签 `HutatsuiwaMamizou`、`Luize`、`Toyohime` 各放一次；丰姬由 H 选图。
- 期望：生成的顾客两端编号一致；C 不弹选图面板。

### 12 延长营业

- 操作：蕾米莉亚（标签 `DLC4_Remilia`）放红卡后 H `close_izakaya`，期间店里有蕾米莉亚。
- 期望：C 打烊后 `hold` 大于 0，H 继续来客时 C 也来客；H 关店后 C 随即关店，两端进入结算。
- 依据：H 两条 `Send IzakayaCloseMessage`，第二条 `"Final":true`。

### 13 断线

- 操作：黑卡宣言中 C 断开；另一次打烊后、H 仍在延长营业时 C 断开。
- 期望：C 单机继续，符卡演完，之后的符卡仍能执行；打烊后的 C 在顾客离开后关店。

## 全部卡

### 14 逐卡

- 操作：对 60 张卡各放红卡、黑卡一次（`Tewi` 另放离场卡），每次等两端 `queue=0`、`runs=[]`。
- 期望：两端无异常堆栈，`tables`、`cookers` 始终一致。
