# 装饰

房主 H、客机 C 读同一份存档，加载同一个资源包：在示例资源包中加入 `decorations` 达摩雪雪 11000（`DarumaYukiyuki`）、小鸽子挂坠 11001（`DovePendant`），以及 `buffs` 不倒翁七転八起 11004。各端 `worktime SECONDS=1200` 后共同营业。H 装备装饰，C 只获得不装备，作为同一存档、同一晚的对照。下文的 `decoration` 均带 `IDS=11000,11001 BUFF=11004`。

通用判定：两端日志没有异常堆栈；没有 `跳过注册`、`buff 11004 图标加载失败`。

## 白天

### 1 注册

- 操作：两端 `decoration MODE=info`。
- 期望：`11000:DarumaYukiyuki/Outdoor`、`11001:DovePendant/Outdoor`。
- 依据：日志 `Registered Item ID 11000 (达摩雪雪)`、`Registered Item ID 11001 (小鸽子挂坠)`。

### 2 获得

- 操作：两端 `decoration MODE=give`。
- 期望：两件都是 `own=1 use=0`；原版获得装饰提示显示占位图标和名称。

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
- 期望：`food` 后两端都是 `buff=none`。`dark` 后 H 为 `buff=30`（读取延迟可到 28），`free` 比基准多 0.2，`cook` 为基准的 0.8 倍；C 仍为 `buff=none`，`free`、`cook` 不变。H 的 buff 栏显示占位图标，说明里的剩余秒数已替换。
- 说明：`dark` 直接调用原版烹饪完成汇合点 `CallOnResultCompleteCallback`，不经过 QTE、厨具或符卡来源。

### 7 刷新不叠加

- 操作：用例 6 后等约 10 秒，H `MODE=info`；再 H `MODE=dark`、`MODE=info`。
- 期望：等待后 `buff` 降到 17–22；再次触发后回到 28–30，只有一个计时；`free` 仍为基准 + 0.2，`cook` 仍为基准 × 0.8（不是 +0.4、×0.64）。

### 8 结束

- 操作：最后一次触发后等 40 秒，H `MODE=info`。
- 期望：`buff=none`，`free`、`cook` 回到基准。

## 次日

### 9 结算后保持

- 操作：打烊、结算到次日白天（不存档），两端 `MODE=info`。
- 期望：H `use=1` 且 `spd` 等于 `lv + 0.12`；C `spd` 等于 `lv`。

## 未覆盖

挑战存档挂载（`RunTimePlayerData.MountAndOverridePlayerRuntimeData`）、剧情移动中更换装备、读档后恢复、DLC5 肉鸽、配置错误时跳过注册。
