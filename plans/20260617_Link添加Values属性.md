# Link 添加 Values 属性计划

> 目标：为 Link 模型添加 `Values` 属性，用于存储链接的扩展元数据（非收藏夹核心信息），供前端应用灵活使用。

## 背景

当前 Link 模型只包含收藏所需的核心字段（Url、Title、Category、Tags、Page）。然而在实际使用中，链接往往附带来源站点的上下文信息，例如：

- 论坛帖子：所属板块、发布时间、帖子摘要、回复数
- GitHub Issue：Issue 编号、状态、标签、负责人
- 商品页面：价格、评分、库存状态、规格参数

这些信息不属于 HyperMark 的核心收藏逻辑，但在前端展示、搜索、筛选等场景中可能有重要价值。需要一个灵活的扩展字段来存储这类数据。

## 设计决策

### 属性命名：`Values`

`Values` 语义明确，表示"扩展值集合"，与 Site 的 `Vars` 形成呼应但不混淆。

### 类型选择：`Dictionary<string, object>?`

使用 `Dictionary<string, object>?`，与 `Site.Vars` 的 `Dictionary<string, string>` 形成自然的升级关系。

理由：
1. **结构自由** — value 为 `object`，可以是 string、int、bool、嵌套 Dictionary、List 等，支持任意 JSON 结构
2. **与 Site.Vars 呼应** — 同为字典类型，`Vars` 是扁平的 string 值，`Values` 是更灵活的 object 值，语义递进
3. **操作直观** — C# 中对 Dictionary 的增删改查是基本操作，比 `JsonElement` 更易用
4. **AOT 兼容** — System.Text.Json 反序列化 `object` 时默认映射为 `JsonElement`，序列化无问题

### 与 Site.Vars 的区别

| | Site.Vars | Link.Values |
|---|---|---|
| 类型 | `Dictionary<string, string>` | `Dictionary<string, object>?` |
| 用途 | 站点级配置（模板变量等） | 链接级扩展数据 |
| 值类型 | 仅字符串 | 任意 JSON 值 |

`Site.Vars` 用 `Dictionary<string, string>` 是合理的，因为站点配置本质上是扁平的模板变量。`Link.Values` 用 `Dictionary<string, object>?`，因为链接的扩展数据来源多样、结构不定。

### 默认值：`null`

`Values` 默认 `null`。大多数链接不需要扩展信息，`null` 避免序列化空对象，保持 JSON 文件简洁。

## 变更范围

| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `HyperMark/Models/Link.cs` | **修改** | 添加 `Values` 属性 |
| `HyperMark/ApiRequests.cs` | **修改** | `CreateLinkRequest`、`UpdateLinkRequest`、`MarkShortcutRequest` 添加 `Values` 字段 |
| `HyperMark/ApiResponses.cs` | **无需修改** | 响应直接返回 Link 对象，自动包含 Values |
| `HyperMark/HyperMarkJsonContext.cs` | **无需修改** | `Dictionary<string, object>` 序列化已支持 |
| `HyperMark/Storage/IStorage.cs` | **无需修改** | Link 对象整体传入传出，接口不变 |
| `HyperMark/Storage/LocalStorage.cs` | **无需修改** | JSON 序列化自动处理新属性 |
| `HyperMark/Storage/CacheStorage.cs` | **无需修改** | 缓存层透传 Link 对象 |
| `HyperMark/Storage/Repository.cs` | **无需修改** | 业务逻辑不感知 Values |
| `HyperMark.Web/Api/LinksEndpoints.cs` | **修改** | 创建/更新链接时传递 Values |
| `HyperMark.Web/Api/MarkEndpoints.cs` | **修改** | Mark 请求支持可选 Values |
| `HyperMark.Web/wwwroot/index.html` | **修改** | 管理面板展示/编辑 Values（JSON 编辑器） |

## 执行步骤

### 步骤 1：修改 Link 模型

**文件**：`HyperMark/Models/Link.cs`

添加属性：

```csharp
/// <summary>
/// 扩展数据，存储链接的非核心元数据
/// </summary>
public Dictionary<string, object>? Values { get; set; }
```

### 步骤 2：修改 API 请求 DTO

**文件**：`HyperMark/ApiRequests.cs`

`MarkShortcutRequest`、`CreateLinkRequest`、`UpdateLinkRequest` 各添加字段：

```csharp
public Dictionary<string, object>? Values { get; set; }
```

### 步骤 3：修改 API 端点

**文件**：`HyperMark.Web/Api/LinksEndpoints.cs`

创建链接时传递 Values：

```csharp
var link = new Link
{
    // ... 现有字段
    Values = body.Values,
};
```

