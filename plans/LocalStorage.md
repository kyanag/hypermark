# LocalStorage 开发计划

## 概述

保留 `SqliteStorage` 不变，新增 `LocalStorage.cs` 作为基于文件系统的存储实现，同样实现 `IStorage` 接口。
通过切换 DI 注入的 `IStorage` 实现即可切换存储后端。

---

## 目录结构设计

```
~/.hypermark/
├── config.json                    # 已有配置文件
├── sites/                         # 站点定义，每个站点一个 YAML 文件
│   ├── ${Site.Name}.yaml
│   └── ...
├── bookmarks/                     # 收藏的页面，每个页面一个 JSON 文件
│   ├── ${Category.Name}/          # 分类目录（支持嵌套子目录实现层级）
│   │   ├── ${Title}_${yyyy-MM-dd_HH_mm_ss}.json     # 收藏页面
│   │   └── ${SubCategory}/
│   │       └── ${Title}_${yyyy-MM-dd_HH_mm_ss}.json
│   └── ...                        # 无分类的页面放在根目录
├── domain_cnames.json             # 域名 CNAME 映射关系
├── tags.json                      # 标签元信息
└── categories.json                # 分类元信息（ID/名称/层级关系）
```

---

## 数据格式

### 1. 站点数据：`~/.hypermark/sites/${Site.Name}.yaml`

直接以 YAML 格式序列化 `Site` 对象，与现有 `SqliteStorage` 中使用的 YAML 序列化方式一致。

### 2. 分类信息：`~/.hypermark/categories.json`

> **`Category` 模型：使用 `Name` 作为分类唯一标识，无 `Id` 字段。**

文件系统用目录实现分类层级，但分类的 **名称 / 父子关系** 仍需要一个元数据文件来管理：

```json
[
  { "name": "技术", "parentId": null, "createdAt": "2026-05-29T10:00:00" },
  { "name": "编程", "parentId": "技术", "createdAt": "2026-05-29T10:01:00" }
]
```

- `parentId` 改为父分类的 `name`（字符串），不再使用整数 ID
- 目录路径由 `name` 按层级拼接：`技术/编程/`

### 3. 页面收藏：`~/.hypermark/bookmarks/[分类路径/]${FileName}.json`

每个收藏页面为一个独立 JSON 文件，文件命名规则：

```
FileName = Link.Title + "_" + DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss");
```

使用 `Link.Title` 作为文件名前缀，便于直观识别；时间戳使用当前时间，精确到秒。由于 Windows 文件名不允许 `:` 字符，时间格式中的 `:` 替换为 `_`。

文件内容包含 `Link` + `Page` 的完整信息，文件名示例：`Example Page_2026-06-02 14_30_00.json`：

```json
{
  "url": "https://example.com/page?id=1",
  "name": "example_a94a8fe5ccb19ba61c4c0873d391e987982fbbd3",
  "title": "Example Page",
  "createdAt": "2026-05-29T10:00:00",
  "category": "技术/编程",
  "tags": ["tag1", "tag2"],
  "page": {
    "hyperId": "site://std_url",
    "site": "example",
    "original": "https://example.com/page?id=1",
    "std": "https://example.com/page",
    "args": { "id": "1" },
    "tags": ["parsed-tag"],
    "createAt": "2026-05-29T10:00:00",
    "route": "default"
  }
}
```

### 4. 域名 CNAME 映射：`~/.hypermark/domain_cnames.json`

```json
{
  "m.example.com": "example.com",
  "blog.example.com": "example.com"
}
```

### 5. 标签元信息：`~/.hypermark/tags.json`

```json
[
  { "id": 1, "name": "前端", "createdAt": "2026-05-29T10:00:00" },
  { "id": 2, "name": "后端", "createdAt": "2026-05-29T10:01:00" }
]
```

### 6. Tags Mappings

不再使用独立的映射表。标签直接保存在数据中：
- **Link.Tags**：`Link` JSON 文件中存储 `tags` 数组（标签名称列表）
- **Page.Tags**：`Page` JSON 文件中已有 `tags` 数组

查询时通过扫描所有 JSON 文件并过滤 tags 数组实现 `GetPagesByTag` / `GetLinksByTag`。

---

## 实现步骤

### 步骤 1：创建 LocalStorage.cs 基础骨架

