using System.Reflection;

namespace model_generator;

public static class Utils {
    private static readonly List<string> IgnoreAttributes = new() {
        typeof(System.Text.Json.Serialization.JsonIgnoreAttribute).FullName,
        typeof(Newtonsoft.Json.JsonIgnoreAttribute).FullName
    };

    private const string ToSwiftPrefix = "var";

    /// <summary>
    /// Get the information from the properties of the current type.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="skipDayjs"></param>
    /// <param name="isInterface"></param>
    /// <param name="convertType"></param>
    /// <param name="ktUseRealm"></param>
    /// <returns>NameAndType&#x5B;&#93;</returns>
    public static NameAndType[] GetAllPropertiesInType(this Type t, bool skipDayjs, bool isInterface, ConvertType convertType, bool ktUseRealm = false, bool isSwiftRealm = false) {
        return (
            from p in (
                from p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(x => !x.CustomAttributes.Any() ||
                                x.CustomAttributes.All(a =>
                                    !IgnoreAttributes.Any(attr => attr.Equals(a.AttributeType.FullName))
                                )
                    )
                select new NameAndType() {
                    Name = p.Name,
                    Type = convertType switch {
                        ConvertType.Ts => p.PropertyType.ToTypeScriptType(skipDayjs, isInterface),
                        ConvertType.Kt => p.PropertyType.ToKotlinType(ktUseRealm),
                        ConvertType.Swift => p.PropertyType.ToSwiftType(false, isSwiftRealm),
                        _ => throw new NotImplementedException(),
                    }
                }).Distinct()
            orderby p.Name
            select p).ToArray();
    }

    /// <summary>
    /// Get information from the declared properties of the current type.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="convertType"></param>
    /// <param name="skipDayjs"></param>
    /// <param name="isInterface"></param>
    /// <returns>List&lt;NameAndType&gt;</returns>
    public static List<NameAndType> GetDeclaredPropertiesInType(this Type t, ConvertType convertType, bool skipDayjs, bool isInterface, bool isSwiftRealm = false) {
        return (
            from p in (
                from p in t.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                    .Where(x => !x.CustomAttributes.Any() ||
                                x.CustomAttributes.All(a =>
                                    !IgnoreAttributes.Any(attr => attr.Equals(a.AttributeType.FullName))
                                )
                    )
                select new NameAndType() {
                    Name = p.Name,
                    Type = p.PropertyType.ToCustomType(convertType, skipDayjs, isInterface, isSwiftRealm),
                    Prefix = p.PropertyType.ToCustomPrefix(convertType),
                    Submodel = p.PropertyType.ToSubmodel(convertType)
                }).Distinct()
            orderby p.Name
            select p).ToList();
    }

    /// <summary>
    /// Get information from the basic conflicted properties of the current type with default values.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="convertType"></param>
    /// <returns>List&lt;NameAndType&gt;</returns>
    public static List<NameAndType> GetConflictedPropertiesInType(this Type t, ConvertType convertType) {
        return (
            from p in (
                from p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(x => !x.CustomAttributes.Any() ||
                                x.CustomAttributes.All(a =>
                                    !IgnoreAttributes.Any(attr => attr.Equals(a.AttributeType.FullName))
                                )
                    )
                select new NameAndType() {
                    Name = p.Name,
                    Type = p.PropertyType.ToConflictedType(convertType, p.Name)
                }).Distinct()
            orderby p.Name
            select p).ToList();
    }

    /// <summary>
    /// Get the type of property.
    /// </summary>
    /// <param name="pi"></param>
    /// <returns>Type</returns>
    public static Type GetPropertyType(this PropertyInfo pi) {
        if (!pi.PropertyType.IsGenericType) {
            return pi.PropertyType;
        }
        return pi.PropertyType.GetGenericArguments()[0];
    }

    /// <summary>
    /// Determine if the type is not in the System and Newtonsoft namespaces.
    /// </summary>
    /// <param name="t"></param>
    /// <returns>bool</returns>
    public static bool IsModelType(this Type t) {
        if (!t.IsClass || t.Namespace == null || t == typeof(string)) {
            return false;
        }

        if (t.FullName == null) {
            return false;
        }
        return !t.FullName.StartsWith("System.") && !t.FullName.StartsWith("Newtonsoft.");
    }

