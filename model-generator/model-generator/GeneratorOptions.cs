using System.Runtime.Serialization;

namespace model_generator;

[DataContract]
public class GeneratorOptions {
    [DataMember]
    public string Namespace { get; set; }

    [DataMember]
    public string TsDestination { get; set; }

    [DataMember]
    public bool? SkipTsFormInterfaces { get; set; }

    [DataMember]
    public string[] Files { get; set; }

    [DataMember]
    public string Source { get; set; }

    [DataMember]
    public string Models { get; set; }

    [DataMember]
    public bool? SkipDayjs { get; set; }

    [DataMember]
    public string KtDestination { get; set; }

    [DataMember]
    public string KtPackageName { get; set; }

    [DataMember]
    public bool? KtUseKotlinxSerialization { get; set; }

    [DataMember]
    public bool? KtUseRealmDb { get; set; }

    [DataMember]
    public string SwiftDestination { get; set; }

    [DataMember]
    public bool? SwiftUseRealmDb { get; set; }
}