- 实现 `IStorage` 接口
- 构造函数接收 `basePath`（默认 `~/.hypermark`）
- 初始化目录结构（`sites/`、`bookmarks/`）
- 定义统一的 `JsonSerializerOptions` 和 `YamlSerializer`/`YamlDeserializer`
- 实现内存缓存 + 懒加载策略，避免频繁读文件

**关键设计决策：文件锁 / 并发控制**
- 使用 `ReaderWriterLockSlim` 保护共享数据结构
- 写操作获取写锁，读操作获取读锁

### 步骤 2：实现站点数据（Sites）

| 方法 | 实现 |
|------|------|
| `AddSite(Site)` | 序列化 Site 为 YAML，写入 `~/.hypermark/sites/${Site.Name}.yaml` |
| `ExistsSite(Site)` | 检查对应 YAML 文件是否存在 |
| `Sites()` | 读取 `sites/` 目录下所有 `.yaml` 文件，反序列化为 `List<Site>` |
| `GetSite(string)` | 读取并反序列化单个 YAML 文件 |
| `DeleteSite(string)` | 删除对应 YAML 文件，同时删除该站点下所有 Page 文件 |

### 步骤 3：实现分类（Categories）

> **模型变更：`Category` 无 `Id` 字段，使用 `Name` 作为唯一标识，`parentId` 为字符串（父分类 name）**

| 方法 | 签名变更 | 实现 |
|------|----------|------|
| `AddCategory` | `(Category)` → `string`(返回 name) | 写入 `categories.json`，同时创建对应目录 `bookmarks/${path}/` |
| `GetCategories()` | 不变 | 读取 `categories.json` |
| `GetCategory` | `(string name)` → `Category?` | 按 name 从缓存中查找 |
| `MoveCategory` | `(string name, string? newParentName)` | 更新 `categories.json`，重命名对应目录 |
| `DeleteCategory` | `(string name)` | 从 `categories.json` 移除，删除对应目录（递归） |
| `UpdateCategoryName` | `(string oldName, string newName)` | 更新 `categories.json`，重命名对应目录，更新所有子分类的 parentId 引用 |

**目录路径计算：**
```
GetCategoryPath(categoryName):
  获取分类 -> 获取父分类(name) -> ... -> 根
  路径 = string.Join("/", names.Reverse())
```

### 步骤 4：实现页面收藏（Pages + Links）

`AddLink` 和 `AddPage` 合并操作：收藏一个链接时同时写入 Link 信息和解析后的 Page 信息到同一个 JSON 文件。

| 方法 | 实现 |
|------|------|
| `AddLink(Link)` | 根据 `Link.Category` 计算目录路径；文件名使用 `${Title}_${yyyy-MM-dd_HH_mm_ss}`，将 Link + Page 序列化为 JSON 写入 `bookmarks/[path/]${FileName}.json` |
| `AddPage(Page)` | 不独立调用，由 `AddLink` 统一处理。若独立调用，查找已存在的 Link JSON 文件并更新其中的 page 字段 |
| `ExistsPage(Page)` | 扫描 `bookmarks/` 目录，查找 `page.Site == entry.Page.Site && page.HyperId == entry.Page.HyperId` 的 JSON 文件 |
| `GetLinkByUrl(string)` | 遍历 `bookmarks/` 目录所有 JSON 文件，匹配 `entry.Url == url` |
| `GetLinks(string?)` | 遍历 `bookmarks/` 目录（可按分类子目录筛选），读取所有 JSON |
| `GetSitePages(string)` | 扫描 `bookmarks/` 所有 JSON，过滤 `entry.Page.Site == siteName` 的记录 |
| `UpdateLinkCategory(string, string)` | 读取原 JSON -> 修改 category -> 移动到新目录 -> 删除原文件 |
| `DeleteLink(string)` | 遍历所有 JSON 文件匹配 url 字段 -> 删除 |
| `DeleteSite(string)` | 扫描 `bookmarks/` 所有 JSON，过滤 `entry.Page.Site == siteName`，批量删除 |
| `DeletePage(string, string)` | 遍历所有 JSON 匹配 Site+HyperId -> 删除 |
| `IsMarked(Page)` | 遍历所有 JSON 文件，匹配 Site+HyperId（Full）或 Site+Std（Half） |

