using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace NextXI.Core.Persistence
{
    /// <summary>Storage abstraction (disk on device, memory in tests).</summary>
    public interface IFileStore
    {
        bool Exists(string name);
        string Read(string name);
        /// <summary>Must be atomic: either the old or the new content survives a crash.</summary>
        void WriteAtomic(string name, string content);
        void Delete(string name);
        IEnumerable<string> List(string prefix);
    }

    public sealed class DiskFileStore : IFileStore
    {
        private readonly string _root;

        public DiskFileStore(string root)
        {
            _root = root;
            Directory.CreateDirectory(root);
        }

        private string P(string name) => Path.Combine(_root, name);
        public bool Exists(string name) => File.Exists(P(name));
        public string Read(string name) => File.ReadAllText(P(name), Encoding.UTF8);

        public void WriteAtomic(string name, string content)
        {
            string path = P(name);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, content, Encoding.UTF8);
            if (File.Exists(path))
            {
                // Keep the previous good version as a backup, swap in the new file.
                string bak = path + ".bak";
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(path, bak);
            }
            File.Move(tmp, path);
        }

        public void Delete(string name)
        {
            if (File.Exists(P(name))) File.Delete(P(name));
            if (File.Exists(P(name) + ".bak")) File.Delete(P(name) + ".bak");
        }

        public IEnumerable<string> List(string prefix)
        {
            if (!Directory.Exists(_root)) yield break;
            foreach (var f in Directory.GetFiles(_root, prefix + "*"))
            {
                var n = Path.GetFileName(f);
                if (n.EndsWith(".tmp") || n.EndsWith(".bak")) continue;
                yield return n;
            }
        }
    }

    public sealed class MemoryFileStore : IFileStore
    {
        public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
        public bool Exists(string name) => Files.ContainsKey(name);
        public string Read(string name) => Files[name];
        public void WriteAtomic(string name, string content)
        {
            if (Files.TryGetValue(name, out var old)) Files[name + ".bak"] = old;
            Files[name] = content;
        }
        public void Delete(string name) { Files.Remove(name); Files.Remove(name + ".bak"); }
        public IEnumerable<string> List(string prefix)
        {
            foreach (var k in Files.Keys) if (k.StartsWith(prefix) && !k.EndsWith(".bak")) yield return k;
        }
    }

    [Serializable]
    public sealed class SaveEnvelope
    {
        public string Magic = "NEXTXI";
        public string Kind;
        public int FormatVersion;
        public string GameVersion;
        public string SavedAtUtc;
        public string Checksum;
        public string Payload;
    }

    public enum LoadStatus { Ok, NotFound, RecoveredFromBackup, Corrupted, IncompatibleVersion }

    /// <summary>
    /// Versioned, checksummed JSON saves with atomic writes, automatic backup and
    /// recovery, and forward migrations between save format versions.
    /// </summary>
    public sealed class SaveSystem
    {
        public const string GameVersion = "0.1.0";
        private readonly IFileStore _store;
        private readonly Dictionary<string, SortedDictionary<int, Func<JObject, JObject>>> _migrations =
            new Dictionary<string, SortedDictionary<int, Func<JObject, JObject>>>();

        public static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Converters = { new StringEnumConverter() }
        };

        public SaveSystem(IFileStore store) { _store = store; }

        public IFileStore Store => _store;

        /// <summary>Registers a migration that upgrades payloads of `kind` from `fromVersion` to fromVersion+1.</summary>
        public void RegisterMigration(string kind, int fromVersion, Func<JObject, JObject> migrate)
        {
            if (!_migrations.TryGetValue(kind, out var m)) _migrations[kind] = m = new SortedDictionary<int, Func<JObject, JObject>>();
            m[fromVersion] = migrate;
        }

        public void Save<T>(string name, string kind, int version, T data)
        {
            string payload = JsonConvert.SerializeObject(data, JsonSettings);
            var env = new SaveEnvelope
            {
                Kind = kind,
                FormatVersion = version,
                GameVersion = GameVersion,
                SavedAtUtc = DateTime.UtcNow.ToString("o"),
                Checksum = Hash(payload),
                Payload = payload
            };
            _store.WriteAtomic(name, JsonConvert.SerializeObject(env, Formatting.Indented));
        }

        public LoadStatus TryLoad<T>(string name, string kind, int currentVersion, out T data, out string error) where T : class
        {
            data = null;
            error = null;
            if (!_store.Exists(name)) { error = "not found"; return LoadStatus.NotFound; }
            var status = TryLoadFile(name, kind, currentVersion, out data, out error);
            if (status == LoadStatus.Ok || status == LoadStatus.IncompatibleVersion) return status;
            Log.Warn("Save", $"{name} failed to load ({error}); trying backup");
            if (_store.Exists(name + ".bak"))
            {
                var s2 = TryLoadFile(name + ".bak", kind, currentVersion, out data, out var err2);
                if (s2 == LoadStatus.Ok)
                {
                    // Restore the good backup as the main file.
                    _store.WriteAtomic(name, _store.Read(name + ".bak"));
                    return LoadStatus.RecoveredFromBackup;
                }
                error += "; backup: " + err2;
            }
            return LoadStatus.Corrupted;
        }

        private LoadStatus TryLoadFile<T>(string name, string kind, int currentVersion, out T data, out string error) where T : class
        {
            data = null;
            error = null;
            try
            {
                var env = JsonConvert.DeserializeObject<SaveEnvelope>(_store.Read(name));
                if (env == null || env.Magic != "NEXTXI") { error = "not a NEXT XI save"; return LoadStatus.Corrupted; }
                if (env.Kind != kind) { error = $"wrong kind {env.Kind}"; return LoadStatus.Corrupted; }
                if (env.Payload == null || Hash(env.Payload) != env.Checksum) { error = "checksum mismatch"; return LoadStatus.Corrupted; }
                if (env.FormatVersion > currentVersion) { error = $"save from newer version {env.FormatVersion}"; return LoadStatus.IncompatibleVersion; }
                var jo = JObject.Parse(env.Payload);
                int v = env.FormatVersion;
                while (v < currentVersion)
                {
                    if (!_migrations.TryGetValue(kind, out var m) || !m.TryGetValue(v, out var step))
                    {
                        error = $"no migration from v{v}";
                        return LoadStatus.IncompatibleVersion;
                    }
                    jo = step(jo);
                    v++;
                }
                data = jo.ToObject<T>(JsonSerializer.Create(JsonSettings));
                if (data == null) { error = "empty payload"; return LoadStatus.Corrupted; }
                return LoadStatus.Ok;
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                return LoadStatus.Corrupted;
            }
        }

        public void Delete(string name) => _store.Delete(name);
        public bool Exists(string name) => _store.Exists(name);
        public IEnumerable<string> List(string prefix) => _store.List(prefix);

        public static string Hash(string s)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>Deep copy through JSON (used for snapshots).</summary>
        public static T Clone<T>(T obj) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(obj, JsonSettings), JsonSettings);
    }
}
