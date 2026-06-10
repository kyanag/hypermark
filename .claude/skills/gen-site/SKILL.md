---
name: gen-site
description: 当用户提供一个网址，要求抓取页面链接并生成 HyperMark Site JSON 定义时使用此技能。触发词："生成站点"、"抓取站点"、"gen-site"、"分析网站路由"。
---

# gen-site：从网页链接自动生成 Site 定义

用户会提供一个网站 URL，你需要：

1. 抓取页面，提取所有链接
2. 按路由模式分组
3. 生成符合 HyperMark 格式的 Site JSON

## 第一步：抓取页面并提取链接

使用项目自带的 Node.js 脚本抓取页面：

```bash
node scripts/fetch_links.mjs "<url>" --proxy http://127.0.0.1:10809
```

脚本返回 JSON 数组，每项包含 `url` 和 `text`（链接文字）。

**如果脚本报错或无法访问**，回退使用 `WebFetch` 工具（不支持代理，但可用于公开站点）。WebFetch 返回 markdown 格式，链接格式为 `[链接文字](URL)`。

### 多层次抓取策略

首页通常只有导航链接，真正的内容链接在子页面中。按以下顺序逐步深入：

1. **先抓首页**：提取导航栏链接，识别站点的主要分区（如论坛板块、频道、分类）
2. **识别导航链接**：从首页链接中找出代表主要分区的链接（通常在导航栏、侧边栏中）
3. **抓取分区页面**：选择 2-5 个有代表性的分区页面，分别抓取以获取内容链接
4. **提取内容链接**：从分区页面中提取实际的内容链接（帖子、文章、视频等）

每次抓取子页面时，运行：
```bash
node scripts/fetch_links.mjs "<子页面URL>" --proxy http://127.0.0.1:10809
```

**判断何时需要深入抓取**：
- 如果首页已经包含大量不同类型的内容链接（如文章列表、商品列表），可以直接进入第二步
- 如果首页主要是导航链接（板块列表、分类入口），需要继续抓取子页面
- 每次抓取新页面时，告诉用户你正在分析哪个子页面

### 处理翻页和动态加载

- 如果页面有翻页（`?page=2`、`/page/2`），只需抓取第一页即可
- 如果页面使用 JS 动态加载内容（WebFetch 只返回静态 HTML），告知用户抓取结果可能不完整，建议用户手动补充链接

### 链接去重和过滤

- 过滤掉明显非内容链接：静态资源（.css, .js, .png, .jpg）、锚点链接（#...）、javascript: 链接
- 去除重复链接
- 保留站内链接，过滤外部链接（除非是该站点的其他域名）

## 第二步：分析和分组

将提取的链接按 URL 结构分组，识别路由模式：

### 分组方法

1. 提取每个链接的路径部分（去掉域名和 query string 先看路径结构）
2. 按路径的「骨架」分组 — 将数字/字符串 ID 替换为 `{param}` 占位符
3. 对于 query string 中的参数，也用 `{param}` 替换值部分

### 常见 URL 模式示例

**路径参数型**：
```
/articles/12345          → /articles/{article_id}
/user/john/posts         → /user/{username}/posts
/forum/2-thread-345.html → /forum/{_}-thread-{thread_id}.html
```

**查询参数型**：
```
/forum.php?mod=viewthread&tid=1234  → /forum.php?mod=viewthread&tid={thread_id}
/search?q=keyword&page=2            → /search?q={keyword}&page={page}
```

**混合型**：
```
/blog/post-slug?ref=sidebar  → /blog/{slug}?ref={ref}
```

### 分组注意事项

- 同一类内容可能有多种 URL 格式（旧版/新版），它们应该成为同名的多个 route
- 分页参数（page, pn, p 等）通常用 `{_}` 通配符处理，不声明为 arg
- 排序、筛选等可选参数不放入 pattern，而是在 args 中设为 `isRequired: false`

## 第三步：生成 Site JSON

根据分析结果，生成完整的 Site 定义。

### JSON 结构

```json
{
  "title": "站点中文名称",
  "name": "site-id",
  "homepage": "https://example.com/",
  "domains": ["example.com", "www.example.com"],
  "routes": [ ... ],
  "vars": {}
}
```

### Route 结构

```json
{
  "title": "路由中文名称",
  "name": "route-id",
  "pattern": "/path/{param}",
  "args": [ ... ],
  "stdFormat": "/path/{param}",
  "tags": ["@tag1", "@tag2"]
}
```

### RouteArg 结构

