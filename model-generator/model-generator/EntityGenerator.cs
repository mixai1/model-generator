using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace model_generator;

public static class EntityGenerator {
    static readonly string[] KtBasicTypes = { "Int", "UInt", "String", "Boolean", "Long", "ULong", "Byte", "UByte", "Short", "UShort", "Float", "Double", "Any", "Double" };

    /// <summary>
    /// Get the typescript content for the model.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="convertType"></param>
    /// <param name="skipDayjs"></param>
    /// <returns>string</returns>
    private static string CreateModelTsString(Type t, ConvertType convertType, bool skipDayjs) {
        var className = t.Name.Replace("`1", "<T>").Replace("`2", "<T, U>");
        string str = GetStr(t, className);
        string baseTypeName = GetBaseTypeName(t);
        string str1 = baseTypeName.Replace("`1", "").Replace("`2", "");
        string str3 = baseTypeName;
        string str4 = baseTypeName.Replace("`1", "<T>").Replace("`2", "<T, U>");
        var import = new List<string>();
        if (t.BaseType != null && t.BaseType.IsModelType() && t.BaseType.GenericTypeArguments.Length > 0) {
            str3 = ConcatGenericArguments(t.BaseType, skipDayjs, false);
            foreach (var parameter in t.BaseType.GenericTypeArguments.Where(x => x.IsModelType())) {
                import.Add(parameter.Name);
            }
        }

        NameAndType[] allPropertiesInType = t.GetAllPropertiesInType(skipDayjs, false, convertType);
        var stringBuilder = new StringBuilder();
        BuildDayjsProperties(allPropertiesInType, stringBuilder);
        if (!string.IsNullOrWhiteSpace(str1)) {
            import.Add(str1);
        }

        import.AddRange(FindTypesToImport(t));
        for (int i = 0; i < import.Count; i++) {
            string str2 = import[i];
            stringBuilder.AppendLine(
                $"import {{ {str2} }} from './{GetFileName(str2).Replace("-Model", ".model").ToLower()}';");
            if (i == import.Count - 1) {
                stringBuilder.AppendLine();
            }
        }

        stringBuilder.Append(str);
        if (!string.IsNullOrWhiteSpace(str1)) {
            stringBuilder.Append(string.Concat(" extends ", t.IsAbstract ? str4 : str3));
        }
        stringBuilder.AppendLine(" {");
        var declaredPropertiesInType = t.GetDeclaredPropertiesInType(convertType, skipDayjs, false);
        foreach (NameAndType nameAndType in declaredPropertiesInType) {
            stringBuilder.AppendLine(string.Format(TabToSpace(1) + "public {0}: {1};", Utils.GetLower(nameAndType.Name), nameAndType.Type));
        }
        if (!t.IsAbstract) {
            stringBuilder.AppendLine();
            stringBuilder.AppendLine(TabToSpace(1) + "public constructor(");
            stringBuilder.AppendLine(string.Format(TabToSpace(2) + "fields?: Partial<{0}>) {{", className));
            stringBuilder.AppendLine();
            if (!string.IsNullOrWhiteSpace(str1)) {
                stringBuilder.AppendLine(TabToSpace(2) + "super(fields);");
            }
            stringBuilder.AppendLine(TabToSpace(2) + "if (fields) {");
            NameAndType[] modelPropertiesInType = GetModelPropertiesInType(t, skipDayjs, false);
            stringBuilder.AppendLine(string.Join("\n",
                from prop in modelPropertiesInType
                from decProp in declaredPropertiesInType
                where prop.Name == decProp.Name
                select string.Format(TabToSpace(3) + "if (fields.{0}) {{ fields.{0} = new {1}(fields.{0}); }}", Utils.GetLower(prop.Name), prop.Type)));
            if (allPropertiesInType.Any(x => x.Type == "dayjs.Dayjs")) {
                stringBuilder.AppendLine(TabToSpace(3) + "dayjs.extend(utc);");
            }
            stringBuilder.AppendLine(string.Join("\n",
                from x in allPropertiesInType
                where x.Type == "dayjs.Dayjs"
                select x into prop
                select string.Format(TabToSpace(3) + "if (fields.{0}) {{ fields.{0} = dayjs.utc(fields.{0}); }}", Utils.GetLower(prop.Name))));
            stringBuilder.AppendLine(TabToSpace(3) + "Object.assign(this, fields);");
            stringBuilder.AppendLine(TabToSpace(2) + "}");
            stringBuilder.AppendLine(TabToSpace(1) + "}");
        }
        if (t.IsAbstract) {
            stringBuilder.AppendLine();
            stringBuilder.AppendLine(TabToSpace(1) + "public constructor(");
            stringBuilder.AppendLine(string.Format(TabToSpace(2) + "fields?: Partial<{0}>) {{", t.Name.Replace("`1", "<T>").Replace("`2", "<T, U>")));
            stringBuilder.AppendLine();
            if (!string.IsNullOrWhiteSpace(str1)) {
                stringBuilder.AppendLine(TabToSpace(2) + "super(fields);");
            }
            stringBuilder.AppendLine(TabToSpace(2) + "if (fields) {");
            NameAndType[] modelPropertiesInType = GetModelPropertiesInType(t, skipDayjs, false);
            stringBuilder.AppendLine(string.Join("",
                from prop in modelPropertiesInType
                select string.Format(TabToSpace(3) + "if (fields.{0}) {{ fields.{0} = new {1}(fields.{0}); }}", Utils.GetLower(prop.Name), prop.Type)));
            stringBuilder.AppendLine(TabToSpace(3) + "Object.assign(this, fields);");
            stringBuilder.AppendLine(TabToSpace(2) + "}");
            stringBuilder.AppendLine(TabToSpace(1) + "}");
        }
        stringBuilder.AppendLine("}");
        return stringBuilder.ToString();
    }

