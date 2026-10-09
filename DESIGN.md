# ProbablyBagMod — 设计说明

> 游戏：《Probably Stolen (Demo)》· MelonLoader / Il2Cpp mod
> 定位：通过「计算背包运算后的价值」决定产物的总价值。

## 参考程序集路径（重要）
- 当前可用的游戏安装（含 MelonLoader 生成物）：
  `E:\SteamLibrary\steamapps\common\Probably Stolen Demo`
  - `MelonLoader\Il2CppAssemblies\`（123 个 dll，含 Assembly-CSharp.dll）
  - `MelonLoader\net6\`（MelonLoader.dll、0Harmony.dll 等）
- 另注：`D:\Program Files (x86)\Steam\steamapps\common\Probably Stolen Demo` 也曾装过 MelonLoader（123 dll），
  现 csproj 的 `GameDir` 指向 **E 盘**那份。

## 构建
```
cd E:\code\ProbablyBagMod
dotnet build ProbablyBagMod.csproj -c Release
```
产物：`E:\code\build\ProbablyBagMod.dll`

## 当前进度
- [x] mod 工程骨架（csproj + BagMod 入口）
- [x] `ItemBagProperty` 物品背包属性类（纯数据、只读）
- [x] `BagValueSorter` 静态价值计算的分层工具类（按 Priority 分层，层间降序）
- [x] `BagPropertyManager` 管理类框架：静态字典（原生指针作键）+ 惰性构建 + `Remove` 回收入口
- [ ] `BagPropertyManager.BuildProperty`：物品映射表 + 默认值公式（**待实现**）
- [ ] Harmony Prefix 挂到 GameItem 销毁函数（**Hook 点待反编译确定**）
- [ ] 背包价值计算（按 Priority 分层，同层不互相影响）
- [ ] 接入游戏（把属性挂到 GameItem）

## 挂载方案（已定案，含证据）

**存储**：静态 `Dictionary<IntPtr, ItemBagProperty>`，**原生指针作键**。

依据（反编译 Il2CppInterop.Runtime.dll 实证）：
- `Il2CppObjectPool` 自身即用 `ConcurrentDictionary<IntPtr, WeakReference<Il2CppObjectBase>>` 缓存包装对象
  → 「指针作键」是官方同款做法。
- `Il2CppObjectBase.Pointer` 由构造时的 `il2cpp_gchandle_new` 决定，**同一原生对象生命期内恒定**
  → 同一 GameItem 的所有包装，`Pointer` 相同，可作稳定键。

**取值**：惰性构建。`Get` 先查字典 → 未命中则 `BuildProperty`（映射表 → 默认公式）→ 写回。

**回收**：`Remove(GameItem)` 入口已就绪，待用 **Harmony Prefix** 挂到游戏 GameItem 的销毁函数。
Hook 点候选：`GameItem.OnDestroy` 或游戏自定移除方法（**反编译待定**）。

⚠️ **两个必须避开的坑**：
1. `GameItem.Pointer` 在原生物体被回收后**抛 `ObjectCollectedException`**（不是返回 0）；
   取值前必须先用 `WasCollected` 探测。
2. **指针复用**：Unity 销毁对象后地址可能被新对象复用 → 不回收会脏读。这是必须 Hook 销毁的原因。

## ItemBagProperty 字段
| 字段 | 类型 | 含义 |
|---|---|---|
| `Value` | `int` | 物品自身价值 |
| `AdditiveInfluence` | `int` | 对邻近物品的加法影响 `x + a` |
| `MultiplicativeInfluence` | `float` | 对邻近物品的乘法影响 `x * m` |
| `Priority` | `int` | 计算优先级；同优先级不互相影响（防无限递归） |

- 方向、形状等属性由游戏原版提供，本 mod 不重复定义。
- 乘法精度问题由**调用方**处理。

## 关键约定
- 属性类**只承载数据**；注入 / 回收 / 计算都不在属性类里。
- 管理类统一负责「注入到游戏物体」与「回收」；回收走 Harmony Prefix（Hook 点待定）。
- `BagValueSorter` **不引入泛型**，直接以 `IList<GameItem>` 为入参，避免类型参数传错；
  取值统一走 `BagPropertyManager.Get(item)`（静态方法，非委托）。
- 取值语义：`BagPropertyManager.Get(item)` 返回 `null` 表示「没有背包属性」——
  调用方**静默处理，当作空格子**，不抛异常、不补默认值。
