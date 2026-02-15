using System.Text.Json.Serialization;

namespace SonOfAnton.Shared.DTOs;

/// <summary>
/// SCIM 2.0 User resource (RFC 7643)
/// </summary>
public class ScimUser
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = ["urn:ietf:params:scim:schemas:core:2.0:User"];

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public ScimName? Name { get; set; }

    [JsonPropertyName("emails")]
    public List<ScimEmail>? Emails { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; } = true;

    [JsonPropertyName("groups")]
    public List<ScimGroupRef>? Groups { get; set; }

    [JsonPropertyName("meta")]
    public ScimMeta? Meta { get; set; }
}

public class ScimName
{
    [JsonPropertyName("givenName")]
    public string? GivenName { get; set; }

    [JsonPropertyName("familyName")]
    public string? FamilyName { get; set; }

    [JsonPropertyName("formatted")]
    public string? Formatted { get; set; }
}

public class ScimEmail
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "work";

    [JsonPropertyName("primary")]
    public bool Primary { get; set; }
}

public class ScimGroupRef
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("display")]
    public string? Display { get; set; }
}

public class ScimMeta
{
    [JsonPropertyName("resourceType")]
    public string ResourceType { get; set; } = "User";

    [JsonPropertyName("created")]
    public DateTime? Created { get; set; }

    [JsonPropertyName("lastModified")]
    public DateTime? LastModified { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }
}

/// <summary>
/// SCIM 2.0 Group resource
/// </summary>
public class ScimGroup
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = ["urn:ietf:params:scim:schemas:core:2.0:Group"];

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("members")]
    public List<ScimMember>? Members { get; set; }

    [JsonPropertyName("meta")]
    public ScimMeta? Meta { get; set; }
}

public class ScimMember
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("display")]
    public string? Display { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "User";
}

/// <summary>
/// SCIM 2.0 ListResponse
/// </summary>
public class ScimListResponse<T>
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = ["urn:ietf:params:scim:api:messages:2.0:ListResponse"];

    [JsonPropertyName("totalResults")]
    public int TotalResults { get; set; }

    [JsonPropertyName("startIndex")]
    public int StartIndex { get; set; } = 1;

    [JsonPropertyName("itemsPerPage")]
    public int ItemsPerPage { get; set; }

    [JsonPropertyName("Resources")]
    public List<T> Resources { get; set; } = [];
}

/// <summary>
/// SCIM 2.0 PatchOp
/// </summary>
public class ScimPatchRequest
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = ["urn:ietf:params:scim:api:messages:2.0:PatchOp"];

    [JsonPropertyName("Operations")]
    public List<ScimPatchOperation> Operations { get; set; } = [];
}

public class ScimPatchOperation
{
    [JsonPropertyName("op")]
    public string Op { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("value")]
    public object? Value { get; set; }
}

/// <summary>
/// SCIM 2.0 Error response
/// </summary>
public class ScimError
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = ["urn:ietf:params:scim:api:messages:2.0:Error"];

    [JsonPropertyName("status")]
    public string Status { get; set; } = "400";

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }
}
