# Category 添加 Domain 属性计划

> 目标：为 Category 模型添加 `Domain` 属性，支持域名专属分类，使分类系统兼具全局收藏夹和站点外挂收藏夹双重角色。

## 背景

当前 Category 是全局扁平结构，所有分类共享同一个命名空间（`Name` 为唯一标识）。随着站点管理精细化的需求，需要支持：
- **全局分类**（`Domain = null`）：跨站点的通用分类，如"待读"、"参考"
- **域名专属分类**（`Domain = "github.com"`）：绑定到特定域名的分类

选择 `Domain` 而非 `Site` 的原因：存在未配置 Site 但仍需对域名进行分类管理的场景。`Domain` 比 `Site` 更底层、更灵活。

## 核心设计决策

### 唯一性约束

**保持 `Name` 全局唯一**，`Domain` 作为归属标记而非唯一键的一部分。

理由：
1. `ParentId` 通过 `Name` 字符串引用父分类，若允许同名分类存在则引用歧义
2. 现有存储结构（单文件 categories.json、目录路径）均以 `Name` 为锚点
3. 避免引入复合主键的复杂度（存储接口需全面改造）

### 域名等价解析

`Category.Domain` 存储的是一个具体域名（如 `"github.com"`）。查询时需要考虑域名等价关系：

**等价来源 1 — Site.Domains**：同一 Site 的所有 Domains 互为等价域名。例如 Site "github" 的 Domains 为 `["github.com", "www.github.com"]`，则 `github.com` ≡ `www.github.com`。

**等价来源 2 — CNAME 映射**：`domain_cnames.json` 中的别名域名映射到站点名，再通过该站点的 Domains 扩展。例如 `"gh.com"` CNAME → `"github"`（站点名），则 `gh.com` 等价于 `github` 站点下所有 Domains。

**解析算法**（`ResolveDomain(string domain) → HashSet<string>`）：

```
输入: domain (如 "www.github.com")
输出: 所有等价域名集合

1. 初始化 result = { domain }
2. 查找包含 domain 的所有 Site，将这些 Site 的全部 Domains 加入 result
3. 查找 domain 的 CNAME 映射 → 得到站点名，将该站点的全部 Domains 加入 result
4. 对 result 中每个新发现的域名，递归执行步骤 2-3（直到无新发现）
5. 返回 result
```

**使用场景**：查询某域名下的分类时，先解析出所有等价域名，再匹配 `Category.Domain ∈ 等价集合`。

### 文件系统目录结构

域名专属分类的目录放在 `bookmarks/_domains/{domain}/` 下，与全局分类隔离：

```
~/.hypermark/bookmarks/
├── 待读/                            # 全局分类 (Domain=null)
├── 参考/                            # 全局分类 (Domain=null)
└── _domains/
    └── github.com/                  # Domain="github.com" 的分类
        ├── stars/
        └── repos/
```

目录路径使用 `Category.Domain` 的原始值（不做等价展开），保证物理路径稳定。

## 变更范围

| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `HyperMark/Models/Category.cs` | **修改** | 添加 `Domain` 属性 |
| `HyperMark/ApiRequests.cs` | **修改** | `CreateCategoryReq` 添加 `Domain` 字段 |
| `HyperMark/ApiResponses.cs` | **修改** | `CategoryCreatedResponse` 添加 `Domain` 字段 |
| `HyperMark/Storage/IStorage.cs` | **修改** | 接口新增 `GetCategoriesByDomain` 方法 |
| `HyperMark/Storage/LocalStorage.cs` | **修改** | 存储实现适配 Domain + 域名解析 |
| `HyperMark/Storage/CacheStorage.cs` | **修改** | 缓存层适配 Domain |
| `HyperMark/Storage/Repository.cs` | **修改** | 业务逻辑适配 Domain + 域名解析方法 |
| `HyperMark.Web/Api/CategoriesEndpoints.cs` | **修改** | API 端点适配 Domain |
| `HyperMark/HyperMarkJsonContext.cs` | **无需修改** | Category 已注册，新增属性自动序列化 |
| `HyperMark.Web/wwwroot/index.html` | **修改** | 管理面板支持按域名筛选分类 |

## 执行步骤

### 步骤 1：修改 Category 模型

**文件**：`HyperMark/Models/Category.cs`

添加属性：