    /// <summary>
    /// Determine if a type is generic and contains &quot;.Dtos&quot; in the namespace and &quot;`1&quot; in the name.
    /// </summary>
    /// <param name="t"></param>
    /// <returns>bool</returns>
    public static bool IsModelGenericType(this Type t) {
        if (!t.IsClass || t.Namespace == null || t == typeof(string) || !t.IsGenericType || t.FullName != null) {
            return false;
        }
        return t.Namespace.Contains(".Dtos") && t.Name.Contains("`1");
    }

    /// <summary>
    /// Determine if a type is non-abstract or if the type is a model.
    /// </summary>
    /// <param name="t"></param>
    /// <returns>bool</returns>
    public static bool IsModelTypeNoAbstract(this Type t) {
        if (t.IsAbstract) {
            return false;
        }
        return t.IsModelType();
    }

    /// <summary>
    /// Get the generated string by the conflicted converting type.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="convertType"></param>
    /// <param name="propertyName"></param>
    /// <returns>string</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static string ToConflictedType(this Type t, ConvertType convertType, string propertyName) {
        switch (convertType) {
            case ConvertType.Swift:
                return ToConflictedSwiftType(t, false, propertyName);
            default:
                throw new ArgumentOutOfRangeException(nameof(convertType), convertType, null);
        }
    }

    /// <summary>
    /// Get the generated string by the specified converting type.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="convertType"></param>
    /// <param name="skipDayjs"></param>
    /// <param name="isInterface"></param>
    /// <param name="isSwiftRealm"></param>
    /// <returns>string</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static string ToCustomType(this Type t, ConvertType convertType, bool skipDayjs, bool isInterface, bool isSwiftRealm) {
        switch (convertType) {
            case ConvertType.Ts:
                return ToTypeScriptType(t, skipDayjs, isInterface);
            case ConvertType.Swift:
                return ToSwiftType(t, false, isSwiftRealm);
            default:
                throw new ArgumentOutOfRangeException(nameof(convertType), convertType, null);
        }
    }

    private static string ToSwiftType(this Type t, bool listValue = false, bool isSwiftRealm = false) {
        if (t.IsModelType()) {
            if (isSwiftRealm) {
                return listValue ? $"{t.Name}Object" : $": {t.Name}Object?";
            }

            return listValue ? $"{t.Name}" : $": {t.Name}?";
        }

        if (t.Name == "Nullable`1") {
            return t.GetGenericArguments()[0].ToSwiftType();
        }

        if (t == typeof(bool)) {
            return listValue ? "Bool" : ": Bool? = false";
        }

        if (IsSwiftInt(t)) {
            return IntSwiftType(listValue);
        }

        if (t == typeof(uint) || t == typeof(ulong) || t == typeof(long)) {
            return listValue ? "Int64" : ": Int64? = nil";
        }

        if (t == typeof(decimal) || t == typeof(double)) {
            return listValue ? "Double" : ": Double? = nil";
        }

        if (t == typeof(float)) {
            return listValue ? "Float" : ": Float? = nil";
        }

        if (t == typeof(string) || t == typeof(char) || t == typeof(Guid)) {
            return StringSwiftType(listValue);
        }

        if (t == typeof(DateTime)) {
            return StringSwiftType(listValue);
        }

        if (IsSwiftList(t)) {
            if (isSwiftRealm) {
                return string.Concat(": List<", t.GetGenericArguments()[0].ToSwiftType(true, isSwiftRealm), ">");
            }

            return string.Concat(": [", t.GetGenericArguments()[0].ToSwiftType(true), "] = []");
        }

        if (t.Name == "IFormFile" || t.Name == "FormFile") {
            return StringSwiftType(listValue);
        }

        if (isSwiftRealm) {
            return ": AnyRealmValue";
        }

        return listValue ? "Any" : ": Any? = nil";
    }

    private static string ToConflictedSwiftType(this Type t, bool isListValue = false, string propertyName = "") {
        if (t.IsModelType()) {
            var value = isListValue ? "$0" : $"object.{GetLower(propertyName)}";
            return $"{t.Name}.init({value})";
        }

        if (t.Name == "Nullable`1") {
            return t.GetGenericArguments()[0].ToConflictedSwiftType();
        }

        if (IsSwiftList(t)) {
            return string.Concat($"object.{GetLower(propertyName)}.map {{ ", t.GetGenericArguments()[0].ToConflictedSwiftType(true), " }");
        }

        if (IsNotConflictedSwiftType(t)) {
            if (isListValue) {
                var type = ToSwiftType(t, true);
                return $"{type}.init($0)";
            }
            return null;
        }

        return $"Any(object.{GetLower(t.Name)})";
    }

    private static bool IsSwiftInt(Type t) {
        return t == typeof(ushort) || t == typeof(short) || t == typeof(byte) || t == typeof(sbyte) || t == typeof(int) || t.IsEnum;
    }

    private static bool IsSwiftList(Type t) {
        return t.Name == "List`1" || t.Name == "IList`1" || t.IsGenericType && typeof(IEnumerable<object>).IsAssignableFrom(t);
    }

    private static string IntSwiftType(bool listValue = false) {
        return listValue ? "Int" : ": Int? = nil";
    }

    private static string StringSwiftType(bool listValue = false) {
        return listValue ? "String" : ": String? = nil";
    }

    private static bool IsNotConflictedSwiftType(Type t) {
        return IsSwiftInt(t) || t.Name == "IFormFile" || t.Name == "FormFile" ||
            t == typeof(bool) || t == typeof(decimal) || t == typeof(double) ||
            t == typeof(float) || t == typeof(string) || t == typeof(char) ||
            t == typeof(uint) || t == typeof(ulong) || t == typeof(long) ||
            t == typeof(Guid) || t == typeof(DateTime);
    }

    /// <summary>
    /// Get prefix by the specified converting type.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="convertType"></param>
    /// <returns>string</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static string ToCustomPrefix(this Type t, ConvertType convertType) {
        switch (convertType) {
            case ConvertType.Ts:
                return null;
            case ConvertType.Swift:
                return ToSwiftPrefix;
            default:
                throw new ArgumentOutOfRangeException(nameof(convertType), convertType, null);
        }
    }

    /// <summary>
    /// Get submodel by the specified converting type.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="convertType"></param>
    /// <returns>string</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static string ToSubmodel(this Type t, ConvertType convertType) {
        switch (convertType) {
            case ConvertType.Ts:
                return null;
            case ConvertType.Swift:
                return ToSwiftSubmodel(t);
            default:
                throw new ArgumentOutOfRangeException(nameof(convertType), convertType, null);
        }
    }

    /// <summary>
    /// </summary>
    /// <param name="t"></param>
    /// <returns></returns>
    private static string ToSwiftSubmodel(this Type t) {
        if (t.IsModelType()) {
            return t.Name;
        }

        if (t.Name == "List`1" || t.Name == "IList`1" || t.IsGenericType && typeof(IEnumerable<object>).IsAssignableFrom(t)) {
            return t.GetGenericArguments()[0].ToSwiftSubmodel();
        }

        return null;
    }

    /// <summary>
    /// Get a string containing the typescript type.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="skipDayjs"></param>
    /// <param name="isInterface"></param>
    /// <returns>string</returns>
    public static string ToTypeScriptType(this Type t, bool skipDayjs = false, bool isInterface = false) {
        if (t.IsModelType()) {
            return isInterface ? $"FormGroup<{t.Name.Replace("Model", "FormInterface")}>" : t.Name;
        }
        if (t == typeof(bool)) {
            return isInterface ? "FormControl<boolean>" : "boolean";
        }
        if (t == typeof(byte) || t == typeof(sbyte) || t == typeof(ushort) || t == typeof(short) || t == typeof(uint) || t == typeof(int) || t == typeof(ulong) || t == typeof(long) || t == typeof(float) || t == typeof(double) || t == typeof(decimal)) {
            return isInterface ? "FormControl<number>" : "number";
        }
        if (t == typeof(string) || t == typeof(char) || t == typeof(Guid)) {
            return isInterface ? "FormControl<string>" : "string";
        }
        if (t.Name == "List`1" || t.IsGenericType && typeof(IEnumerable<object>).IsAssignableFrom(t)) {
            var type = t.GetGenericArguments()[0].ToTypeScriptType(skipDayjs, isInterface);
            return isInterface ? $"FormArray<{type.Replace("Model", "FormInterface")}>" : string.Concat(type, "[]");
        }
        if (t.Name == "Nullable`1") {
            return t.GetGenericArguments()[0].ToTypeScriptType(skipDayjs, isInterface);
        }
        if (t == typeof(DateTime)) {
            var type = skipDayjs ? "number" : "dayjs.Dayjs";
            return isInterface ? $"FormControl<{type}>" : type;
        }
        if (t.IsGenericParameter) {
            return "T";
        }
        if (t.IsModelGenericType()) {
            var type = t.Name.Replace("`1", "<T>");
            return isInterface ? $"FormGroup<{type}>" : type;
        }
        return "any";
    }

    /// <summary>
    /// Get a string containing the kotlin type.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="useRealm"></param>
    /// <param name="evaluatingListType"></param>
    /// <returns>string</returns>
    public static string ToKotlinType(this Type t, bool useRealm, bool evaluatingListType = false) {
        if (t.IsModelType()) {
            var type = useRealm ? $"{t.Name}Realm" : t.Name;
            return evaluatingListType ? $"{type}" : $"{type}? = null";
        }
        if (t.IsGenericParameter) {
            return evaluatingListType ? "T" : "T? = null";
        }
        if (t.IsModelGenericType()) {
            var type = t.Name.Replace("`1", "<T>");
            return type;
        }
        if (t.Name == "List`1" || t.IsGenericType && typeof(IEnumerable<object>).IsAssignableFrom(t)) {
            var type = t.GetGenericArguments()[0].ToKotlinType(useRealm, true);
            var defaultValue = useRealm ? " = RealmList()" : " = listOf()";
            var evaluatedType = useRealm ? $"RealmList<{type}>" : $"List<{type}>";
            return evaluatingListType ? evaluatedType : $"{evaluatedType}{defaultValue}";
        }
        if (t.Name == "Nullable`1") {
            return $"{t.GetGenericArguments()[0].ToKotlinType(useRealm, true)}? = null";
        }
        if (t == typeof(Guid)) {
            return evaluatingListType ? "String" : "String = \"\"";
        }

        TypeCode typeCode = Type.GetTypeCode(t);

        return typeCode switch {
            TypeCode.Boolean => evaluatingListType ? "Boolean" : "Boolean = false",
            TypeCode.Char => evaluatingListType ? "String" : "String = \"\"",
            TypeCode.SByte => evaluatingListType ? "Byte" : "Byte = 0",
            TypeCode.Byte => evaluatingListType ? "UByte" : "UByte = 0u",
            TypeCode.Int16 => evaluatingListType ? "Short" : "Short = 0",
            TypeCode.Int32 => evaluatingListType ? "Int" : "Int = 0",
            TypeCode.UInt16 => evaluatingListType ? "UShort" : "UShort = 0u",
            TypeCode.UInt32 => evaluatingListType ? "UInt" : "UInt = 0u",
            TypeCode.Int64 => evaluatingListType ? "Long" : "Long = 0",
            TypeCode.UInt64 => evaluatingListType ? "ULong" : "ULong = 0u",
            TypeCode.Single => evaluatingListType ? "Float" : "Float = 0F",
            TypeCode.Decimal or TypeCode.Double => evaluatingListType ? "Double" : "Double = 0.0",
            TypeCode.DateTime => evaluatingListType ? "Int" : "Int = 0",
            TypeCode.String => evaluatingListType ? "String" : "String = \"\"",
            _ => evaluatingListType ? "Any" : "Any? = null",
        };
    }

    /// <summary>
    /// Overwrite or create a file at the given path if the file is not found or the content is not equal.
    /// </summary>
    /// <param name="text"></param>
    /// <param name="path"></param>
    /// <returns>bool</returns>
    public static bool WriteIfChanged(string text, string path) {
        if (File.Exists(path) && string.Equals(text, File.ReadAllText(path))) {
            return false;
        }
        File.WriteAllText(path, text);
        return true;
    }


    /// <summary>
    /// Get the specified string with the first character converted to lowercase.
    /// </summary>
    /// <param name="str"></param>
    /// <returns>string</returns>
    public static string GetLower(string str) {
        return char.ToLower(str[0]) + str.Substring(1);
    }
}
