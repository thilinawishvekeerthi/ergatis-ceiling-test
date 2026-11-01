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
          {"role":"HR_ADMIN","resource":"config:*","actions":["View","Edit","Approve"],"scope":"All","fieldClass":"Internal","talentConfidential":true},
          {"role":"EMPLOYEE","resource":"assessment:Cycle","actions":["View"],"scope":"All","fieldClass":"Public","talentConfidential":false},
          {"role":"EMPLOYEE","resource":"assessment:Review","actions":["View"],"scope":"Self","fieldClass":"Internal","talentConfidential":false},
          {"role":"MANAGER","resource":"assessment:Cycle","actions":["View"],"scope":"All","fieldClass":"Internal","talentConfidential":false},
          {"role":"MANAGER","resource":"assessment:Review","actions":["View","Edit"],"scope":"CycleAssigned","fieldClass":"Internal","talentConfidential":true},
          {"role":"MANAGER","resource":"assessment:Board","actions":["View"],"scope":"CycleAssigned","fieldClass":"Internal","talentConfidential":true},
          {"role":"TALENT_COMMITTEE","resource":"assessment:*","actions":["View","Calibrate"],"scope":"All","fieldClass":"Internal","talentConfidential":true},
          {"role":"HR_ADMIN","resource":"assessment:*","actions":["View","Edit","Approve","Calibrate"],"scope":"All","fieldClass":"Internal","talentConfidential":true},
          {"role":"EMPLOYEE","resource":"career:Job","actions":["View"],"scope":"All","fieldClass":"Public","talentConfidential":false},
          {"role":"EMPLOYEE","resource":"career:Path","actions":["View"],"scope":"All","fieldClass":"Public","talentConfidential":false},
          {"role":"EMPLOYEE","resource":"career:Profile","actions":["View"],"scope":"Self","fieldClass":"Internal","talentConfidential":false},
          {"role":"MANAGER","resource":"career:Job","actions":["View"],"scope":"All","fieldClass":"Internal","talentConfidential":false},
          {"role":"MANAGER","resource":"career:Path","actions":["View"],"scope":"All","fieldClass":"Internal","talentConfidential":false},
          {"role":"TALENT_COMMITTEE","resource":"career:*","actions":["View"],"scope":"All","fieldClass":"Internal","talentConfidential":true},
          {"role":"HR_ADMIN","resource":"career:*","actions":["View","Edit"],"scope":"All","fieldClass":"Internal","talentConfidential":true}]}
        """;
}
