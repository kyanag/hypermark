# CLAUDE.md
使用中文

## 项目概述

HyperMark 是一个增强版网页收藏工具，支持 Web 服务和 Windows 桌面两种运行模式：
- **站点管理**：以 JSON 格式管理站点元信息，以 name 作为唯一标识
- **路由管理**：解析 URL 路由信息进行标准化管理
- **链接管理**：根据域名及站点 homepage 匹配站点，通过路由解析 URL 得到标准化信息
- **分类管理**：支持多级树形分类
- **标签管理**：支持标签搜索、自动补全

## 技术栈

- .NET 10.0
- C# ASP.NET Core Minimal API
- Avalonia UI 11.x（桌面端，支持 AOT）
- 文件系统存储（无数据库依赖）

## 解决方案结构

```
HyperMark.slnx
├── HyperMark/              # 核心库（net10.0 类库）
├── HyperMark.Web/          # Web 服务（net10.0，由 Desktop 承载）
├── HyperMark.Desktop/      # Windows 桌面应用（net10.0-windows, Avalonia UI）
└── HyperMark.Tests/        # 单元测试（xunit）
```

## 核心库目录结构

```
HyperMark/
├── HyperMark.cs                 # 主入口类，封装 AddUri/IsMarked 等核心操作
├── HyperMarkJsonContext.cs      # JSON 序列化上下文
├── ApiRequests.cs               # API 请求 DTO（16 个类）
├── ApiResponses.cs              # API 响应 DTO（17 个 record）
├── FileLogger.cs                # 文件日志工具
├── Config/
│   └── AppConfig.cs             # 应用配置（~/.hypermark/config.json）
├── Matching/
│   └── RouteMatcher.cs          # 路由匹配引擎（正则编译、参数解析）
├── Models/
│   ├── Link.cs                  # 链接收藏模型（核心实体）
│   ├── Site.cs                  # 站点模型
│   ├── Page.cs                  # 页面元信息（Link 的可选属性）
│   ├── Route.cs                 # 路由模型
│   ├── RouteArg.cs              # 路由参数模型
│   ├── Category.cs              # 分类模型（树形结构）
│   ├── Tag.cs                   # 标签模型
│   ├── BuiltInTags.cs           # 内置标签定义（system:page, system:nosite）
│   ├── Fallback.cs              # 兜底站点和路由定义
│   └── SiteDomain.cs            # 域名-站点映射
├── Parsers/
│   └── UrlParser.cs             # URL 解析器
└── Storage/
    ├── IStorage.cs              # 存储接口（MarkStatus 枚举 + CRUD）
    ├── LocalStorage.cs          # 文件系统存储实现
    ├── CacheStorage.cs          # 内存缓存代理层（装饰器模式）
    ├── ActionLogger.cs          # 操作日志装饰器（数据恢复用）
    └── Repository.cs            # 业务逻辑封装
```

## Web 服务目录结构

```
HyperMark.Web/
├── Program.cs                   # WebHost.CreateApp() 入口
├── BackgroundLinkProcessor.cs   # 后台链接解析（Channel + IHostedService）
├── Api/
│   ├── SystemEndpoints.cs       # /api/health, /api/replay/links
│   ├── MarkEndpoints.cs         # /api/mark, /api/unmark, /api/is_marked
│   ├── SitesEndpoints.cs        # /api/sites CRUD + domains
│   ├── LinksEndpoints.cs        # /api/links CRUD + tags + 分页
│   ├── ParseEndpoints.cs        # /api/parse, /api/parse/match, /api/parse/batch
│   ├── CategoriesEndpoints.cs   # /api/categories CRUD + move
│   ├── TagsEndpoints.cs         # /api/tags CRUD + autocomplete
│   ├── DomainCnameEndpoints.cs  # /api/domain-cnames CRUD
│   └── AdminEndpoints.cs        # /admin 破坏性操作
└── wwwroot/
    └── index.html               # 单页管理面板（纯 HTML/CSS/JS）
```

## 桌面应用目录结构

```
HyperMark.Desktop/
├── Program.cs                   # Avalonia 入口，单实例检查，承载 Web 服务
├── App.axaml / App.axaml.cs     # Avalonia 应用定义
├── TrayIconManager.cs           # 系统托盘管理器（Avalonia NativeMenu）
├── StartupManager.cs            # 注册表开机自启管理
└── Resources/
    └── app.ico
```

## 数据目录

```
~/.hypermark/                          # 用户数据目录
├── config.json                        # 配置文件（http, autoStart, startMinimized）
├── sites/{name}.json                  # 站点定义文件
├── bookmarks/[分类路径]/{title}_{ts}.json  # 链接收藏文件
├── categories.json                    # 分类列表（JSON 数组）
├── tags.json                          # 标签列表（JSON 数组）
├── domain_cnames.json                 # 域名 CNAME 映射（JSON 字典）
├── sites.actions                      # 站点操作日志
└── links.actions                      # 链接操作日志

{AppContext.BaseDirectory}/logs/       # 运行时日志（不在数据目录）
├── _access.log                        # API 访问日志
└── _error.log                         # 错误日志
```

## 核心数据模型

1. **Site（站点）**：JSON 格式，包含 title, name, homepage, domains, routes, vars
2. **Link（链接）**：核心实体，包含 url, title, category, tags, page（可选）
3. **Page（页面元信息）**：解析后的标准化信息，作为 Link 的可选属性
4. **Route（路由）**：URL 匹配规则，支持 `{param}` 占位符
5. **Category（分类）**：树形结构，通过 ParentId 实现层级关系

## 存储层架构

装饰器模式组装链：`LocalStorage` → `CacheStorage` → `ActionLogger` → `Repository`

- **LocalStorage**：纯文件系统，原子写入（.tmp + File.Move）
- **CacheStorage**：内存缓存，多维索引（URL/HyperId/分类/站点/标签）
- **ActionLogger**：记录写操作到 .actions 文件，支持 ReplayLinks 数据恢复
- **Repository**：业务验证（分类树防循环等）

### 链接删除

链接删除有两个独立入口，分别按不同维度定位链接：
- `DeleteLink(string url)`：根据原始 URL 删除
- `DeleteLinkByHyperId(string hyperId)`：根据 HyperId（`site://std_url`）删除

CacheStorage 维护 `_linksByHyperId` 索引，两种删除方式都会同步清理所有相关索引。

## 启动方式

```bash
dotnet run --project HyperMark.Web              # Web 服务模式
dotnet run --project HyperMark.Desktop          # 桌面应用模式
```

## 打包

```bash
dotnet publish HyperMark.Desktop/HyperMark.Desktop.csproj -c Release -r win-x64 -o ./publish-desktop
# 支持 AOT 编译（PublishAot=true）
```

## 开发参考

详细需求参见 `HyperMark.md`。