    private static string GetStr(Type t, string className) {
        return string.Concat((t.IsAbstract ? "export abstract class " : "export class "), className);
    }

    private static string GetBaseTypeName(Type t) {
        return t.BaseType == null || !t.BaseType.IsModelType() ? "" : t.BaseType.Name;
    }

    private static void BuildDayjsProperties(NameAndType[] allPropertiesInType, StringBuilder stringBuilder) {
        if (allPropertiesInType.Any(p => {
            if (p.Type == "dayjs.Dayjs") {
                return true;
            }
            return p.Type == "dayjs.Dayjs?";
        })) {
            stringBuilder.AppendLine("import * as dayjs from 'dayjs';");
            stringBuilder.AppendLine("import * as utc from 'dayjs/plugin/utc';");
            stringBuilder.AppendLine();
        }
    }

    /// <summary>
    /// Get the kotlin content for the model.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="convertType"></param>
    /// <param name="useRealmDb"></param>
    /// <param name="annotateAsSerializable"></param>
    /// <param name="packageName"></param>
    /// <returns>string</returns>
    private static string CreateModelKtString(Type t, ConvertType convertType, bool useRealmDb, bool annotateAsSerializable, string packageName) {
        var allPropertiesInType = t.GetAllPropertiesInType(false, false, convertType);
        var iidType = GetIidType(t, false);
        StringBuilder result = new();

        List<string> imports = new();
        if (annotateAsSerializable) {
            imports.Add("import kotlinx.serialization.Serializable");
        }
        if (iidType.Length > 0) {
            imports.Add($"import {packageName}.interfaces.IId");
        }
        if (useRealmDb) {
            imports.Add($"import {packageName}.realm.{t.Name}Realm");
            imports.Add($"import {packageName}.interfaces.RealmMappable");
            if (allPropertiesInType.Select(x => x.Type).Any(x => x.Contains("List<"))) {
                imports.Add($"import {packageName}.helpers.toRealmList");
            }
        }

        List<string> interfaces = new();
        if (iidType.Length > 0) {
            interfaces.Add($"IId<{iidType}>");
        }
        if (useRealmDb) {
            interfaces.Add($"RealmMappable<{t.Name}Realm>");
        }

        result.AppendLine($"package {packageName}");
        result.AppendLine();

        if (imports.Any()) {
            foreach (var import in imports.OrderBy(x => x).ToList()) {
                result.AppendLine(import);
            }
            result.AppendLine();
        }

        if (annotateAsSerializable) {
            result.AppendLine("@Serializable");
        }

        result.AppendLine($"data class {t.Name}(");
        foreach (var property in allPropertiesInType) {
            result.Append($"{TabToSpace(1)}");
            if (property.Name.ToLower().Equals("id") && iidType.Length > 0) {
                result.Append("override ");
            }
            result.AppendLine($"val {Utils.GetLower(property.Name)}: {property.Type},");
        }
        result.Append(')');

        if (interfaces.Count > 0) {
            result.Append(" : ");
            result.AppendJoin(", ", interfaces);
        }
        if (useRealmDb) {
            result.AppendLine(" {");
            result.Append(GenerateRealmModelMap(allPropertiesInType, t));
            result.AppendLine("}");
        }
        result.AppendLine();
        return result.ToString();
    }

