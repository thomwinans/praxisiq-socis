using System.Text.Json;
using System.Text.Json.Nodes;

namespace Snapp.Tools.SpecMerge;

public class Program
{
    static readonly string[] ServiceNames =
        ["auth", "user", "network", "content", "intelligence", "transaction", "notification", "linkedin"];

    public static int Main(string[] args)
    {
        var rootDir = FindRootDir();
        var specsDir = Path.Combine(rootDir, "api", "specs");
        var outputJson = Path.Combine(rootDir, "api", "snapp-api.json");
        var outputYaml = Path.Combine(rootDir, "api", "snapp-api.yaml");

        Console.WriteLine("Snapp.Tools.SpecMerge — OpenAPI spec merge tool");
        Console.WriteLine($"  Specs dir: {specsDir}");

        if (!Directory.Exists(specsDir))
        {
            Console.Error.WriteLine($"ERROR: Specs directory not found: {specsDir}");
            return 1;
        }

        var specFiles = Directory.GetFiles(specsDir, "*.json");
        if (specFiles.Length == 0)
        {
            Console.Error.WriteLine("ERROR: No spec files found in api/specs/");
            return 1;
        }

        Console.WriteLine($"  Found {specFiles.Length} spec file(s)");

        var merged = BuildMergedSpec();
        var totalPaths = 0;
        var totalSchemas = 0;

        foreach (var file in specFiles.OrderBy(f => f))
        {
            var serviceName = Path.GetFileNameWithoutExtension(file);
            Console.Write($"  Merging {serviceName}... ");

            try
            {
                var json = File.ReadAllText(file);
                var spec = JsonNode.Parse(json);
                if (spec == null)
                {
                    Console.WriteLine("SKIP (empty)");
                    continue;
                }

                var pathCount = MergePaths(merged, spec);
                var schemaCount = MergeSchemas(merged, spec);
                totalPaths += pathCount;
                totalSchemas += schemaCount;
                Console.WriteLine($"OK ({pathCount} paths, {schemaCount} schemas)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAILED: {ex.Message}");
            }
        }

        // Write JSON output
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var mergedJson = merged.ToJsonString(jsonOptions);
        File.WriteAllText(outputJson, mergedJson);
        Console.WriteLine($"\n  Output: {outputJson}");

        // Write YAML output
        var yamlContent = ConvertJsonToYaml(merged);
        File.WriteAllText(outputYaml, yamlContent);
        Console.WriteLine($"  Output: {outputYaml}");

        // Validate
        var valid = ValidateMergedSpec(merged);
        Console.WriteLine($"\n  Total paths: {totalPaths}");
        Console.WriteLine($"  Total schemas: {totalSchemas}");
        Console.WriteLine($"  Valid: {valid}");

        return valid ? 0 : 1;
    }

    static JsonObject BuildMergedSpec()
    {
        var version = GetVersion();

        return new JsonObject
        {
            ["openapi"] = "3.1.0",
            ["info"] = new JsonObject
            {
                ["title"] = "SNAPP API",
                ["description"] = "Social Networking Application for PraxisIQ — unified API",
                ["version"] = version
            },
            ["servers"] = new JsonArray
            {
                new JsonObject
                {
                    ["url"] = "http://localhost:8000/api",
                    ["description"] = "Local development (Kong)"
                },
                new JsonObject
                {
                    ["url"] = "https://api.snapp.praxisiq.com",
                    ["description"] = "Production (placeholder)"
                }
            },
            ["paths"] = new JsonObject(),
            ["components"] = new JsonObject
            {
                ["schemas"] = new JsonObject(),
                ["securitySchemes"] = new JsonObject
                {
                    ["BearerJwt"] = new JsonObject
                    {
                        ["type"] = "http",
                        ["scheme"] = "bearer",
                        ["bearerFormat"] = "JWT",
                        ["description"] = "RS256 JWT issued by snapp-auth via magic link flow"
                    }
                }
            },
            ["security"] = new JsonArray
            {
                new JsonObject
                {
                    ["BearerJwt"] = new JsonArray()
                }
            }
        };
    }

