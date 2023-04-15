using System.Diagnostics;
using System.Reflection;

namespace model_generator;

public class Generator {
    private string _basePath;

    public void Process(GeneratorOptions options) {
        if (
            options == null ||
            !options.Sources.Any() ||
            !options.ConvertTypes.Any() ||
            string.IsNullOrEmpty(options.Compiled) ||
            string.IsNullOrEmpty(options.Files?.FirstOrDefault())
           ) {
            throw new Exception("options incorrect");
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        var generalTypes = new HashSet<Type>();

        foreach (var source in options.Sources) {
            try {
                _basePath = AbsolutePath(Path.Combine(source, options.Compiled));

                Console.Write("Scanning for DTO objects in {0}...  ", _basePath);
                var strs = options.Files.SelectMany(f => Directory.GetFiles(_basePath, f));
                var assemblies = strs.Select(Load).Where(a => a != null);
                var list = assemblies.SelectMany(x => GetAssemblyTypes(x, source)).ToList();
                var types = new HashSet<Type>(list);
                var array = types.ToArray();
                foreach (var t in array) {
                    RecursivelySearchModels(t, types);
                    generalTypes.Add(t);
                }

                Console.WriteLine($"count types: {types.Count}");

                Console.ForegroundColor = types.Count > 0 ? ConsoleColor.Green : ConsoleColor.Yellow;
                Console.WriteLine("Found {0}", types.Count);
                Console.ResetColor();
            } catch (DirectoryNotFoundException e) {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"IGNORED {e.Message}");
                Console.ResetColor();
            }
        }

        Console.WriteLine($"generalTypes Count: {generalTypes.Count}");

        foreach (var convertType in options.ConvertTypes) {
            var modelTargetPath = GetDestinationPath(convertType, options);
            if (Directory.Exists(modelTargetPath)) {
                Directory.Delete(modelTargetPath, true);
            }

            Directory.CreateDirectory(modelTargetPath);
            EntityGenerator.Generate(modelTargetPath, generalTypes, convertType, options);

            if (convertType == ConvertType.Kt) {
                Directory.CreateDirectory(Path.Combine(modelTargetPath, "interfaces"));
                EntityGenerator.GenerateKtInterfaces(options.KtPackageName, modelTargetPath);
                if (options.KtUseRealmDb ?? false) {
                    Directory.CreateDirectory(Path.Combine(modelTargetPath, "realm"));
                    Directory.CreateDirectory(Path.Combine(modelTargetPath, "helpers"));
                    EntityGenerator.GenerateKtRealmListConverter(options.KtPackageName, modelTargetPath);
                }
            }

            if (convertType == ConvertType.Swift && (options.SwiftUseRealmDb ?? false)) {
                Directory.CreateDirectory(Path.Combine(modelTargetPath, "protocols"));
                EntityGenerator.GenerateSwiftProtocols(modelTargetPath);
            }

            if (convertType == ConvertType.Ts && !(options.SkipTsFormInterfaces ?? false)) {
                var interfaceTargetPath = GetDestinationPath(convertType, options, true);
                if (Directory.Exists(interfaceTargetPath)) {
                    Directory.Delete(interfaceTargetPath, true);
                }

                Directory.CreateDirectory(interfaceTargetPath);
                EntityGenerator.Generate(interfaceTargetPath, generalTypes, convertType, options, true);
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Done in {0:N3}s", stopwatch.Elapsed.TotalSeconds);
        Console.ResetColor();
    }

    private string AbsolutePath(string relativePath) {
        return Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(Environment.CurrentDirectory, relativePath);
    }

    private IEnumerable<Type> GetAssemblyTypes(Assembly a, string path) {
        var filesName = Directory.EnumerateFiles(AbsolutePath(path), "*", SearchOption.AllDirectories)
            .Select(x => Path.GetFileNameWithoutExtension(x).ToLower())
            .ToList();

        Console.WriteLine($"count filesName: {filesName.Count}");

        var assembly = a.GetTypes()
            .Where(t => filesName
            .Contains(
                        t.Name.ToLower()) &&
                        !(t.Name.ToLower().Contains("sync")) ||
                        t.IsAbstract &&
                        !(t.IsAbstract && t.IsSealed)
                     )
            .ToList();
        Console.WriteLine($"count assembly: {assembly.Count}");
        return assembly;
    }

    private IEnumerable<Type> GetModelTypes(Type t) {
        if (t.IsModelType()) {
            if (!t.IsArray) {
                yield return t;
            } else {
                yield return t.GetElementType();
            }
        } else if (t.IsGenericType) {
            var genericArguments = t.GetGenericArguments();
            foreach (var type in (
                         from a in genericArguments
                         where a.IsModelType()
                         select a).SelectMany(GetModelTypes)) {
                yield return type;
            }
        }
        if (t.BaseType != null && t.BaseType.IsModelType()) {
            yield return t.BaseType;
        }
    }

    private Assembly Load(string path) {
        Assembly assembly;
        try {
            assembly = Assembly.Load(File.ReadAllBytes(path));
        } catch {
            assembly = null;
        }
        return assembly;
    }
    private void RecursivelySearchModels(Type model, ISet<Type> visitedModels) {
        var types = (
                from p in model.GetProperties()
                select p.GetPropertyType()).SelectMany(GetModelTypes)
            .Where(t => !visitedModels.Contains(t) && t.IsModelType() && t.ContainsGenericParameters);
        foreach (var type in types) {
            visitedModels.Add(type);
            RecursivelySearchModels(type, visitedModels);
        }
    }

    private Assembly ResolveAssembly(object sender, ResolveEventArgs args) {
        Assembly assembly;
        try {
            var str = Path.Combine(_basePath,
                string.Concat(args.Name.Substring(0, args.Name.IndexOf(",", StringComparison.Ordinal)), ".dll"));
            assembly = Assembly.Load(File.ReadAllBytes(str));
        } catch {
            Console.WriteLine(args.Name);
            assembly = null;
        }
        return assembly;
    }

    private string GetDestinationPath(ConvertType convertType, GeneratorOptions options, bool isInterface = false) {
        switch (convertType) {
            case ConvertType.Ts:
                return isInterface
                    ? Path.GetFullPath(Path.Combine(AbsolutePath(options.TsDestination), "generated/form-interfaces"))
                    : Path.GetFullPath(Path.Combine(AbsolutePath(options.TsDestination), "generated"));
            case ConvertType.Kt:
                return Path.GetFullPath(Path.Combine(AbsolutePath(options.KtDestination), "generated"));
            case ConvertType.Swift:
                return Path.GetFullPath(Path.Combine(AbsolutePath(options.SwiftDestination), "Generated"));
            default:
                throw new ArgumentOutOfRangeException(nameof(convertType), convertType, null);
        }
    }
}