    /// <summary>
    /// Get the kotlin content for extension method mapping to realm model.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="properties"></param>
    /// <returns>string</returns>
    private static string GenerateRealmModelMap(NameAndType[] properties, Type type) {
        var result = new StringBuilder();

        result.AppendLine($"{TabToSpace(1)}override fun toRealmModel(): {type.Name}Realm {{");
        result.AppendLine($"{TabToSpace(2)}return {type.Name}Realm().apply {{");
        foreach (var property in properties) {
            result.Append($"{TabToSpace(3)}{Utils.GetLower(property.Name)} = this@{type.Name}.{Utils.GetLower(property.Name)}");
            if (property.Type.Contains("List<")) {
                if (IsCustomTypeList(property.Type)) {
                    result.Append(".map { it.toRealmModel() }");
                }
                result.Append(".toRealmList()");
            }
            result.Append('\n');
        }
        result.AppendLine($"{TabToSpace(2)}}}");
        result.AppendLine($"{TabToSpace(1)}}}");

        return result.ToString();
    }

    /// <summary>
    /// Get the kotlin content for the realm model.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="convertType"></param>
    /// <param name="packageName"></param>
    /// <returns>string</returns>
    private static string CreateRealmModelKtString(Type t, ConvertType convertType, string packageName) {
        var allPropertiesInType = t.GetAllPropertiesInType(false, false, convertType, true);
        var iidType = GetIidType(t, false);
        StringBuilder result = new();
        List<string> imports = new();
        if (allPropertiesInType.Select(x => x.Type).Any(x => x.Contains("RealmList<"))) {
            imports.Add("import io.realm.RealmList");
        }
        if (iidType.Length > 0) {
            imports.Add($"import {packageName}.interfaces.IId");
        }
        imports.Add("import io.realm.RealmObject");
        imports.Add("import io.realm.annotations.PrimaryKey");
        imports.Add("import org.bson.types.ObjectId");
        imports.Add($"import {packageName}.interfaces.DtoMappable");
        imports.Add($"import {packageName}.{t.Name}");

        List<string> interfaces = new();
        if (iidType.Length > 0) {
            interfaces.Add($"IId<{iidType}>");
        }
        interfaces.Add("RealmObject()");
        interfaces.Add($"DtoMappable<{t.Name}>");

        result.AppendLine($"package {packageName}.realm");
        result.AppendLine();

        foreach (var import in imports.OrderBy(x => x).ToList()) {
            result.AppendLine(import);
        }
        result.AppendLine();

        result.Append($"open class {t.Name}Realm : ");
        result.AppendJoin(", ", interfaces);
        result.AppendLine(" {");
        if (!(iidType.Length > 0)) {
            result.AppendLine($"{TabToSpace(1)}@PrimaryKey var realmId: ObjectId = ObjectId()");
        }

        foreach (var property in allPropertiesInType) {
            result.Append($"{TabToSpace(1)}");
            if (property.Name.ToLower().Equals("id") && iidType.Length > 0) {
                result.Append("@PrimaryKey override ");
            }
            result.AppendLine($"var {Utils.GetLower(property.Name)}: {property.Type}");
        }
        result.AppendLine();
        result.AppendLine(GenerateDataModelMap(allPropertiesInType, t));

        result.AppendLine("}");
        result.AppendLine();

        return result.ToString();
    }

