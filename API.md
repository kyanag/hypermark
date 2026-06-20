# API 文档

## 概述

HyperMark 提供 RESTful API，按功能分组：

| 前缀 | 用途 |
|------|------|
| `/api/` | 基础接口，浏览器插件和管理后台均可调用 |
| `/admin/` | 管理端，仅限破坏性操作（删除站点/分类/Tag） |

### 伪装方案

浏览器插件等环境可能限制 `PUT`/`DELETE`/`PATCH` 方法。服务端使用 ASP.NET Core 内置的 `UseHttpMethodOverride()` 中间件：

```
POST /api/xxx
Header: X-HTTP-Method-Override: PUT/DELETE/PATCH
```

框架自动将请求方法重写为对应值，路由按重写后的方法匹配。

### HTTP 状态码

| 状态码 | 含义 |
|--------|------|
| `200` | 成功 |
| `201` | 创建成功 |
| `204` | 删除成功（无 body） |
| `400` | 请求参数错误 |
| `404` | 资源不存在 |
| `409` | 资源冲突（重复） |
| `500` | 服务器内部错误 |

---

## 1. 系统接口

### 健康检查

```
GET /api/health
```

返回服务健康状态。

**响应：**
```json
{ "status": "ok", "version": "dev" }
```

---

### 从日志重建链接

```
POST /api/replay/links
```

从操作日志中重建链接数据。

**响应：**
```json
{ "message": "重建完成，恢复 N 条链接" }
```

---

## 2. 收藏快捷接口

语义直观的快捷操作，适合高频场景。是 `/api/links` 的包装，插件可自行选择。

### 收藏链接

```
POST /api/mark
```

**请求体：**

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `url` | string | 是 | 要收藏的链接 |
| `title` | string | 否 | 自定义标题（默认为 URL） |
| `category` | string | 否 | 所属分类名称 |
| `tags` | string[] | 否 | 标签名称列表 |

**响应：**
- `201 Created` — 收藏成功
- `400 Bad Request` — 缺少 `url` 字段
- `409 Conflict` — 该链接已收藏

---

### 取消收藏

```
POST /api/unmark
```

**请求体：**

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `url` | string | 是 | 要移除的链接 |
| `force` | bool | 否 | 为 `true` 时按 HyperId 匹配删除（默认 `false`，仅按 URL 删除） |

**响应：**
- `200 OK` — 取消收藏成功
- `400 Bad Request` — 缺少 `url` 字段
- `404 Not Found` — 链接不存在

---

### 检查是否已收藏

```
GET /api/is_marked?url=xxx
```

**查询参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `url` | string | 是 | 要检查的 URL |

**匹配逻辑：** 先按 URL 精确匹配，未命中则解析 URL 获取 HyperId 再匹配。

**响应：**

| status | 含义 |
|--------|------|
| `0` (No) | 未收藏 |
| `1` (Half) | 已收藏，通过 HyperId 匹配（URL 不同） |
| `3` (Full) | 已收藏，URL 完全一致 |

未收藏：
```json
{ "status": 0 }
```

已收藏（URL 精确匹配）：
```json
{
  "status": 3,
  "link": {
    "url": "https://example.com/page",
    "title": "页面标题",
    "category": "默认分类",
    "tags": ["tag1", "tag2"]
  }
}
```

已收藏（HyperId 匹配，URL 不同）：
```json
{
  "status": 1,
  "link": {
    "url": "https://example.com/page",
    "title": "页面标题",
    "category": "默认分类",
    "tags": ["tag1", "tag2"]
  }
}
```

---

## 3. 站点 (Sites)

以 `name` 作为唯一标识。删除操作归入 `/admin`。

### 列出所有站点

```
GET /api/sites
```

返回简要信息（仅 name + title）。

**响应：**
```json
[
  { "name": "github", "title": "GitHub" }
]
```

---

### 获取站点详情

```
GET /api/sites/{name}
```

返回站点基本信息（name, title, homepage）。

**响应：**
- `200 OK` — 站点信息
- `404 Not Found` — 站点不存在

---

### 获取完整站点信息

```
GET /api/sites/{name}/full
```

返回完整站点信息（含 routes, domains, vars）。

---

### 创建站点

```
POST /api/sites
```

支持 JSON 格式请求体。

**请求体（JSON）：**
```json
{
  "name": "github",
  "title": "GitHub",
  "homepage": "https://github.com",
  "domains": ["github.com"],
  "routes": [],
  "vars": {}
}
```

**响应：**
- `201 Created` — 站点创建成功
- `400 Bad Request` — 格式错误或缺少 `name` 字段

