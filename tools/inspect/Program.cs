using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;

var path = @"D:\SteamLibrary\steamapps\common\Sailwind\Sailwind_Data\Managed\Assembly-CSharp.dll";
var names = args.Length > 0 ? args : new[] { "--help" };
if (names[0] is "-h" or "--help")
{
    Console.WriteLine("Usage: inspect <TypeName> [TypeName...]");
    return;
}

var decompiler = new CSharpDecompiler(path, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
foreach (var name in names)
{
    var type = decompiler.TypeSystem.MainModule.TypeDefinitions.FirstOrDefault(t => t.Name == name);
    if (type == null)
    {
        Console.WriteLine($"MISSING {name}");
        continue;
    }

    Console.WriteLine($"\n========== {type.FullName} ==========\n");
    Console.WriteLine(decompiler.DecompileTypeAsString(type.FullTypeName));
}