    /// <summary>
    /// Get the kotlin content for extension method mapping to data model.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="properties"></param>
    /// <returns>string</returns>
    private static string GenerateDataModelMap(NameAndType[] properties, Type type) {
        var result = new StringBuilder();

        result.AppendLine($"{TabToSpace(1)}override fun toDataModel(): {type.Name} {{");
        result.AppendLine($"{TabToSpace(2)}return {type.Name}(");
        foreach (var property in properties) {
            result.Append($"{TabToSpace(3)}{Utils.GetLower(property.Name)} = this.{Utils.GetLower(property.Name)}");
            if (property.Type.Contains("RealmList<") && IsCustomTypeList(property.Type)) {
                result.Append(".map { it.toDataModel() }");
            }
            result.Append(",\n");
        }
        result.AppendLine($"{TabToSpace(2)})");
        result.Append($"{TabToSpace(1)}}}");

        return result.ToString();
    }

    private static bool IsCustomTypeList(string listDefinition) {
        Regex regex = new(@"(?<=\<).+?(?=\>)");
        var listType = regex.Matches(listDefinition).FirstOrDefault()?.Value;
        return listType != null && !KtBasicTypes.Any(x => x == listType);
    }

    private static string GetIidType(Type t, bool isRealmModel) {
        var iid = t.GetInterfaces().FirstOrDefault(x => x.Name.ToLower().Equals("iid`1"));
        var iidType = iid?.GetGenericArguments().FirstOrDefault();
        return iidType != null ? iidType.ToKotlinType(isRealmModel, true) : string.Empty;
    }

    private static bool HasIidType(Type t) {
        var iid = t.GetInterfaces().FirstOrDefault(x => x.Name.ToLower().Equals("iid`1"));
        var iidType = iid?.GetGenericArguments().FirstOrDefault();
        return iidType != null;
    }

    /// <summary>
    /// Get the kotlin content for the realm list converter helper extention method.
    /// </summary>
    /// <param name="packageName"></param>
    /// <param name="targetPath"></param>
    /// <returns>string</returns>
    internal static void GenerateKtRealmListConverter(string packageName, string targetPath) {
        StringBuilder methodString = new();
        methodString.AppendLine($"package {packageName}.helpers");
        methodString.AppendLine();
        methodString.AppendLine("import io.realm.RealmList");
        methodString.AppendLine();
        methodString.AppendLine("fun <T> Collection<T>.toRealmList(): RealmList<T> {");
        methodString.AppendLine($"{TabToSpace(1)}val result = RealmList<T>()");
        methodString.AppendLine($"{TabToSpace(1)}for (item in this) {{");
        methodString.AppendLine($"{TabToSpace(2)}result.add(item)");
        methodString.AppendLine($"{TabToSpace(1)}}}");
        methodString.AppendLine($"{TabToSpace(1)}return result");
        methodString.AppendLine("}");

        var result = methodString.ToString();

        Utils.WriteIfChanged(result, Path.Combine(targetPath, "helpers", "RealmListConverter.kt"));
    }

    /// <summary>
    /// Get the kotlin content for the realm mapper interfaces.
    /// </summary>
    /// <param name="packageName"></param>
    /// <param name="targetPath"></param>
    /// <returns>string</returns>
    internal static void GenerateKtInterfaces(string packageName, string targetPath) {
        StringBuilder realmMapInterface = new();
        realmMapInterface.AppendLine($"package {packageName}.interfaces");
        realmMapInterface.AppendLine();
        realmMapInterface.AppendLine("interface RealmMappable<out T> {");
        realmMapInterface.AppendLine($"{TabToSpace(1)}fun toRealmModel(): T");
        realmMapInterface.AppendLine("}");

        Utils.WriteIfChanged(realmMapInterface.ToString(), Path.Combine(targetPath, "interfaces", "RealmMappable.kt"));

        StringBuilder dataMapInterface = new();
        dataMapInterface.AppendLine($"package {packageName}.interfaces");
        dataMapInterface.AppendLine();
        dataMapInterface.AppendLine("interface DtoMappable<out T> {");
        dataMapInterface.AppendLine($"{TabToSpace(1)}fun toDataModel(): T");
        dataMapInterface.AppendLine("}");

        Utils.WriteIfChanged(dataMapInterface.ToString(), Path.Combine(targetPath, "interfaces", "DtoMappable.kt"));

        StringBuilder idInterface = new();
        idInterface.AppendLine($"package {packageName}.interfaces");
        idInterface.AppendLine();
        idInterface.AppendLine("interface IId<T> {");
        idInterface.AppendLine($"{TabToSpace(1)}val id: T");
        idInterface.AppendLine("}");

        Utils.WriteIfChanged(idInterface.ToString(), Path.Combine(targetPath, "interfaces", "IId.kt"));
    }

