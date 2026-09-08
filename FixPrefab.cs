using System;
using System.IO;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        string path = @""Assets\Resources\Robots\HarvestHavoc\ExampleA.prefab"";
        string content = File.ReadAllText(path);
        
        // Find all instances of PieceType: 7 with messed up AdditionalPieceTypes
        content = Regex.Replace(content, @""      PieceType: 7\r?\n        AdditionalPieceTypes:\r?\n        - 8"", @""      PieceType: 7
      AdditionalPieceTypes:
      - 8"");
      
        File.WriteAllText(path, content);
    }
}