    static int MergePaths(JsonObject merged, JsonNode source)
    {
        var paths = source["paths"];
        if (paths is not JsonObject pathsObj) return 0;

        var mergedPaths = merged["paths"]!.AsObject();
        var count = 0;

        foreach (var (path, value) in pathsObj)
        {
            if (value == null) continue;

            if (mergedPaths.ContainsKey(path))
            {
                // Merge methods into existing path
                var existing = mergedPaths[path]!.AsObject();
                var incoming = value.AsObject();
                foreach (var (method, methodValue) in incoming)
                {
                    if (methodValue != null && !existing.ContainsKey(method))
                    {
                        existing[method] = methodValue.DeepClone();
                    }
                }
            }
            else
            {
                mergedPaths[path] = value.DeepClone();
                count++;
            }
        }

        return count;
    }

    static int MergeSchemas(JsonObject merged, JsonNode source)
    {
        var schemas = source["components"]?["schemas"];
        if (schemas is not JsonObject schemasObj) return 0;

        var mergedSchemas = merged["components"]!["schemas"]!.AsObject();
        var count = 0;

        foreach (var (name, value) in schemasObj)
        {
            if (value == null) continue;

            if (!mergedSchemas.ContainsKey(name))
            {
                mergedSchemas[name] = value.DeepClone();
                count++;
            }
            // Duplicate schemas from Snapp.Shared are expected — skip silently
        }

        return count;
    }

    static bool ValidateMergedSpec(JsonObject merged)
    {
        var valid = true;

        if (merged["openapi"]?.GetValue<string>() is not "3.1.0")
        {
            Console.Error.WriteLine("  VALIDATION: Missing or incorrect openapi version");
            valid = false;
        }

        if (merged["info"]?["title"]?.GetValue<string>() is null)
        {
            Console.Error.WriteLine("  VALIDATION: Missing info.title");
            valid = false;
        }

        if (merged["paths"] is not JsonObject paths || paths.Count == 0)
        {
            Console.Error.WriteLine("  VALIDATION: No paths in merged spec");
            valid = false;
        }

        if (merged["components"]?["securitySchemes"]?["BearerJwt"] is null)
        {
            Console.Error.WriteLine("  VALIDATION: Missing BearerJwt security scheme");
            valid = false;
        }

        return valid;
    }

    static string GetVersion()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", "describe --tags --always")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit();
                if (proc.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    return output;
            }
        }
        catch { }

        return "0.1.0";
    }

    static string FindRootDir()
    {
        // Walk up from current directory looking for snapp.sln
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "snapp.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }

        // Fallback: assume we're in the root
        return Directory.GetCurrentDirectory();
    }

    static string ConvertJsonToYaml(JsonNode node)
    {
        var serializer = new SharpYaml.Serialization.Serializer(new SharpYaml.Serialization.SerializerSettings
        {
            EmitDefaultValues = true,
            SortKeyForMapping = false
        });

        // Convert JsonNode to dictionary structure that SharpYaml can serialize
        var obj = ConvertJsonNodeToObject(node);
        return serializer.Serialize(obj);
    }

    static object? ConvertJsonNodeToObject(JsonNode? node)
    {
        if (node is null) return null;

        if (node is JsonObject obj)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var (key, value) in obj)
                dict[key] = ConvertJsonNodeToObject(value);
            return dict;
        }

        if (node is JsonArray arr)
        {
            var list = new List<object?>();
            foreach (var item in arr)
                list.Add(ConvertJsonNodeToObject(item));
            return list;
        }

        if (node is JsonValue val)
        {
            if (val.TryGetValue<bool>(out var b)) return b;
            if (val.TryGetValue<long>(out var l)) return l;
            if (val.TryGetValue<double>(out var d)) return d;
            if (val.TryGetValue<string>(out var s)) return s;
            return val.ToString();
        }

        return node.ToString();
    }
}