    /// <summary>
    /// Get the swift content for the protocols.
    /// </summary>
    /// <param name="packageName"></param>
    /// <param name="targetPath"></param>
    /// <returns>string</returns>
    internal static void GenerateSwiftProtocols(string targetPath) {
        var protocolBuilder = new StringBuilder();

        protocolBuilder.AppendLine("import RealmSwift");
        protocolBuilder.AppendLine();
        protocolBuilder.AppendLine("protocol RealmModel: Codable {");
        protocolBuilder.AppendLine(TabToSpace(1) + "func toObject() -> Object");
        protocolBuilder.AppendLine(TabToSpace(1) + "init(_ obj: Object?)");
        protocolBuilder.AppendLine("}");

        protocolBuilder.AppendLine();
        protocolBuilder.AppendLine("extension RealmModel {");
        protocolBuilder.AppendLine(TabToSpace(1) + "func asDictionary() -> [String: Any] {");
        protocolBuilder.AppendLine(TabToSpace(2) + "guard let data = try? JSONEncoder().encode(self) else {");
        protocolBuilder.AppendLine(TabToSpace(3) + "return [:]");
        protocolBuilder.AppendLine(TabToSpace(2) + "}");
        protocolBuilder.AppendLine(TabToSpace(2) + "guard let dictionary = try? JSONSerialization.jsonObject(with: data, options: .allowFragments) as? [String: Any] else {");
        protocolBuilder.AppendLine(TabToSpace(3) + "return [:]");
        protocolBuilder.AppendLine(TabToSpace(2) + "}");
        protocolBuilder.AppendLine(TabToSpace(2) + "return dictionary");
        protocolBuilder.AppendLine(TabToSpace(1) + "}");
        protocolBuilder.AppendLine("}");

        Utils.WriteIfChanged(protocolBuilder.ToString(), Path.Combine(targetPath, "protocols", "RealmModel.swift"));
    }

