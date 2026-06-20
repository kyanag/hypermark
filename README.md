# HyperMark

增强版网页收藏工具，支持站点管理、URL 智能解析、链接收藏与分类管理。提供 Web 服务和 Windows 桌面应用两种运行模式。

## 功能特性

- **站点管理**：以 JSON 格式定义站点元信息（域名、路由规则等）
- **URL 智能解析**：根据站点路由规则自动解析 URL，生成标准化页面元信息
- **链接收藏**：支持书签增删查，后台异步解析链接
- **分类管理**：支持多级树形分类，拖拽移动
- **标签管理**：支持标签搜索、自动补全
- **收录状态**：No（未收录）、Half（部分匹配）、Full（完全匹配）
- **操作日志**：记录写操作，支持数据恢复
- **域名映射**：支持域名 CNAME 映射

## 技术栈

- .NET 10.0（LTS）
- ASP.NET Core Minimal API
- Avalonia UI 12.x（桌面端，支持 AOT 编译）
- 文件系统存储（无数据库依赖）

## 项目结构

```
HyperMark.slnx
├── HyperMark/                  # 核心库（net10.0 类库）
│   ├── Models/                 # 数据模型（Site, Link, Page, Route, Category, Tag）
│   ├── Matching/               # 路由匹配引擎
│   ├── Parsers/                # URL 解析器
│   └── Storage/                # 存储层（LocalStorage → CacheStorage → ActionLogger → Repository）
├── HyperMark.Web/              # Web 服务（net10.0，由 Desktop 承载）
│   ├── Api/                    # API 端点定义
│   └── wwwroot/                # 前端管理面板
├── HyperMark.Desktop/          # Windows 桌面应用（net10.0-windows, Avalonia UI）
└── HyperMark.Tests/            # 单元测试（xunit）
```

## 数据目录

```
~/.hypermark/
├── config.json                 # 配置文件（http 地址等）
├── sites/{name}.json           # 站点定义
├── bookmarks/[分类路径]/*.json # 链接收藏
├── categories.json             # 分类列表
├── tags.json                   # 标签列表
├── domain_cnames.json          # 域名 CNAME 映射
├── sites.actions               # 站点操作日志
└── links.actions               # 链接操作日志
```

运行时日志在应用目录的 `logs/` 下（`_access.log`、`_error.log`）。

## 快速开始

### 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download)（开发时）
- 或直接使用发布包（无需安装运行时）

### 开发运行

```bash
git clone <repository-url>
cd HyperMark
dotnet run --project HyperMark.Web
```

服务默认启动在 `http://localhost:5000`，访问 `http://localhost:5000` 打开管理面板。

### 桌面模式

```bash
dotnet run --project HyperMark.Desktop
```

启动后出现在系统托盘，右键菜单可：暂停/启动服务、打开 Web 界面、打开数据目录、设置开机自启。基于 Avalonia UI 构建，支持 AOT 编译。

### 自定义配置

编辑 `~/.hypermark/config.json`：

```json
{
  "http": "http://localhost:8080"
}
```

### 发布

```powershell
# 运行发布脚本（生成两个版本）
./publish.ps1

# 或手动发布
dotnet publish HyperMark.Desktop/HyperMark.Desktop.csproj -c Release -o ./publish/dependent
dotnet publish HyperMark.Desktop/HyperMark.Desktop.csproj -c Release --self-contained true -o ./publish/self-contained

# AOT 编译（需在 csproj 中设置 PublishAot=true）
dotnet publish HyperMark.Desktop/HyperMark.Desktop.csproj -c Release -r win-x64 -o ./publish/aot
```

- **框架依赖版本**：体积小，需用户安装 .NET 运行时
- **Self-Contained 版本**：体积大，无需额外安装
- **AOT 版本**：原生编译，启动最快，体积适中

## API 参考

