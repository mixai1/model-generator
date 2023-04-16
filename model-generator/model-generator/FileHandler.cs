using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace model_generator;

public static class FileHandler {
    public static T ReadJson<T>(string filePath) where T : class {
        try {
            if (!File.Exists(filePath)) {
                return default(T);
            }

            var file = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<T>(file);
        } catch (Exception exception) {
            throw new SerializationException("Failed to parse json", exception);
        }
    }
}