```csharp
/// <summary>
/// 所属域名，null 表示全局分类
/// </summary>
public string? Domain { get; set; }
```

### 步骤 2：修改 API 请求/响应 DTO

**文件**：`HyperMark/ApiRequests.cs`

`CreateCategoryReq` 添加字段：

```csharp
public class CreateCategoryReq
{
    public string Name { get; set; } = string.Empty;
    public string? ParentName { get; set; }
    public string? Domain { get; set; }  // 新增
}
```

**文件**：`HyperMark/ApiResponses.cs`

`CategoryCreatedResponse` 添加字段：

```csharp
public record CategoryCreatedResponse(string Name, string? ParentId, string? Domain);  // 新增 Domain
```

### 步骤 3：新增域名解析方法到 Repository

**文件**：`HyperMark/Storage/Repository.cs`

在 `#region 域名 CNAME 映射操作` 区域新增核心解析方法：

```csharp
/// <summary>
/// 解析域名的所有等价域名（考虑 Site.Domains 和 CNAME 映射）
/// </summary>
public HashSet<string> ResolveDomain(string domain)
{
    var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { domain };
    var queue = new Queue<string>();
    queue.Enqueue(domain);

    while (queue.Count > 0)
    {
        var current = queue.Dequeue();

        // 1. 查找包含 current 的所有 Site，将其 Domains 加入结果
        foreach (var site in _storage.GetSites())
        {
            if (site.Domains.Contains(current, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var d in site.Domains)
                {
                    if (result.Add(d)) queue.Enqueue(d);
                }
            }
        }

        // 2. 查找 current 的 CNAME → 站点名，再展开该站点的 Domains
        var cname = _storage.GetCnameByDomain(current);
        if (!string.IsNullOrEmpty(cname))
        {
            // CNAME 值本身可能是一个域名，先加入
            if (result.Add(cname)) queue.Enqueue(cname);

            // CNAME 值也可能匹配某个 Site 的 Domains
            foreach (var site in _storage.GetSites())
            {
                if (site.Domains.Contains(cname, StringComparer.OrdinalIgnoreCase))
                {
                    foreach (var d in site.Domains)
                    {
                        if (result.Add(d)) queue.Enqueue(d);
                    }
                }
            }
        }
    }

    return result;
}

/// <summary>
/// 获取指定域名下的分类（自动考虑域名等价关系）
/// </summary>
public List<Category> GetCategoriesByDomain(string domain)
{
    var domains = ResolveDomain(domain);
    return _storage.GetCategories()
        .Where(c => c.Domain != null && domains.Contains(c.Domain))
        .ToList();
}
```

### 步骤 4：修改存储接口

**文件**：`HyperMark/Storage/IStorage.cs`

无需修改 IStorage 接口。域名解析逻辑在 Repository 层实现，IStorage 的 `GetCategories()` 返回全部分类，由 Repository 层筛选。

现有接口方法签名不变：
- `GetCategories()` — 返回全部分类（Repository 层按需筛选）
- `GetCategory(string name)` — Name 全局唯一，无需改动
- `AddCategory(Category category)` — Category 对象已携带 Domain

### 步骤 5：修改 LocalStorage 实现

**文件**：`HyperMark/Storage/LocalStorage.cs`

#### 5.1 `AddCategory` — 无需改动

`Category` 对象已携带 `Domain`，`BuildCategoryDirPath` 会自动适配。

#### 5.2 适配 `BuildCategoryDirPath`

需要根据分类的 `Domain` 属性决定目录根路径：

```csharp
internal static string BuildCategoryDirPath(string category, List<Category> cats)
{
    var names = new List<string>();
    var current = category;
    var visited = new HashSet<string>();
    string? domain = null;

    while (!string.IsNullOrEmpty(current))
    {
        if (visited.Contains(current)) break;
        visited.Add(current);
        names.Add(current);
        var cat = cats.FirstOrDefault(c => c.Name == current);
        domain ??= cat?.Domain;  // 记录域名（取最深层分类的 Domain）
        current = cat?.ParentId;
    }
    names.Reverse();

    var baseDir = GetBookmarksDir(cats);
    if (!string.IsNullOrEmpty(domain))
        baseDir = Path.Combine(baseDir, "_domains", SanitizeDirName(domain));

    return Path.Combine(new[] { baseDir }.Concat(names).ToArray());
}
```