---

### 更新站点

```
PUT /api/sites/{name}
```

部分更新，仅传递需要修改的字段。

**请求体：**

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `title` | string | 否 | 站点标题 |
| `homepage` | string | 否 | 主页 URL |
| `domains` | string[] | 否 | 关联域名列表 |
| `routes` | Route[] | 否 | 路由规则列表 |
| `vars` | object | 否 | 其他属性 |

**响应：**
- `200 OK` — 更新成功
- `404 Not Found` — 站点不存在

---

### 获取站点域名

```
GET /api/sites/{name}/domains
```

**响应：**
```json
{ "name": "github", "domains": ["github.com", "github.io"] }
```

---

### 添加站点域名

```
POST /api/sites/{name}/domains
```

**请求体：**
```json
{ "domain": "github.io" }
```

**响应：**
- `200 OK` — 添加成功
- `404 Not Found` — 站点不存在

---

### 删除站点域名

```
DELETE /api/sites/{name}/domains/{domain}
```

**响应：**
- `200 OK` — 删除成功
- `404 Not Found` — 站点不存在

---

### 获取站点下的链接

```
GET /api/sites/{name}/links
```

**响应：** `Link` 对象列表

---

### 删除站点

```
DELETE /admin/sites/{name}
```

> 管理端接口，详见 [管理端接口](#9-管理端-admin) 章节。
> 客户端伪装：`POST` + `X-HTTP-Method-Override: DELETE`

---

## 4. 链接 (Links)

以 `url` 作为链接的唯一标识。路径参数中的 `{urlEncoded}` 需要 `encodeURIComponent()` 编码。

### 创建链接

```
POST /api/links
```

**请求体：**

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `url` | string | 是 | 链接 URL |
| `title` | string | 否 | 自定义标题 |
| `category` | string | 否 | 分类名称 |
| `tags` | string[] | 否 | 标签名称列表 |

**响应：**
- `201 Created` — 创建成功
- `400 Bad Request` — 缺少 `url` 字段
- `409 Conflict` — 该链接已收藏

---

### 列出链接

```
GET /api/links?site=&category=&tag=&limit=&offset=
```

支持按站点、分类、标签筛选，支持分页。

**查询参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `site` | string | 否 | 按站点名称筛选 |
| `category` | string | 否 | 按分类名称筛选 |
| `tag` | string | 否 | 按标签名称筛选 |
| `limit` | int | 否 | 每页数量（默认 50，最大 200） |
| `offset` | int | 否 | 偏移量（默认 0） |

**响应：**
```json
{
  "data": [
    { "url": "...", "title": "...", "category": "...", "tags": [], "page": {...}, "createdAt": "..." }
  ],
  "total": 100,
  "limit": 50,
  "offset": 0
}
```

---

### 获取单个链接

```
GET /api/links/{urlEncoded}
```

**路径参数：** `urlEncoded` = `encodeURIComponent(url)`

**响应：**
- `200 OK` — `Link` 对象（含 page 信息）
- `404 Not Found` — 链接不存在

---

### 更新链接

```
PUT /api/links/{urlEncoded}
```

部分更新，仅传递需要修改的字段。

**请求体：**

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `title` | string | 否 | 新标题 |
| `category` | string | 否 | 新分类名称 |
| `tags` | string[] | 否 | 新标签列表（替换） |

**响应：**
- `200 OK` — 更新成功
- `404 Not Found` — 链接不存在

---

### 删除链接

```
DELETE /api/links/{urlEncoded}
```

**响应：**
- `200 OK` — 删除成功
- `404 Not Found` — 链接不存在

---

### 获取链接的标签

```
GET /api/links/{urlEncoded}/tags
```

**响应：** `Tag` 对象列表

---

### 为链接添加标签

```
POST /api/links/{urlEncoded}/tags
```

支持按 ID 或按名称添加标签。

**请求体：**

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `tagIds` | int[] | 否 | 标签 ID 列表 |
| `tags` | string[] | 否 | 标签名称列表（不存在则自动创建） |

**响应：**
```json
{ "message": "添加成功", "added": 3 }
```

---

### 移除链接标签

```
DELETE /api/links/{urlEncoded}/tags/{tagId}
```

**响应：**
- `200 OK` — 移除成功
- `404 Not Found` — 链接或标签不存在

---

## 5. 链接解析 (Parse)

独立解析能力，不绑定到链接 CRUD。

### 解析 URL

```
GET /api/parse?url=xxx
```

解析 URL，返回 page 信息。如果已收藏则同时返回链接信息。

**查询参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `url` | string | 是 | 要解析的 URL |

**响应：**
```json
{
  "page": {
    "hyperId": "github://user/repo",
    "site": "github",
    "std": "https://github.com/{user}/{repo}",
    "args": { "user": "xxx", "repo": "yyy" },
    "tags": [],
    "route": "repo"
  },
  "link": {
    "url": "https://github.com/xxx/yyy",
    "title": "页面标题",
    "category": "默认分类",
    "createdAt": "2026-06-06T10:00:00",
    "tags": []
  }
}
```

`link` 为 `null` 表示该 URL 未被收藏。

---

### 仅匹配站点和路由

```
GET /api/parse/match?url=xxx
```

**响应：**
```json
{
  "matched": true,
  "site": "github",
  "route": "repo"
}
```

---

### 批量解析

```
POST /api/parse/batch
```

批量解析已有链接的 page 信息。

**请求体：**
```json
{
  "urls": ["https://example.com/a", "https://example.com/b"]
}
```

`urls` 为空则解析所有未解析的链接。

**响应：**
```json
{ "message": "解析完成", "total": 10, "success": 8, "failed": 2 }
```

---

## 6. 分类 (Categories)

以 `name` 作为唯一标识。删除操作归入 `/admin`。

### 列出所有分类

```
GET /api/categories
```

**响应：** `Category` 对象列表
```json
[
  { "name": "默认分类", "parentId": null, "createdAt": "2026-06-06T10:00:00" }
]
```

---

### 获取单个分类

```
GET /api/categories/{name}
```

**响应：**
- `200 OK` — 分类信息
- `404 Not Found` — 分类不存在

---

### 创建分类

```
POST /api/categories
```

**请求体：**

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `name` | string | 是 | 分类名称 |
| `parentName` | string | 否 | 父分类名称（用于嵌套分类） |

**响应：**
- `201 Created` — 创建成功
- `400 Bad Request` — 缺少 `name` 字段
- `409 Conflict` — 分类已存在

---

### 重命名分类

```
PUT /api/categories/{name}
```

**请求体：**
```json
{ "newName": "新分类名称" }
```

**响应：**
- `200 OK` — 更新成功
- `404 Not Found` — 分类不存在

---

### 移动分类

```
PATCH /api/categories/{name}/move
```

修改分类的父分类。

**请求体：**
```json
{ "newParentName": "父分类名称" }
```

`newParentName` 为 `null` 表示移至根级。

**响应：**
- `200 OK` — 移动成功
- `400 Bad Request` — 循环引用或移动到自己
- `404 Not Found` — 分类不存在

---

### 获取分类下的链接

```
GET /api/categories/{name}/links
```

**响应：** `Link` 对象列表

---

### 删除分类

```
DELETE /admin/categories/{name}
```

> 管理端接口，详见 [管理端接口](#9-管理端-admin) 章节。

---

## 7. 标签 (Tags)

删除操作归入 `/admin`。

### 列出所有标签

```
GET /api/tags?q=&limit=
```

**查询参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `q` | string | 否 | 搜索关键词（匹配 name 或 title） |

**响应：** `Tag` 对象列表

---

### 标签自动补全

```
GET /api/tags/autocomplete?q=xxx&limit=10
```

**查询参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `q` | string | 是 | 搜索关键词 |
| `limit` | int | 否 | 返回数量（默认 10） |

**响应：**
```json
[
  { "id": 1, "name": "github", "title": "GitHub" }
]
```

---

### 获取单个标签

```
GET /api/tags/{id}
```

**响应：**
- `200 OK` — 标签信息
- `404 Not Found` — 标签不存在

---

### 创建标签

```
POST /api/tags
```

**请求体：**

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `name` | string | 是 | 标签名称 |
| `title` | string | 否 | 显示标题 |

**响应：**
- `201 Created` — 创建成功
- `400 Bad Request` — 缺少 `name` 字段
- `409 Conflict` — 标签已存在

---

### 更新标签

```
PUT /api/tags/{id}
```

部分更新。

**请求体：**

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `name` | string | 否 | 新名称 |
| `title` | string | 否 | 新标题 |

**响应：**
- `200 OK` — 更新成功
- `404 Not Found` — 标签不存在

---

### 获取标签下的链接

```
GET /api/tags/{id}/links
```

**响应：** `Link` 对象列表

---

### 删除标签

```
DELETE /admin/tags/{id}
```

> 管理端接口，详见 [管理端接口](#9-管理端-admin) 章节。

---

## 8. 域名 CNAME (Domain CNAME)

### 列出所有映射

```
GET /api/domain-cnames
```

**响应：**
```json
[
  { "domain": "old.example.com", "cname": "new.example.com" }
]
```

---

### 添加映射

```
POST /api/domain-cnames
```

**请求体：**
```json
{ "domain": "old.example.com", "cname": "new.example.com" }
```

**响应：**
- `200 OK` — 添加成功
- `400 Bad Request` — 缺少字段

---

### 删除映射

```
DELETE /api/domain-cnames/{domain}
```

**响应：**
- `200 OK` — 删除成功
- `404 Not Found` — 域名不存在

---

## 9. 管理端 (Admin)

破坏性操作（删除站点/分类/Tag），仅限管理后台调用。

客户端通过 `POST` + `X-HTTP-Method-Override: DELETE` 伪装。

### 删除站点

```
POST /admin/sites/{name}
Header: X-HTTP-Method-Override: DELETE
```

**响应：**
- `200 OK` — 删除成功
- `404 Not Found` — 站点不存在

---

### 删除分类

```
POST /admin/categories/{name}
Header: X-HTTP-Method-Override: DELETE
```

**响应：**
- `200 OK` — 删除成功
- `404 Not Found` — 分类不存在

---

### 删除标签

```
POST /admin/tags/{id}
Header: X-HTTP-Method-Override: DELETE
```

**响应：**
- `200 OK` — 删除成功
- `404 Not Found` — 标签不存在

---

### 从日志重建链接（管理端）

```
POST /admin/replay/links
```

**响应：**
```json
{ "message": "重建完成，恢复 N 条链接" }
```

---

## 数据模型

### Link

```json
{
  "url": "string",
  "title": "string",
  "createdAt": "ISO8601",
  "category": "string | null",
  "tags": ["string"],
  "page": {
    "hyperId": "string",
    "site": "string",
    "route": "string",
    "std": "string",
    "args": { "key": "value" },
    "tags": ["string"],
    "createAt": "ISO8601"
  } | null
}
```

### Category

```json
{
  "name": "string",
  "parentId": "string | null",
  "createdAt": "ISO8601"
}
```

### Tag

```json
{
  "id": "number",
  "name": "string",
  "title": "string",
  "createdAt": "ISO8601"
}
```

### Site

```json
{
  "name": "string",
  "title": "string",
  "homepage": "string",
  "domains": ["string"],
  "routes": [
    {
      "name": "string",
      "title": "string",
      "pattern": "string",
      "args": [{ "name": "string", "title": "string", "description": "string" }],
      "stdFormat": "string",
      "tags": ["string"]
    }
  ],
  "vars": { "key": "value" }
}
```

---

## 旧版接口（向后兼容）

旧版接口继续保留，与新接口共存：

| 旧接口 | 新接口 | 说明 |
|--------|--------|------|
| `POST /mark` | `POST /api/mark` | 功能一致 |
| `POST /unmark` | `POST /api/unmark` | 功能一致 |
| `GET /is_marked` | `GET /api/is_marked` | 功能一致 |
| `GET /info` | `GET /api/parse` | `/info` 返回格式更简洁 |
| `GET /categories` | `GET /api/categories` | 功能一致 |
| `GET /sites` | `GET /api/sites` | 功能一致 |
| `POST /move_category` | `PUT /api/links/{url}` | 移动分类变为链接更新的一部分 |
| `POST /add_tag` | `POST /api/links/{url}/tags` | 标签作为链接子资源 |
| `POST /add_domain_cname` | `POST /api/domain-cnames` | 功能一致 |
| `GET /api/sites` | `GET /api/sites` | 功能一致 |
| `POST /api/links` | `POST /api/links` | 新接口自动解析 Page |

---

## 插件端组合示例

### 场景 1：页面收藏按钮

```
1. GET /api/is_marked?url=xxx    → 检查状态
2. 未收藏: POST /api/mark        → 快速收藏
3. 已收藏: GET /api/links/{url}   → 获取详情展示
```

### 场景 2：收藏弹窗

```
1. GET /api/parse?url=xxx         → 解析当前页面
2. GET /api/categories             → 加载分类列表
3. GET /api/tags?q=xxx             → 标签搜索
4. POST /api/links                  → 保存或更新
```

### 场景 3：分类管理

```
1. GET /api/categories              → 加载列表
2. POST /api/categories             → 新建
3. POST /api/categories/{name}      → 拖拽排序 (X-HTTP-Method-Override: PATCH)
4. POST /api/categories/{name}      → 重命名 (X-HTTP-Method-Override: PUT)
5. POST /admin/categories/{name}    → 删除 (X-HTTP-Method-Override: DELETE)
```