### 系统

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/api/health` | 健康检查（返回状态和版本号） |
| `POST` | `/api/replay/links` | 从操作日志重建链接数据 |

### 收藏

| 方法 | 路径 | 说明 |
|------|------|------|
| `POST` | `/api/mark` | 快捷收藏（url, title?, category?, tags?） |
| `POST` | `/api/unmark` | 取消收藏（url, force? — force=true 时按 HyperId 匹配删除） |
| `GET` | `/api/is_marked?url=` | 检查是否已收藏（返回 MarkStatus: 0=未收藏, 1=HyperId匹配, 3=URL匹配） |

### 站点

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/api/sites` | 获取所有站点 |
| `GET` | `/api/sites/{name}` | 获取站点详情 |
| `POST` | `/api/sites` | 创建站点（JSON body） |
| `PUT` | `/api/sites/{name}` | 更新站点 |
| `GET` | `/api/sites/{name}/domains` | 获取站点域名列表 |
| `POST` | `/api/sites/{name}/domains` | 添加域名 |
| `DELETE` | `/api/sites/{name}/domains/{domain}` | 删除域名 |
| `GET` | `/api/sites/{name}/links` | 获取站点下所有链接 |

### 链接

| 方法 | 路径 | 说明 |
|------|------|------|
| `POST` | `/api/links` | 创建链接 |
| `GET` | `/api/links?site=&category=&tag=&tags=&route=&q=&limit=&offset=` | 查询链接（分页） |
| `GET` | `/api/links/{url}` | 获取链接详情 |

**查询参数说明**：
- `site` - 按站点名筛选
- `category` - 按分类筛选
- `tag` - 按单个标签筛选（兼容旧接口）
- `tags` - 按多个标签筛选（AND 逻辑，链接必须包含所有指定标签）
- `route` - 按路由筛选（匹配 `page.route` 字段）
- `q` - 关键字搜索（匹配标题或 URL，不区分大小写）
| `PUT` | `/api/links/{url}` | 更新链接 |
| `DELETE` | `/api/links/{url}` | 删除链接 |
| `GET` | `/api/links/{url}/tags` | 获取链接标签 |
| `POST` | `/api/links/{url}/tags` | 为链接添加标签 |
| `DELETE` | `/api/links/{url}/tags/{tagId}` | 移除链接标签 |

### URL 解析

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/api/parse?url=` | 解析 URL，返回页面信息 |
| `GET` | `/api/parse/match?url=` | URL 匹配测试 |
| `POST` | `/api/parse/batch` | 批量解析 |

### 分类

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/api/categories` | 获取所有分类 |
| `GET` | `/api/categories/{name}` | 获取分类详情 |
| `POST` | `/api/categories` | 创建分类（name, parentName?） |
| `PUT` | `/api/categories/{name}` | 重命名分类 |
| `PATCH` | `/api/categories/{name}/move` | 移动分类 |
| `GET` | `/api/categories/{name}/links` | 获取分类下链接 |

### 标签

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/api/tags?q=` | 搜索标签 |
| `GET` | `/api/tags/autocomplete?q=&limit=` | 标签自动补全 |
| `GET` | `/api/tags/{id}` | 获取标签详情 |
| `POST` | `/api/tags` | 创建标签 |
| `PUT` | `/api/tags/{id}` | 更新标签 |
| `GET` | `/api/tags/{id}/links` | 获取标签关联链接 |

### 域名映射

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/api/domain-cnames` | 获取所有映射 |
| `POST` | `/api/domain-cnames` | 添加映射 |
| `DELETE` | `/api/domain-cnames/{domain}` | 删除映射 |

### 管理（破坏性操作）

| 方法 | 路径 | 说明 |
|------|------|------|
| `DELETE` | `/admin/sites/{name}` | 删除站点（含其下所有链接） |
| `DELETE` | `/admin/categories/{name}?force=` | 删除分类（存在子分类/链接时需 force=true） |
| `DELETE` | `/admin/tags/{id}` | 删除标签 |
| `POST` | `/admin/replay/links` | 重建链接数据 |

## 核心概念

### 站点（Site）

| 字段 | 类型 | 说明 |
|------|------|------|
| `name` | string | 唯一标识 |
| `title` | string | 显示名称 |
| `homepage` | string | 主页 URL |
| `domains` | string[] | 其他域名列表 |
| `routes` | Route[] | 路由规则 |
| `vars` | object | 自定义属性 |

### 路由（Route）