### 步骤 5：实现域名 CNAME 映射

| 方法 | 实现 |
|------|------|
| `AddDomainCname(domain, cname)` | 读取 `domain_cnames.json` -> 添加/替换 -> 写回 |
| `RemoveDomainCname(domain)` | 读取 -> 移除 key -> 写回 |
| `GetCnameByDomain(domain)` | 读取 -> 查找 key |
| `GetDomainsByCname(cname)` | 读取 -> 过滤 value == cname 的 keys |
| `GetAllDomainCnames()` | 读取全部 key-value 对 |
| `RemoveAllCnames()` | 写入空对象 `{}` |

### 步骤 6：实现标签（Tags）

| 方法 | 实现 |
|------|------|
| `AddTag(Tag)` | 读取 `tags.json` -> 添加 -> 写回 |
| `GetTags()` | 读取 `tags.json` |
| `GetTag(int)` / `GetTagByName(string)` | 从缓存查找 |
| `DeleteTag(int)` | 从 `tags.json` 移除，**同时清理所有 Link/Page JSON 中的对应 tag** |
| `UpdateTagName(int, string)` | 更新 `tags.json`，**同时更新所有 Link/Page JSON 中的 tag 名称** |
| `GetTagsForObject(...)` | 读取目标 JSON 文件，返回 `tags` 数组中的 Tag 对象 |
| `AddTagToObject(...)` | 读取目标 JSON -> 添加 tag name 到 tags 数组 -> 写回 |
| `RemoveTagFromObject(...)` | 读取目标 JSON -> 移除 tag name -> 写回 |
| `GetPagesByTag(int)` | 扫描 `bookmarks/` 所有 JSON，过滤 `page.tags` 包含该 tag 名称的记录 |
| `GetLinksByTag(int)` | 扫描 `bookmarks/` 所有 JSON，过滤 `tags` 包含该 tag 名称的记录 |

**性能优化：**
- 维护内存中的 `tag -> [file paths]` 反向索引
- 写操作时更新索引，读操作时先查索引再按需读取文件

---

## 缓存策略

为避免频繁读取文件，`LocalStorage` 内部维护以下内存缓存：

| 缓存 | 数据结构 | 加载时机 |
|------|----------|----------|
| sites | `Dictionary<string, Site>` | 首次调用 `Sites()` 或 `GetSite()` 时全量加载 |
| categories | `Dictionary<string, Category>` (name -> Category) | 启动时加载 `categories.json` |
| tags | `Dictionary<int, Tag>` | 启动时加载 `tags.json` |
| domainCnames | `Dictionary<string, string>` | 启动时加载 `domain_cnames.json` |
| links | `Dictionary<string, string>` (Title_Timestamp -> filePath) | 启动时扫描 `bookmarks/` 目录，建立文件名到路径的双向索引 |
| tagIndex | `Dictionary<string, HashSet<string>>` (tagName -> filePaths) | 扫描 links 时同步构建 |

**缓存一致性：**
- 每次写操作后同步更新内存缓存
- 提供 `Reload()` 方法用于外部触发重新加载（如手动修改文件后）

---

## Program.cs 集成

在 [Program.cs](HyperMark.Web/Program.cs) 中通过配置切换存储后端：

```csharp
// 使用 LocalStorage
var storage = new ActionLogger(new LocalStorage());

// 使用 SqliteStorage（原有方式）
// var storage = new ActionLogger(new SqliteStorage());

builder.Services.AddSingleton<IStorage>(storage);
```

也可通过配置文件或环境变量切换：
```json
// config.json
{
  "storage": "local"  // 或 "sqlite"
}
```

---

## 需要修改的模型文件

### Category.cs

分类模型，**去掉 `Id` 字段**，使用 `Name` 作为唯一标识，**`ParentId` 类型为 `string?`**（父分类 name）：

```csharp
public class Category
{
    /// <summary>
    /// 分类名称（唯一标识）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 父分类名称，null 表示根分类
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
```

### Link.cs

`Link.Tags` 字段需要从 `JsonIgnore` 改为**可序列化**，因为文件存储时 tags 需要持久化到 JSON 中：

```csharp
// 移除 [JsonIgnore]，tags 会随 Link 一起序列化到 JSON
public List<string> Tags { get; set; } = [];
```

