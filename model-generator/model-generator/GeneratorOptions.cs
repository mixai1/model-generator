namespace model_generator;

public class GeneratorOptions {
    public string TsDestination { get; set; }

    public string[] Files { get; set; }

    public string[] Sources { get; set; }

    public ConvertType[] ConvertTypes { get; set; }

    public string Compaild { get; set; }

    public bool? SkipTsFormInterfaces { get; set; }

    public bool? SkipDayjs { get; set; }

    public string KtDestination { get; set; }

    public string KtPackageName { get; set; }

    public bool? KtUseKotlinxSerialization { get; set; }

    public bool? KtUseRealmDb { get; set; }

    public string SwiftDestination { get; set; }

    public bool? SwiftUseRealmDb { get; set; }
}
