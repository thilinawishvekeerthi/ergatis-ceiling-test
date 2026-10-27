namespace Talent.Config.IntegrationTests;

internal static class Payloads
{
    public const string Scale5 = """
        {"levels":[
          {"level":1,"label":"Poor","minScore":1,"band":"LOW"},
          {"level":2,"label":"Fair","minScore":2,"band":"LOW"},
          {"level":3,"label":"Good","minScore":3,"band":"MEDIUM"},
          {"level":4,"label":"Very good","minScore":4,"band":"HIGH"},
          {"level":5,"label":"Excellent","minScore":5,"band":"HIGH"}]}
        """;

    public const string Scale1Level = """{"levels":[{"level":1,"label":"Only","minScore":1,"band":"LOW"}]}""";

    public const string Grid2x2 = """
        {"mode":"Calculated","columnBounds":[1,3],"rowBounds":[1,3],
         "cells":[{"column":0,"row":0,"label":"A","colour":"#aaaaaa","band":"LOW"},
                  {"column":1,"row":0,"label":"B","colour":"#bbbbbb","band":"MEDIUM"},
                  {"column":0,"row":1,"label":"C","colour":"#cccccc","band":"MEDIUM"},
                  {"column":1,"row":1,"label":"D","colour":"#dddddd","band":"HIGH"}]}
        """;

    public const string Grid1x2 = """{"mode":"Manual","columnBounds":[1],"rowBounds":[1,3],"cells":[]}""";
}
