using HyperMark.Models;

namespace HyperMark;

/// <summary>
/// AOT 兼容的 API 请求类型
/// </summary>

// Mark
public class MarkShortcutRequest
{
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Category { get; set; }
    public List<string>? Tags { get; set; }
}

public class UnmarkShortcutRequest
{
    public string Url { get; set; } = string.Empty;
}

// Sites
public class SiteUpdateRequest
{
    public string? Title { get; set; }
    public string? Homepage { get; set; }
    public List<string>? Domains { get; set; }
    public List<Route>? Routes { get; set; }
    public Dictionary<string, string>? Vars { get; set; }
}

public class AddDomainRequest
{
    public string Domain { get; set; } = string.Empty;
}

// Links
public class CreateLinkRequest
{
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Category { get; set; }
    public List<string>? Tags { get; set; }
}

public class UpdateLinkRequest
{
    public string? Title { get; set; }
    public string? Category { get; set; }
    public List<string>? Tags { get; set; }
}

public class AddLinkTagsRequest
{
    public List<int>? TagIds { get; set; }
    public List<string>? Tags { get; set; }
}

// Parse
public class BatchParseRequestBody
{
    public List<string>? Urls { get; set; }
}

// Categories
public class CreateCategoryReq
{
    public string Name { get; set; } = string.Empty;
    public string? ParentName { get; set; }
}

public class RenameCategoryReq
{
    public string NewName { get; set; } = string.Empty;
}

public class MoveCategoryReq
{
    public string? NewParentName { get; set; }
}

// Tags
public class CreateTagReq
{
    public string Name { get; set; } = string.Empty;
    public string? Title { get; set; }
}

public class UpdateTagReq
{
    public string? Name { get; set; }
    public string? Title { get; set; }
}

// Domain CNAME
public class AddCnameRequest
{
    public string Domain { get; set; } = string.Empty;
    public string Cname { get; set; } = string.Empty;
}