| 字段 | 类型 | 说明 |
|------|------|------|
| `name` | string | 路由名称 |
| `title` | string | 路由标题 |
| `pattern` | string | URL 模式（`{param}` 占位符） |
| `stdFormat` | string | 标准化 URL 格式 |
| `args` | RouteArg[] | 参数说明 |
| `tags` | string[] | 分类标签 |

### 链接（Link）

| 字段 | 类型 | 说明 |
|------|------|------|
| `url` | string | 链接地址 |
| `name` | string | 链接名称 |
| `title` | string | 链接标题 |
| `createdAt` | datetime | 收藏时间 |
| `category` | string | 所属分类 |
| `tags` | string[] | 标签列表 |
| `page` | Page? | 解析后的页面元信息（可选） |

### Page 元信息

| 字段 | 类型 | 说明 |
|------|------|------|
| `original` | string | 原始 URL |
| `std` | string | 标准化 URL |
| `args` | object | 解析参数 |
| `site` | string | 所属站点 |
| `route` | string | 匹配的路由 |
| `hyperId` | string | 标准 ID（`site://std_url`） |

### 收录状态（MarkStatus）

| 状态 | 值 | 说明 |
|------|----|------|
| `No` | 0 | 未收录 |
| `Half` | 1 | 标准 URL 相同但原始 URL 不同 |
| `Full` | 3 | 完全匹配 |

## Claude Code Skills

项目配置了以下 Claude Code 技能（在 Claude Code 中使用 `/skill-name` 触发）：

### /gen-site — 自动生成站点定义

当用户提供一个网站 URL，自动抓取页面链接并生成 HyperMark Site JSON 定义。

**触发词**：`生成站点`、`抓取站点`、`gen-site`、`分析网站路由`

**使用方式**：

```
/gen-site https://www.example.com
```

**工作流程**：

1. 抓取页面，提取所有链接（支持多层次抓取：首页 → 分区页 → 内容页）
2. 按 URL 结构分组，识别路由模式（路径参数、查询参数、混合型）
3. 生成符合 HyperMark 格式的 Site JSON

**示例**：

```
/gen-site https://github.com
```

输出：

```json
{
  "title": "GitHub",
  "name": "github",
  "homepage": "https://github.com",
  "domains": ["github.com", "www.github.com"],
  "routes": [
    {
      "title": "仓库",
      "name": "repo",
      "pattern": "/{owner}/{repo}",
      "args": [
        { "title": "所有者", "name": "owner", "default": null, "isRequired": true },
        { "title": "仓库名", "name": "repo", "default": null, "isRequired": true }
      ],
      "stdFormat": "/{owner}/{repo}",
      "tags": ["@repo"]
    }
  ],
  "vars": {}
}
```

**依赖**：

```bash
# 需要 Node.js（用于页面抓取脚本）
node scripts/fetch_links.mjs "<url>" --proxy http://127.0.0.1:10809

# 可选：Python Scrapling（反爬虫严格的站点）
pip install scrapling
```

## curl 示例

```bash
# 健康检查
curl http://localhost:5000/api/health

# 收藏链接
curl -X POST http://localhost:5000/api/mark \
  -H "Content-Type: application/json" \
  -d '{"url": "https://github.com/user/repo", "title": "My Repo"}'

# 检查是否已收藏
curl "http://localhost:5000/api/is_marked?url=https%3A%2F%2Fgithub.com%2Fuser%2Frepo"

# 创建站点
curl -X POST http://localhost:5000/api/sites \
  -H "Content-Type: application/json" \
  -d '{
    "name": "github",
    "title": "GitHub",
    "homepage": "https://github.com",
    "routes": [
      {
        "name": "repo",
        "title": "仓库",
        "pattern": "/{owner}/{repo}",
        "stdFormat": "/{owner}/{repo}"
      }
    ]
  }'

# 创建分类
curl -X POST http://localhost:5000/api/categories \
  -H "Content-Type: application/json" \
  -d '{"name": "技术", "parentName": null}'

# 解析 URL
curl "http://localhost:5000/api/parse?url=https%3A%2F%2Fgithub.com%2Fuser%2Frepo"

# 查询链接（带筛选）
curl "http://localhost:5000/api/links?route=repo/{owner}/{name}&tags=starred&tags=important"
curl "http://localhost:5000/api/links?q=github"
```
