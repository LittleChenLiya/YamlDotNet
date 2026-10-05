# YamlNode 树投影（Tree Projection）

下游消费者只接受**无 anchor / 无 alias** 的 YAML 时，可以使用
`YamlDotNet.RepresentationModel.YamlTreeProjection` 把一个可能含有共享节点
（由锚点/别名引入）的 `YamlNode` 图显式投影成一棵自包含的树：每个共享节点
都会被复制成相互独立的节点，同时记录每个副本的来源（provenance）。

## 两阶段设计

1. **分析阶段（不分配结果）**：在源节点图上做一次遍历——
   - 计算完全展开后的节点数量（共享节点按出现次数计）；
   - 检测**真实的环**：只有"节点是其自身祖先"（递归栈上再次遇到）才算环；
     仅仅被多个父节点引用的共享节点**不会**被误判为环（DAG 展开是允许的）。
   - 计数在 `maxNodes + 1` 处饱和，因此超大展开不会溢出，也不会做无用功。
2. **复制阶段（仅在预算足够且无环时执行）**：逐节点复制，
   保留 `Tag` 与各类 `Style`（标量/序列/映射），**丢弃 Anchor**；
   源图与其锚点保持完全不变。

预算不足时不分配任何结果对象；`TryProject` 返回
`YamlTreeProjectionStatus.BudgetExceeded` 且 `result` 为 `null`。
环以 `YamlTreeProjectionStatus.CycleDetected` 报告。
未解析的别名节点与未知扩展节点类型会被拒绝（抛出 `NotSupportedException`）。

## 输出保证

- 投影树中不存在共享引用：每个节点都是新实例；
- 投影树不含任何 anchor，也不含 `YamlAliasNode`；
- `Tag` 与标量/序列/映射的 `Style` 原样保留；
- 源图（包括其锚点）不被修改。

## 来源（Provenance）

`YamlTreeProjectionResult.Provenance` 把投影树中的每个节点（按引用比较）
映射回它复制自的源节点。**来自同一共享源节点的多个副本，其来源都指回同一个
源节点实例**。也可以用 `GetOriginal(copy)` 查询单个副本的来源。

## 示例

```csharp
// first: &s [item]
// second: *s
var shared = new YamlSequenceNode(new YamlScalarNode("item"));
var root = new YamlMappingNode
{
    { new YamlScalarNode("first"), shared },
    { new YamlScalarNode("second"), shared },
};

// 展开后共 7 个节点：1 映射 + 2 键 + 2 序列 + 2 标量项
var status = YamlTreeProjection.TryProject(root, maxNodes: 7, out var result);
// status == Success；result.NodeCount == 7
// maxNodes: 6 时返回 BudgetExceeded，result 为 null，源图仍共享同一序列

var copies = ((YamlMappingNode)result!.Root).Children.Values.ToList();
// copies[0] 与 copies[1] 是两个独立节点，
// 但 result.GetOriginal(copies[0]) 与 GetOriginal(copies[1])
// 指回同一个 shared 实例。
```

自引用（如 `sequence.Add(sequence)`）会被识别为真实环：
`TryProject` 返回 `CycleDetected`，`Project` 抛出
`YamlTreeProjectionException`（其 `Status` 为 `CycleDetected`）。

## API 一览

| 成员 | 说明 |
| --- | --- |
| `YamlTreeProjection.Project(root, maxNodes)` | 成功返回结果；预算不足或存在环时抛 `YamlTreeProjectionException` |
| `YamlTreeProjection.TryProject(root, maxNodes, out result)` | 返回 `YamlTreeProjectionStatus`，失败时不分配结果 |
| `YamlTreeProjectionResult.Root` | 投影树的根（全新实例） |
| `YamlTreeProjectionResult.SourceRoot` | 源图的根（未被修改） |
| `YamlTreeProjectionResult.NodeCount` | 展开后的确切节点数 |
| `YamlTreeProjectionResult.Provenance` | 副本 → 源节点 的只读映射（引用比较） |
| `YamlTreeProjectionResult.GetOriginal(copy)` | 查询单个副本的来源；不属于本投影时返回 `null` |
