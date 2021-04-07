using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Macrology {
    public class Configuration : IPluginConfiguration {
        private Macrology Plugin { get; set; } = null!;

        public int Version { get; set; } = 1;

        [JsonProperty]
        [JsonConverter(typeof(NodeConverter))]
        public List<INode> Nodes { get; private set; } = new();

        public int MaxLength { get; set; } = 10_000;

        internal void Initialise(Macrology plugin) {
            this.Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Macrology cannot be null");
        }

        internal void Save() {
            var configPath = ConfigPath(this.Plugin);
            var configText = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(configPath, configText);
        }

        private static string ConfigPath(Macrology plugin) {
            string[] paths = {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XIVLauncher",
                "pluginConfigs",
                $"{plugin.Name}.json",
            };
            return Path.Combine(paths);
        }

        internal static Configuration? Load(Macrology plugin) {
            var configPath = ConfigPath(plugin);

            if (!File.Exists(configPath)) {
                return new Configuration();
            }

            string configText;
            try {
                configText = File.ReadAllText(configPath);
            }
            catch (IOException e) {
                PluginLog.Log($"Could not read config at {configPath}: {e.Message}.");
                return null;
            }

            return JsonConvert.DeserializeObject<Configuration>(configText);
        }

        private static IEnumerable<T> Traverse<T>(T item, Func<T, IEnumerable<T>> childSelector) {
            var stack = new Stack<T>();
            stack.Push(item);
            while (stack.Any()) {
                var next = stack.Pop();
                yield return next;
                foreach (var child in childSelector(next)) {
                    stack.Push(child);
                }
            }
        }

        public Macro? FindMacro(Guid id) {
            return this.Nodes.Select(node => (Macro?) Traverse(node, n => n.Children).FirstOrDefault(n => n.Id == id && n is Macro)).FirstOrDefault(macro => macro != null);
        }
    }

    public interface INode {
        Guid Id { get; set; }
        string Name { get; set; }
        List<INode> Children { get; }

        INode Duplicate();
    }

    public class Folder : INode {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public List<INode> Children { get; private set; } = new();

        public Folder(string name, List<INode>? children = null) {
            this.Id = Guid.NewGuid();
            this.Name = name;
            if (children != null) {
                this.Children = children;
            }
        }

        internal Folder(Guid id, string name, List<INode> children) {
            this.Id = id;
            this.Name = name;
            this.Children = children;
        }

        public INode Duplicate() {
            return new Folder(this.Id, this.Name, this.Children);
        }
    }

    public class Macro : INode {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Contents { get; set; }
        public List<INode> Children => new();

        public Macro(string name, string contents) {
            this.Id = Guid.NewGuid();
            this.Name = name;
            this.Contents = contents;
        }

        internal Macro(Guid id, string name, string contents) {
            this.Id = id;
            this.Name = name;
            this.Contents = contents;
        }

        public INode Duplicate() {
            return new Macro(this.Id, this.Name, this.Contents);
        }
    }

    // This custom converter is necessary to enable live reloading of the assembly. Without this converter, trying to use type information will fail
    // when a new version of the assembly is loaded. Instead, don't use type information and just check for the presence of a "Contents" key, then
    // manually do the deserialisation. It's gross, but it works.

    public class NodeConverter : JsonConverter {
        public override bool CanWrite => false;
        public override bool CanRead => true;

        public override bool CanConvert(Type objectType) {
            return objectType == typeof(INode);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            throw new InvalidOperationException("Use default serialization.");
        }

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) {
            var jsonArray = JArray.Load(reader);
            var list = new List<INode>();
            foreach (var token in jsonArray) {
                var jsonObject = (JObject) token;
                INode node;
                if (jsonObject.ContainsKey("Contents")) {
                    node = new Macro(
                        jsonObject["Id"].ToObject<Guid>(),
                        jsonObject["Name"].ToObject<string>(),
                        jsonObject["Contents"].ToObject<string>()
                    );
                }
                else {
                    node = new Folder(
                        jsonObject["Id"].ToObject<Guid>(),
                        jsonObject["Name"].ToObject<string>(),
                        (List<INode>) this.ReadJson(jsonObject["Children"].CreateReader(), typeof(List<INode>), null, serializer)
                    );
                }

                list.Add(node);
            }

            return list;
        }
    }
}