```json
{
  "title": "参数中文说明",
  "name": "param_name",
  "default": null,
  "isRequired": true
}
```

### Pattern 语法规则

1. **路径参数**：`{param_name}` 标记变化的路径段
   - `/article/{article_id}` 匹配 `/article/12345`
2. **查询参数（Standard 模式）**：`?key={param}` 标记
   - `/forum.php?mod=viewthread&tid={thread_id}`
   - 静态参数直接写值：`mod=viewthread`
3. **通配符段**：`{_}` 标记不关心的变化段（如分页码）
   - `/forum-{forum_id}-{_}.html` 中 `{_}` 不声明为 arg
4. **PathStyle 模式**：`?` 后非标准 key=value 格式时，整体作为路径匹配
   - 例：`/search?tid-{tid}-keyword-{keyword}.html`
   - 需设置 `"queryMode": "PathStyle"`

### StdFormat 规则

- 去掉无关变化（分页、排序），用固定值替代
- 分页统一用 `-1` 结尾
- 例：`pattern: "/forum-{forum_id}-{_}.html"` → `stdFormat: "/forum-{forum_id}-1.html"`

### Tag 约定

标签以 `@` 开头，常用标签：

| 标签 | 含义 |
|------|------|
| `@article` | 文章/帖子详情 |
| `@thread` | 论坛帖子 |
| `@forum` | 论坛板块 |
| `@category` | 分类/列表页 |
| `@list` | 列表页 |
| `@tag` | 标签页 |
| `@user` | 用户主页 |
| `@search` | 搜索结果 |
| `@video` | 视频页 |
| `@image` | 图片页 |
| `@download` | 下载页 |
| `@comment` | 评论 |
| `@other` | 其他 |

可以自由组合，如 `["@forum", "@category", "@list"]`。

### Args 规则

- `name` 与 pattern 中 `{param_name}` 完全一致
- `title` 用中文说明参数含义
- `isRequired`：核心标识参数 `true`，可选参数 `false`
- `default`：有合理默认值时填写，否则 `null`
- `{_}` 通配符**不**声明为 arg

### 多域名处理

- 不同子域名都加入 `domains`
- `homepage` 使用主域名

### 同一内容多种 URL 格式

创建多个同名 route，共享 `title`、`tags`、`stdFormat`：

```json
[
  { "name": "thread", "pattern": "/thread-{thread_id}.html", ... },
  { "name": "thread", "pattern": "/forum.php?mod=viewthread&tid={thread_id}", ... }
]
```

### name 命名规则

- `Site.name`：简短英文标识，如 `github`、`zhihu`、`bilibili`
- `Route.name`：简短英文，描述内容类型，如 `article`、`thread`、`user`

## 第四步：验证和输出

生成 JSON 后，进行自检：

1. **Pattern 语法检查**：所有 `{param}` 在 args 中都有对应声明（`{_}` 除外）
2. **StdFormat 检查**：stdFormat 中的参数名与 pattern 一致
3. **必填参数检查**：核心标识参数（如 ID）应设为 `isRequired: true`
4. **路由覆盖检查**：提取到的所有链接至少能被一个 route 匹配

如果有不确定的地方，在 JSON 之后用注释说明：
- 哪些链接可能需要额外的 route
- 哪些参数的语义不确定（需要用户确认）
- 是否检测到多种 URL 格式指向同一内容

## 输出格式

最终输出时：

1. 先输出完整的 Site JSON（可直接复制使用）
2. 再输出简要说明：
   - 识别到的路由数量
   - 每个路由匹配的链接数
   - 需要用户确认的不确定项

## 完整示例

**用户输入**：`/gen-site https://www.example.com`

**你的操作流程**：

1. `node scripts/fetch_links.mjs "https://www.example.com" --proxy http://127.0.0.1:10809` → 获取首页链接
2. 分析链接，发现主要是导航链接
3. 识别出主要分区：`/articles/`、`/videos/`、`/users/`
4. `node scripts/fetch_links.mjs "https://www.example.com/articles/" --proxy http://127.0.0.1:10809` → 获取文章列表页链接
5. `node scripts/fetch_links.mjs "https://www.example.com/videos/" --proxy http://127.0.0.1:10809` → 获取视频列表页链接
6. 从所有页面汇总链接，分组分析
7. 输出 Site JSON + 说明

## 依赖

零依赖，纯 Node.js 内置模块（`node:http`、`node:https`）。无需 `npm install`。

如遇反爬虫保护严格的站点，可安装 Python 版 Scrapling 获得更强的抓取能力（需先安装 Python）：

```bash
pip install scrapling
```
