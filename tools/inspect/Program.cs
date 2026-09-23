using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;

var path = @"D:\SteamLibrary\steamapps\common\Sailwind\Sailwind_Data\Managed\Assembly-CSharp.dll";
var names = args.Length > 0 ? args : new[] { "--help" };
if (names[0] is "-h" or "--help")
{
    Console.WriteLine("Usage: inspect [--dll path] <TypeName> [TypeName...]");
    Console.WriteLine("       inspect --dll path --list [substring]");
    return;
}

if (names[0] == "--dll" && names.Length >= 2)
{
    path = names[1];
    names = names.Skip(2).ToArray();
    if (names.Length == 0)
        names = new[] { "--help" };
}

var decompiler = new CSharpDecompiler(path, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
if (names[0] == "--list")
{
    var filter = names.Length > 1 ? names[1] : "";
    foreach (var type in decompiler.TypeSystem.MainModule.TypeDefinitions.OrderBy(t => t.FullName))
    {
        if (filter.Length == 0 || type.FullName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            Console.WriteLine(type.FullName);
    }
    return;
}

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
