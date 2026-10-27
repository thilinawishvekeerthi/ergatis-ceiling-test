namespace Talent.Security.Matrix;

/// <summary>
/// The matrix a new install starts with, published as version 1. It is a starting point, not the client's answer:
/// the client replaces it with a new version through the Config API.
/// </summary>
internal static class DefaultPermissionMatrix
{
    public const string Name = "Permission matrix";

    public const string Json = """
        {"grants":[
          {"role":"EMPLOYEE","resource":"config:Scale","actions":["View"],"scope":"All","fieldClass":"Public","talentConfidential":false},
          {"role":"MANAGER","resource":"config:Scale","actions":["View"],"scope":"All","fieldClass":"Internal","talentConfidential":false},
          {"role":"MANAGER","resource":"config:Grid","actions":["View"],"scope":"All","fieldClass":"Internal","talentConfidential":false},
          {"role":"TALENT_COMMITTEE","resource":"config:*","actions":["View"],"scope":"All","fieldClass":"Internal","talentConfidential":true},
          {"role":"HR_ADMIN","resource":"config:*","actions":["View","Edit","Approve"],"scope":"All","fieldClass":"Internal","talentConfidential":true}]}
        """;
}
