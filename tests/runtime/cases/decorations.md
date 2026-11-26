# 装饰

资源包：在示例资源包中加入 `decorations` 达摩雪雪 11000（`DarumaYukiyuki`）、小鸽子挂坠 11001（`DovePendant`），以及 `buffs` 不倒翁七転八起 11004。下文的 `decoration` 均带 `IDS=11000,11001 BUFF=11004`。

用例 1–9 联机：房主 H、客机 C 读同一份存档，各端 `worktime SECONDS=1200` 后共同营业。H 装备装饰，C 只获得不装备，作为同一存档、同一晚的对照。联机时伙伴被禁用，伙伴相关的用例 10–13 单机进行。用例 1–13 在 0.31.4（#132）通过（运行时实测）；用例 7 在改用 `Overlay` 前因重新触发时剩余太少而误判通过。

截图用调试器的 `GET /api/screenshot`（含 UI，窗口不可见时也可用）。

通用判定：两端日志没有异常堆栈；没有 `跳过注册`、`buff 11004 图标加载失败`。

## 白天

### 1 注册

- 操作：两端 `decoration MODE=info`。
- 期望：`11000:DarumaYukiyuki/Outdoor`、`11001:DovePendant/Outdoor`。
- 依据：日志 `Registered Item ID 11000 (达摩雪雪)`、`Registered Item ID 11001 (小鸽子挂坠)`。

### 2 获得

- 操作：两端 `decoration MODE=give`。
- 期望：两件都是 `own=1 use=0`。

### 3 白天移速

- 操作：H 依次 `MODE=equip IDS=11001`（两次）、`MODE=unequip IDS=11001`、`MODE=equip IDS=11001`，每步后 `MODE=info`。
- 期望：装备时 `spd` 等于 `lv + 0.12`，重复装备不再增加；卸下后 `spd` 等于 `lv`。C 始终 `spd` 等于 `lv`。

### 4 换地图

- 操作：H `shrine MODE=travel`，切图完成后 `decoration MODE=info`。
- 期望：`spd` 仍为 `lv + 0.12`。

## 营业

H 营业前已装备 11000 与 11001。

### 5 小鸽子挂坠

- 操作：开店后两端 `decoration MODE=info`。
- 期望：H 的 `ms`、`unit` 为 C 的 1.2 倍；同一伙伴的效率和移速都是 C 的 1.2 倍（存档没有伙伴时注明未覆盖）。

### 6 达摩雪雪触发

- 操作：两端先 `MODE=info` 取基准；各 `MODE=food`，再各 `MODE=dark`，每步后 `MODE=info`。
- 期望：`food` 后两端都是 `buff=none`。`dark` 后 H 为 `buff=30`（读取延迟可到 28），`free` 比基准多 0.2，`cook` 为基准的 0.8 倍；C 仍为 `buff=none`，`free`、`cook` 不变。
- 说明：`dark` 直接调用原版烹饪完成汇合点 `CallOnResultCompleteCallback`，不经过 QTE、厨具或符卡来源。

### 7 刷新不叠加

- 操作：用例 6 后等约 10 秒，H `MODE=info`；紧接着 H `MODE=dark`。
- 期望：触发前的 `buff` 不小于 10（否则重做，剩余太少时无法区分刷新与叠加）；再次触发后在 28–30，不是触发前读数 + 30，只有一个计时；`free` 仍为基准 + 0.2，`cook` 仍为基准 × 0.8（不是 +0.4、×0.64）。
- 依据：重复触发用原版 `TryOverrideTimedBuff` 的 `Overlay`（取新旧剩余时间较长者）；默认的 `Additive` 会把剩余时间加到新时长上。

### 8 结束

- 操作：最后一次触发后等 40 秒，H `MODE=info`。
- 期望：`buff=none`，`free`、`cook` 回到基准。

## 次日

### 9 结算后保持

- 操作：打烊、结算到次日白天（不存档），两端 `MODE=info`。
- 期望：H `use=1` 且 `spd` 等于 `lv + 0.12`；C `spd` 等于 `lv`。

## 单机

H 单机，存档开启多伙伴且上次带过伙伴，选店时沿用上次的伙伴选择。

### 10 获得提示

- 操作：白天 `MODE=info` 确认 11001 为 `own=0`；`MODE=give` 后立即截图。
- 期望：画面出现原版获得提示：资源包图标、名称「小鸽子挂坠」、`+1`。存档里已有的装饰不再提示。

### 11 伙伴

- 操作：装备两件后营业（选店截图可见所带伙伴），开店后 `MODE=info`，营业到打烊。
- 期望：输出有 `p<伙伴ID>=`，小鸽子挂坠给出的倍率为 1.2/1.2（伙伴自身效果会再叠乘，如宫古芳香吃料理加速）；打烊前日志没有异常。

### 12 真实黑暗料理

- 操作：伙伴带厨师键山雏（原版有概率把料理做成黑暗料理），营业中每隔几秒 `MODE=info`。
- 期望：不调用 `MODE=dark` 也会出现 `buff`，`free`、`cook` 随之变化；任何时刻 `buff` 不超过 30；buff 结束后 `free`、`cook` 回到基准。

### 13 buff 说明

- 操作：`MODE=dark` 后 `MODE=describe`，紧接着截图（说明框几秒后淡出）。
- 期望：输出「不倒翁七転八起 / …剩余N秒」，N 为数字；截图在 buff 栏旁显示说明框，含橙色图标、标题和同样的说明。

## 未覆盖

其他黑暗料理来源（QTE 失败、厨具、符卡）、挑战存档挂载（`RunTimePlayerData.MountAndOverridePlayerRuntimeData`）、剧情移动中更换装备、读档后恢复、DLC5 肉鸽、配置错误时跳过注册。