更新链接时处理 Values：

```csharp
if (body.Values != null)
{
    link.Values = body.Values;
    repo.UpdateLinkValues(url, body.Values);
}
```

**文件**：`HyperMark.Web/Api/MarkEndpoints.cs`

Mark 操作传递 Values：

```csharp
var link = new Link
{
    // ... 现有字段
    Values = body.Values,
};
```

### 步骤 4：存储层适配

需要在存储层新增 `UpdateLinkValues` 方法，整体替换 Values（Dictionary 不支持部分更新）。

**文件**：`HyperMark/Storage/IStorage.cs`

```csharp
public bool UpdateLinkValues(string url, Dictionary<string, object>? values);
```

**文件**：`HyperMark/Storage/LocalStorage.cs`

```csharp
public bool UpdateLinkValues(string url, Dictionary<string, object>? values)
{
    var (file, entry) = FindLinkByUrl(url);
    if (entry == null || file == null) return false;

    entry.Values = values;
    AtomicWrite(file, JsonSerializer.Serialize(entry, HyperMarkJsonContext.Instance.JsonBookmarkEntry));
    return true;
}
```

同步更新 `JsonBookmarkEntry`（添加 `Values` 属性）和 `ToLink` 方法。

**文件**：`HyperMark/Storage/CacheStorage.cs`

```csharp
public bool UpdateLinkValues(string url, Dictionary<string, object>? values)
{
    if (!_inner.UpdateLinkValues(url, values)) return false;
    lock (_lock)
    {
        if (!_linksByUrl.TryGetValue(url, out var link)) return false;
        link.Values = values;
        _linksByUrl[url] = link;
    }
    return true;
}
```

**文件**：`HyperMark/Storage/ActionLogger.cs`

```csharp
public bool UpdateLinkValues(string url, Dictionary<string, object>? values) => _inner.UpdateLinkValues(url, values);
```

**文件**：`HyperMark/Storage/Repository.cs`

```csharp
public bool UpdateLinkValues(string url, Dictionary<string, object>? values) => _storage.UpdateLinkValues(url, values);
```

### 步骤 5：管理面板适配

**文件**：`HyperMark.Web/wwwroot/index.html`

- 链接列表新增"📝"按钮打开 Values 编辑弹窗
- 编辑弹窗：JSON 文本区域（textarea），支持直接编辑原始 JSON
- 展示时格式化输出（JSON.stringify with indent）
- 保存时 JSON.parse 验证格式

## 数据示例

### 论坛帖子（嵌套结构）

```json
{
  "url": "https://forum.example.com/thread/12345",
  "title": "如何优化 C# 性能",
  "values": {
    "board": "编程技术",
    "author": "user_abc",
    "reply_count": 42,
    "created_at": "2026-06-15T10:30:00Z",
    "top_replies": [
      { "author": "user_x", "likes": 15 },
      { "author": "user_y", "likes": 8 }
    ]
  }
}
```

### GitHub Issue（混合类型）

```json
{
  "url": "https://github.com/org/repo/issues/789",
  "title": "修复登录超时问题",
  "values": {
    "repo": "org/repo",
    "issue_number": 789,
    "state": "open",
    "assignee": "dev_user",
    "labels": ["bug", "urgent", "backend"],
    "milestone": "v2.1.0"
  }
}
```

### 简单键值对

```json
{
  "url": "https://example.com/article/42",
  "title": "某篇文章",
  "values": {
    "category": "技术",
    "tags_count": 5,
    "premium": false
  }
}
```

### 空 Values（大多数链接）

```json
{
  "url": "https://docs.microsoft.com/dotnet",
  "title": ".NET 文档",
  "values": null
}
```

## 向后兼容性

- `Values` 属性为 `Dictionary<string, object>?`，默认 `null`，现有数据无需迁移
- 现有 API 行为不变：创建/更新时不传 Values 等同于 Values=null
- JSON 序列化兼容：新增属性在旧版本反序列化时被忽略
- `null` 值不序列化，保持 JSON 文件简洁

## 注意事项

1. **Values 不参与业务逻辑** — 存储层、匹配层、解析层均不感知 Values，仅作为数据透传
2. **前端负责格式** — 前端提交合法 JSON，后端不做结构校验
3. **无大小限制** — 但建议单个 Link 的 Values 控制在合理范围内（< 10KB）
4. **整体替换** — 更新 Values 时整体替换，不支持部分更新（merge 由前端或业务层处理）
5. **AOT 反序列化** — System.Text.Json 反序列化 `object` 时默认映射为 `JsonElement`，这是预期行为
