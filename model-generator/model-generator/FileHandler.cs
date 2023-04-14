using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.Text;

namespace model_generator;

public static class FileHandler {
    public static T ReadJson<T>(string filePath)
        where T : class {
        T t;
        try {
            if (!File.Exists(filePath)) {
                return default(T);
            }

            DataContractJsonSerializer dataContractJsonSerializer = new(typeof(T));
            byte[] bytes = Encoding.UTF8.GetBytes(File.ReadAllText(filePath));
            using MemoryStream memoryStream = new(bytes);
            t = (T)dataContractJsonSerializer.ReadObject(memoryStream);
        } catch (Exception exception) {
            throw new SerializationException("Failed to parse json", exception);
        }
        return t;
    }
}
