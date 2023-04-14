using System.Diagnostics;
using System.Reflection;

namespace model_generator;

public class Generator {
    private GeneratorOptions _options;
    private string _basePath;

    /// <summary>
    /// Delete existing generated model files and create new ones.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="convertTo"></param>
    /// <returns></returns>
    public void Process(GeneratorOptions options, string[] convertTo = null) {
        _options = options ?? new GeneratorOptions();

        Stopwatch stopwatch = Stopwatch.StartNew();

        var convertTypes = new HashSet<ConvertType>();

        if (convertTo?.Any() ?? false) {
            foreach (var type in convertTo) {
                if (Enum.TryParse(type, true, out ConvertType result)) {
                    convertTypes.Add(result);
                }
            }
        }
        if (!convertTypes.Any()) {
            convertTypes.Add(ConvertType.Ts);
        }

        AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        _basePath = AbsolutePath(_options.Source);
        Console.Write("Scanning for DTO objects in {0}...  ", _basePath);
        var strs = _options.Files.SelectMany(f => Directory.GetFiles(_basePath, f));
        var assemblies = strs.Select(Load).Where(a => a != null);
        var list = assemblies.SelectMany(GetApiControllers).ToList();
        var types = new HashSet<Type>(list);
        var array = types.ToArray();
        foreach (var t in array) {
            RecursivelySearchModels(t, types);
        }

        Console.ForegroundColor = types.Count > 0 ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.WriteLine("Found {0}", types.Count);
        Console.ResetColor();
        foreach (var convertType in convertTypes) {
            var modelTargetPath = GetDestinationPath(convertType);
            if (Directory.Exists(modelTargetPath)) {
                Directory.Delete(modelTargetPath, true);
            }

            Directory.CreateDirectory(modelTargetPath);

            if (convertType == ConvertType.Kt) {
                Directory.CreateDirectory(Path.Combine(modelTargetPath, "interfaces"));
                EntityGenerator.GenerateKtInterfaces(_options.KtPackageName, modelTargetPath);
                if (_options.KtUseRealmDb ?? false) {
                    Directory.CreateDirectory(Path.Combine(modelTargetPath, "realm"));
                    Directory.CreateDirectory(Path.Combine(modelTargetPath, "helpers"));
                    EntityGenerator.GenerateKtRealmListConverter(_options.KtPackageName, modelTargetPath);
                }
            }

            if (convertType == ConvertType.Swift && (_options.SwiftUseRealmDb ?? false)) {
                Directory.CreateDirectory(Path.Combine(modelTargetPath, "protocols"));
                EntityGenerator.GenerateSwiftProtocols(modelTargetPath);
            }
            EntityGenerator.Generate(modelTargetPath, types, convertType, _options);

            if (convertType == ConvertType.Ts && !(_options.SkipTsFormInterfaces ?? false)) {
                var interfaceTargetPath = GetDestinationPath(convertType, true);
                if (Directory.Exists(interfaceTargetPath)) {
                    Directory.Delete(interfaceTargetPath, true);
                }

                Directory.CreateDirectory(interfaceTargetPath);
                EntityGenerator.Generate(interfaceTargetPath, types, convertType, _options, true);
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Done in {0:N3}s", stopwatch.Elapsed.TotalSeconds);
        Console.ResetColor();
    }

    /// <summary>
    /// Get the absolute path.
    /// </summary>
    /// <param name="relativePath"></param>
    /// <returns>string</returns>
    private string AbsolutePath(string relativePath) {
        return Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(Environment.CurrentDirectory, relativePath);
    }

    /// <summary>
    /// Get the types defined in specified assembly.
    /// </summary>
    /// <param name="a"></param>
    /// <returns>IEnumerable&lt;Type&gt;</returns>
    private IEnumerable<Type> GetApiControllers(Assembly a) {
        var filesName = new List<string>();
        foreach (var file in Directory.EnumerateFiles(AbsolutePath(_options.Models), "*", SearchOption.AllDirectories)) {
            filesName.Add(Path.GetFileNameWithoutExtension(file).ToLower());
        }
        return a.GetTypes().Where(t =>
            filesName.Contains(t.Name.ToLower()) && !(t.Name.ToLower().Contains("sync")) ||
            t.IsAbstract && !(t.IsAbstract && t.IsSealed)).ToList();
    }

    /// <summary>
    /// Get model types.
    /// </summary>
    /// <param name="t"></param>
    /// <returns>IEnumerable&lt;Type&gt;</returns>
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

    /// <summary>
    /// Get an assembly from the specified file.
    /// </summary>
    /// <param name="path"></param>
    /// <returns>Assembly</returns>
    private Assembly Load(string path) {
        Assembly assembly;
        try {
            assembly = Assembly.Load(File.ReadAllBytes(path));
        } catch {
            assembly = null;
        }
        return assembly;
    }

    /// <summary>
    /// Recursively add types to specified models.
    /// </summary>
    /// <param name="model"></param>
    /// <param name="visitedModels"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Get the assembly specified in the name of the arguments.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    /// <returns>Assembly</returns>
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

    /// <summary>
    /// Get the destination path for an form interface or model.
    /// </summary>
    /// <param name="convertType"></param>
    /// <param name="isInterface"></param>
    /// <returns>string</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private string GetDestinationPath(ConvertType convertType, bool isInterface = false) {
        switch (convertType) {
            case ConvertType.Ts:
                return isInterface
                    ? Path.GetFullPath(Path.Combine(AbsolutePath(_options.TsDestination), "generated/form-interfaces"))
                    : Path.GetFullPath(Path.Combine(AbsolutePath(_options.TsDestination), "generated"));
            case ConvertType.Kt:
                return Path.GetFullPath(Path.Combine(AbsolutePath(_options.KtDestination), "generated"));
            case ConvertType.Swift:
                return Path.GetFullPath(Path.Combine(AbsolutePath(_options.SwiftDestination), "Generated"));
            default:
                throw new ArgumentOutOfRangeException(nameof(convertType), convertType, null);
        }
    }
}