    /// <summary>
    /// Get the swift content for the model.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="convertType"></param>
    /// <param name="useRealmDB"></param>
    /// <returns>string</returns>
    private static string CreateModelSwiftString(Type t, ConvertType convertType, bool useRealmDB) {
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine(GetSwiftImports(useRealmDB));
        stringBuilder.AppendLine(GetBaseSwiftModel(t, convertType, useRealmDB));

        if (useRealmDB) {
            stringBuilder.AppendLine(GetRealmSwiftModel(t, convertType));
        }

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Insert swift imports
    /// </summary>
    /// <param name="useRealmDb"></param>
    /// <returns>string</returns>
    private static string GetSwiftImports(bool useRealmDb) {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("import EVReflection");

        if (useRealmDb) {
            stringBuilder.AppendLine("import Foundation");
            stringBuilder.AppendLine("import RealmSwift");
        }

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Insert base swift model
    /// </summary>
    /// <param name="t"></param>
    /// <param name="convertType"></param>
    /// <param name="useRealmDB"></param>
    /// <returns>string</returns>
    private static string GetBaseSwiftModel(Type t, ConvertType convertType, bool useRealmDB) {
        var stringBuilder = new StringBuilder();

        string protocol = useRealmDB ? "RealmModel" : "Codable";
        stringBuilder.AppendLine($"class {t.Name}: {protocol} {{");

        var declaredPropertiesInType = t.GetAllPropertiesInType(false, false, convertType, false, false);

        foreach (var nameAndType in declaredPropertiesInType) {
            stringBuilder.AppendLine(TabToSpace(1) + $"var {Utils.GetLower(nameAndType.Name)}{nameAndType.Type}");
        }

        if (useRealmDB) {
            stringBuilder.AppendLine(GetToObjectFunction(t));
            stringBuilder.Append(GetInitFunction(t, convertType));
        }

        stringBuilder.AppendLine("}");

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Insert toObject function
    /// </summary>
    /// <param name="t"></param>
    /// <returns>string</returns>
    private static string GetToObjectFunction(Type t) {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine();
        stringBuilder.AppendLine(TabToSpace(1) + "func toObject() -> RealmSwift.Object {");
        stringBuilder.AppendLine(TabToSpace(2) + "var dictionary = self.asDictionary()");

        if (!HasIidType(t)) {
            stringBuilder.AppendLine(TabToSpace(2) + "dictionary[\"realmId\"] = ObjectId()");
        }

        stringBuilder.AppendLine(TabToSpace(2) + $"return {t.Name}Object(value: dictionary)");
        stringBuilder.AppendLine(TabToSpace(1) + "}");

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Insert init from object function
    /// </summary>
    /// <param name="t"></param>
    /// <param name="convertType"></param>
    /// <returns>string</returns>
    private static string GetInitFunction(Type t, ConvertType convertType) {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine(TabToSpace(1) + "required init(_ obj: RealmSwift.Object?) {");
        stringBuilder.AppendLine(TabToSpace(2) + $"if let object = obj as? {t.Name}Object {{");

        var nameAndTypes = t.GetConflictedPropertiesInType(convertType);
        foreach (var nameAndType in nameAndTypes) {
            string propertyName = Utils.GetLower(nameAndType.Name);
            string propertyValue = nameAndType.Type != null ? nameAndType.Type : $"object.{propertyName}";
            stringBuilder.AppendLine(TabToSpace(3) + $"self.{propertyName} = {propertyValue}");
        }

        stringBuilder.AppendLine(TabToSpace(2) + "}");
        stringBuilder.AppendLine(TabToSpace(1) + "}");

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Insert realm swift model
    /// </summary>
    /// <param name="t"></param>
    /// <param name="convertType"></param>
    /// <returns>string</returns>
    private static string GetRealmSwiftModel(Type t, ConvertType convertType) {
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine($"class {t.Name}Object: Object {{");

        var hasIid = HasIidType(t);
        if (!hasIid) {
            stringBuilder.AppendLine(TabToSpace(1) + "@Persisted(primaryKey: true) var realmId: ObjectId");
        }

        var declaredPropertiesInType = t.GetAllPropertiesInType(false, false, convertType, false, true);
        foreach (var nameAndType in declaredPropertiesInType) {
            string primaryKeyMark = hasIid && nameAndType.Name == "Id" ? "(primaryKey: true)" : "";
            stringBuilder.AppendLine(TabToSpace(1) + $"@Persisted{primaryKeyMark} var {Utils.GetLower(nameAndType.Name)}{nameAndType.Type}");
        }

        stringBuilder.AppendLine("}");

        return stringBuilder.ToString();
    }

    /// <summary>
    /// Get the typescript content for the form interface.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="convertType"></param>
    /// <param name="skipDayjs"></param>
    /// <returns>string</returns>
    private static string CreateInterfaceTsString(Type t, ConvertType convertType, bool skipDayjs) {
        var declaredPropertiesInType = t.GetDeclaredPropertiesInType(convertType, skipDayjs, true);
        var isHavePropsInType = declaredPropertiesInType.Any();
        var interfaceName = t.Name
            .Replace("`1", isHavePropsInType ? "<T extends AbstractControl>" : "")
            .Replace("`2", isHavePropsInType ? "<T extends AbstractControl, U extends AbstractControl>" : "")
            .Replace("Model", "FormInterface");

        string str = string.Concat("export interface ", interfaceName);
        string baseTypeName = t.BaseType == null || !t.BaseType.IsModelType() ? "" : t.BaseType.Name;
        baseTypeName = baseTypeName.Replace("Model", "FormInterface");
        string str1 = baseTypeName.Replace("`1", "").Replace("`2", "");
        string str3 = baseTypeName;
        if (t.BaseType != null && t.BaseType.IsModelType() && t.BaseType.GenericTypeArguments.Length > 0) {
            str3 = ConcatGenericArguments(t.BaseType, skipDayjs, true).Replace("Model", "FormInterface");
        }
        StringBuilder stringBuilder = new();

        List<string> formTypes = new();
        if (interfaceName.Contains("AbstractControl")) {
            formTypes.Add("AbstractControl");
        }

        if (str3.Contains("FormArray") || declaredPropertiesInType.Any(p => p.Type.Contains("FormArray"))) {
            formTypes.Add("FormArray");
        }

        if (str3.Contains("FormControl") || declaredPropertiesInType.Any(p => p.Type.Contains("FormControl"))) {
            formTypes.Add("FormControl");
        }

        if (str3.Contains("FormGroup") || declaredPropertiesInType.Any(p => p.Type.Contains("FormGroup"))) {
            formTypes.Add("FormGroup");
        }

        if (formTypes.Any()) {
            stringBuilder.AppendLine($"import {{ {string.Join(", ", formTypes)} }} from '@angular/forms';");
        }

        if (declaredPropertiesInType.Any(p => p.Type.Contains("dayjs.Dayjs") || p.Type.Contains("dayjs.Dayjs?"))) {
            stringBuilder.AppendLine("import * as dayjs from 'dayjs';");
        }

        if (stringBuilder.Length > 0) {
            stringBuilder.AppendLine();
        }

        List<string> import = new();
        if (!string.IsNullOrWhiteSpace(str1)) {
            import.Add(str1);
        }
        import.AddRange(FindTypesToImport(t).Select(x => x.Replace("Model", "FormInterface")));

        var sorted = import.Distinct().OrderBy(x => x).ToList();
        for (int i = 0; i < sorted.Count; i++) {
            string str2 = sorted[i];
            stringBuilder.AppendLine(
                $"import {{ {str2} }} from './{GetFileName(str2).Replace("-Interface", ".interface").ToLower()}';");
            if (i == sorted.Count - 1) {
                stringBuilder.AppendLine();
            }
        }

        stringBuilder.Append(str);
        if (!string.IsNullOrWhiteSpace(str1)) {
            stringBuilder.Append(string.Concat(" extends ", str3));
        }
        stringBuilder.AppendLine(" {");
        foreach (NameAndType nameAndType in declaredPropertiesInType) {
            stringBuilder.AppendLine(string.Format(TabToSpace(1) + "{0}: {1};", Utils.GetLower(nameAndType.Name), nameAndType.Type));
        }
        stringBuilder.AppendLine("}");
        return stringBuilder.ToString();
    }

    /// <summary>
    /// Get the type names for the imports.
    /// </summary>
    /// <param name="parentType"></param>
    /// <returns>string&#x5B;&#93;</returns>
    private static string[] FindTypesToImport(Type parentType) {
        return (
            from p in (
                from p in parentType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                where p.DeclaringType == parentType || p.DeclaringType == parentType.DeclaringType
                select p.GetPropertyType() into t
                where t.IsModelType() || t.IsModelGenericType()
                select t into x
                where x != parentType
                select x).Distinct()
            orderby p.Name
            select p.Name.Replace("`1", string.Empty)).ToArray();
    }

    /// <summary>
    /// Get the type names of the generated files and prints the result to the console.
    /// </summary>
    /// <param name="targetPath"></param>
    /// <param name="allModels"></param>
    /// <param name="convertType"></param>
    /// <param name="options"></param>
    /// <param name="isInterface"></param>
    /// <returns>List&lt;string&gt;</returns>
    internal static List<string> Generate(string targetPath, HashSet<Type> allModels, ConvertType convertType, GeneratorOptions options, bool isInterface = false) {
        var generatedResult = new List<string>();
        var types = (
            from m in allModels
            where convertType != ConvertType.Ts ? !m.IsAbstract : allModels.Any()
            orderby m.Name
            select m).ToList();

        foreach (var type in types) {
            string name;
            if (convertType == ConvertType.Ts) {
                name = isInterface
                    ? GetFileName(type.Name).Replace("-Model", "-form.interface").ToLower()
                    : GetFileName(type.Name).Replace("-Model", ".model").ToLower();
            } else {
                name = type.Name;
            }
            Utils.WriteIfChanged(GetFileString(convertType, type, options, isInterface, false), Path.Combine(targetPath, string.Concat(name, GetFileType(convertType))).Replace("`1", "").Replace("`2", ""));
            if (convertType == ConvertType.Kt && (options.KtUseRealmDb ?? false)) {
                Utils.WriteIfChanged(GetFileString(convertType, type, options, isInterface, true), Path.Combine(targetPath, "realm", string.Concat($"{name}Realm", GetFileType(convertType))).Replace("`1", "").Replace("`2", ""));
            }
            generatedResult.Add(name);
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Created {0} {1} {2}.", generatedResult.Count, convertType, isInterface ? "interfaces" : "models");
        Console.ResetColor();
        return generatedResult;
    }

    /// <summary>
    /// Get information from non-abstract properties of the specified type.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="skipDayjs"></param>
    /// <param name="isInterface"></param>
    /// <returns>NameAndType&#x5B;&#93;</returns>
    private static NameAndType[] GetModelPropertiesInType(Type t, bool skipDayjs, bool isInterface) {
        return (
            from p in (
                from x in t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                where x.PropertyType.IsModelTypeNoAbstract()
                select x into p
                select new NameAndType() {
                    Name = p.Name,
                    Type = p.PropertyType.ToTypeScriptType(skipDayjs, isInterface)
                }).Distinct()
            orderby p.Name
            select p).ToArray();
    }

    /// <summary>
    /// Get file content for model or form interface.
    /// </summary>
    /// <param name="convertType"></param>
    /// <param name="type"></param>
    /// <param name="options"></param>
    /// <param name="isInterface"></param>
    /// <param name="isRealmModel"></param>
    /// <returns>string</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private static string GetFileString(ConvertType convertType, Type type, GeneratorOptions options, bool isInterface, bool isRealmModel) {
        switch (convertType) {
            case ConvertType.Ts:
                return isInterface ? CreateInterfaceTsString(type, convertType, false) : CreateModelTsString(type, convertType, options.SkipDayjs ?? false);
            case ConvertType.Kt:
                return isRealmModel
                    ? CreateRealmModelKtString(type, convertType, options.KtPackageName)
                    : CreateModelKtString(type, convertType, options.KtUseRealmDb ?? false, options.KtUseKotlinxSerialization ?? false, options.KtPackageName);
            case ConvertType.Swift:
                return CreateModelSwiftString(type, convertType, options.SwiftUseRealmDb ?? false);
            default:
                throw new ArgumentOutOfRangeException(nameof(convertType), convertType, null);
        }
    }

    /// <summary>
    /// Get the file type by the specified converting type.
    /// </summary>
    /// <param name="convertType"></param>
    /// <returns>string</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private static string GetFileType(ConvertType convertType) {
        switch (convertType) {
            case ConvertType.Ts:
                return ".ts";
            case ConvertType.Kt:
                return ".kt";
            case ConvertType.Swift:
                return ".swift";
            default:
                throw new ArgumentOutOfRangeException(nameof(convertType), convertType, null);
        }
    }

    /// <summary>
    /// Get the filename separated by hyphens.
    /// </summary>
    /// <param name="name"></param>
    /// <returns>string</returns>
    private static string GetFileName(string name) {
        var temp = new StringBuilder();
        var results = new List<string>();

        foreach (char c in name) {
            if (char.IsUpper(c) && temp.ToString() != "") {
                results.Add(temp.ToString());
                temp.Clear();
            }

            temp.Append(c);
        }
        results.Add(temp.ToString());
        return string.Join("-", results);
    }


    /// <summary>
    /// Get type name with generic arguments.
    /// </summary>
    /// <param name="t"></param>
    /// <param name="skipDayjs"></param>
    /// <param name="isInterface"></param>
    /// <returns>string</returns>
    private static string ConcatGenericArguments(Type t, bool skipDayjs, bool isInterface) {
        if (t.GenericTypeArguments.Length == 0) {
            return t.Name;
        }
        return $"{t.Name.Substring(0, t.Name.IndexOf('`'))}<{string.Join(", ", t.GenericTypeArguments.Select(x => x.ToTypeScriptType(skipDayjs, isInterface)))}>";
    }

    /// <summary>
    /// Get a string of concatenated count number of strings containing 4 spaces.
    /// </summary>
    /// <param name="count"></param>
    /// <returns>string</returns>
    private static string TabToSpace(int count) {
        StringBuilder stringBuilder = new();
        for (int i = 0; i < count; i++) {
            stringBuilder.Append("    ");
        }
        return stringBuilder.ToString();
    }
}