需要新增目录名安全处理（域名中的 `.` 和 `:` 保留，但处理非法字符）：

```csharp
private static string SanitizeDirName(string domain)
{
    // 域名本身通常是合法目录名，仅替换极端情况
    return domain.Replace('/', '_').Replace('\\', '_');
}
```

### 步骤 6：修改 CacheStorage 实现

**文件**：`HyperMark/Storage/CacheStorage.cs`

**无需新增方法**。域名解析在 Repository 层完成，CacheStorage 只需提供 `GetCategories()` 全量数据。现有缓存逻辑无需改动。

### 步骤 7：修改 API 端点

**文件**：`HyperMark.Web/Api/CategoriesEndpoints.cs`

#### 7.1 GET /api/categories — 支持 domain 查询参数

```csharp
group.MapGet("", (string? domain, Repository repo) =>
{
    if (domain != null)
        return Results.Ok(repo.GetCategoriesByDomain(domain));
    return Results.Ok(repo.GetCategories());
});
```

`domain` 参数来自 query string，如 `/api/categories?domain=github.com`。不传则返回全部。

域名等价解析自动生效：请求 `/api/categories?domain=www.github.com` 会返回 `Domain="github.com"` 的分类（如果两者通过 Site.Domains 或 CNAME 关联）。

#### 7.2 POST /api/categories — 传递 Domain

```csharp
group.MapPost("", async (HttpRequest req, Repository repo) =>
{
    var body = await req.ReadFromJsonAsync<CreateCategoryReq>();
    if (body is null || string.IsNullOrEmpty(body.Name))
        return Results.BadRequest(new ErrorResponse("缺少 name 字段"));

    var existing = repo.GetCategory(body.Name);
    if (existing != null) return Results.Conflict(new ErrorResponse("分类已存在"));

    var category = new Category
    {
        Name = body.Name,
        ParentId = body.ParentName,
        Domain = body.Domain,  // 新增
        CreatedAt = DateTime.Now
    };
    var resultName = repo.AddCategory(category);
    return Results.Created(
        $"/api/categories/{Uri.EscapeDataString(resultName)}",
        new CategoryCreatedResponse(category.Name, category.ParentId, category.Domain)  // 新增 Domain
    );
});
```

### 步骤 8：管理面板适配

**文件**：`HyperMark.Web/wwwroot/index.html`

在分类管理区域添加域名筛选：
- 输入框或下拉框（下拉选项来自所有已知域名：Site.Domains + CNAME domains）
- 切换时重新请求 `/api/categories?domain=xxx`
- 创建分类时增加"所属域名"输入框（可选）
- 显示分类列表时，域名列展示 `Category.Domain` 值

## 域名等价解析示例

假设有以下配置：

**Sites:**
```json
{ "Name": "github", "Domains": ["github.com", "www.github.com"] }
```

**domain_cnames.json:**
```json
{ "gh.com": "github" }
```

**Categories:**
```json
{ "Name": "stars", "Domain": "github.com", "ParentId": null }
```

查询行为：
| 请求 | 等价域名集合 | 匹配结果 |
|------|-------------|----------|
| `?domain=github.com` | {github.com, www.github.com} | ✅ "stars" |
| `?domain=www.github.com` | {github.com, www.github.com} | ✅ "stars" |
| `?domain=gh.com` | {gh.com, github.com, www.github.com} | ✅ "stars" |
| `?domain=example.com` | {example.com} | ❌ 无匹配 |

## 向后兼容性

- `Domain` 属性为 `string?`，默认 `null`，现有数据无需迁移
- 现有 API 行为不变：`GET /api/categories`（不带 domain 参数）仍返回全部分类
- JSON 序列化兼容：新增属性在旧版本反序列化时被忽略

## 注意事项

1. **Name 全局唯一性不变** — 域名专属分类的 Name 仍不能与全局分类或其他域名分类重名
2. **目录路径使用原始 Domain 值** — 不做等价展开，保证物理路径稳定
3. **域名解析在 Repository 层** — 存储层不感知等价逻辑，保持职责单一
4. **Link 关联不受影响** — Link 的 `Category` 字段仍通过 Name 关联，无需改动
5. **性能考虑** — `ResolveDomain` 遍历所有 Site，可在 CacheStorage 层构建域名→站点索引优化（后续按需）