### Page.cs

`Page.Tags` 已经是可序列化的，无需修改。

---

## 需要修改的其他文件

### IStorage.cs

以下方法签名需要变更（`int id` → `string name`）：

```csharp
// 变更前 → 变更后
Category? GetCategory(string name);        // 原 GetCategory(int id)
string AddCategory(Category category);     // 原返回 int id，改为返回 name
bool MoveCategory(string name, string? newParentName);  // 原 (int id, int? newParentId)
bool DeleteCategory(string name);          // 原 DeleteCategory(int id)
bool UpdateCategoryName(string oldName, string newName); // 原 (int id, string name)
```

### Repository.cs

- 同步 `IStorage` 接口变更，所有 `int id` → `string name`
- `MoveCategory` 中的 `IsAncestor` 检查改为按 name 字符串追溯

### SqliteStorage.cs

- SQLite 的 `link_categories` 表需要迁移：`id` 列保留作内部自增，但 `parent_id` 改为存储父分类 name（TEXT）
- 或新建表，做数据迁移
- 所有分类方法的参数改为 `string name`

### ActionLogger.cs

- 同步 `IStorage` 接口变更
- `GetLinks` 签名修复：`int? categoryId` → `string? category`（与 `IStorage` 一致）
- `ReplayLinks` 中查找 `SqliteStorage` 的逻辑需要适配，或改为仅依赖 `IStorage` 接口

### AdminEndpoints.cs

- 所有分类 API 从 `/{id}` 改为 `/{name}` 路径参数
- `CreateCategoryRequest` 的 `ParentId` 改为 `string? ParentName`
- `AdminMoveCategoryRequest` 的 `NewParentId` 改为 `string? NewParentName`
- 创建分类时不再需要设置 `Id`

### DeleteRequests.cs / PluginRequests.cs

- `CreateCategoryRequest.ParentId` → `string? ParentName`
- `AdminMoveCategoryRequest.NewParentId` → `string? NewParentName`

### BackgroundLinkProcessor.cs

- 检查 `AddLink` 调用逻辑，确保 Page 信息在收藏时一并写入

## 测试计划

### 单元测试

1. **站点 CRUD**：添加、查询、删除站点，验证 YAML 文件内容
2. **分类层级**：创建多级分类，验证目录结构；移动分类，验证目录重命名
3. **链接收藏**：添加链接，验证 JSON 文件内容；按分类查询
4. **文件命名**：验证文件名为 `${Title}_${yyyy-MM-dd_HH_mm_ss}` 格式
5. **标签操作**：添加/移除标签，验证 JSON 文件更新；按标签查询
6. **域名 CNAME**：添加/删除映射，验证 JSON 文件
7. **收录状态**：`IsMarked` 的 Full/Half/No 三种状态（利用文件名前缀快速定位）
8. **并发安全**：多线程同时读写同一文件

### 集成测试

1. 切换 `IStorage` 实现为 `LocalStorage`，运行现有 API 测试
2. 验证 API 返回结果与 SQLite 后端一致

---

## 风险点和注意事项

1. **大量文件性能**：当收藏页面数量非常大（>10万）时，扫描目录和匹配 tags 可能较慢。后续可考虑引入文件索引或 SQLite 作为缓存层。
2. **文件原子性**：写 JSON/YAML 时应采用「写临时文件 + 原子替换」策略，避免写入中断导致文件损坏。
3. **分类重命名级联更新**：分类名称变更后，需要更新：(1) `categories.json` 中所有 `parentId` 引用该名称的子分类 (2) 对应目录重命名 (3) 所有 Link JSON 中的 `category` 字段。操作可能耗时。
4. **标签名称变更的级联更新**：修改标签名称时需要更新所有引用该标签的 JSON 文件，操作可能耗时。可改为标签 ID 引用而非名称引用，但这会增加 JSON 文件大小。
5. **ActionLogger 兼容性**：`ActionLogger` 内部有 `FindSqliteStorage()` 方法强依赖 `SqliteStorage` 类型，需要调整为支持任意 `IStorage`。
6. **文件名冲突**：同一分类下相同标题且在同一秒内收藏的页面会覆盖（文件名相同）。由于时间戳精确到秒，正常情况下冲突概率极低。可通过增加毫秒精度或添加唯一后缀解决。
