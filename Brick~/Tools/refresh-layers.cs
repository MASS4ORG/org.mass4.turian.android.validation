#!/usr/bin/env dotnet
// Refreshes the Khronos Vulkan validation layer payloads of this brick and its per-architecture siblings.
// Usage: dotnet run Brick~/Tools/refresh-layers.cs
using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

const string version = "1.4.363.1";
const string archiveSha256 = "2C17B00A95B18FD63244B27332EF86F45C9269769F798A6261CE80B869C41AF4";
const string layer = "libVkLayer_khronos_validation.so";
const string common = "org.mass4.turian.android.validation";
const int requiredAlignment = 16384;

var tag = "vulkan-sdk-" + version;
var url = $"https://github.com/KhronosGroup/Vulkan-ValidationLayers/releases/download/{tag}/android-binaries-{version}.zip";
var tools = (string)AppContext.GetData("EntryPointFileDirectoryPath")!;
var root = Path.GetFullPath(Path.Combine(tools, "..", ".."));
var bricks = Path.GetDirectoryName(root)!;
var work = Path.Combine(root, ".local");
Directory.CreateDirectory(work);

using var client = new HttpClient();
var archivePath = Path.Combine(work, $"android-binaries-{version}.zip");
if (!File.Exists(archivePath))
{
    await File.WriteAllBytesAsync(archivePath, await client.GetByteArrayAsync(url));
}
var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archivePath)));
if (actual != archiveSha256)
{
    File.Delete(archivePath);
    throw new InvalidDataException($"{url} has SHA-256 {actual}, expected {archiveSha256}.");
}

var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
using (var archive = ZipFile.OpenRead(archivePath))
{
    foreach (var (abi, architecture) in new[] { ("arm64-v8a", "arm64"), ("x86_64", "x64") })
    {
        var entry = archive.Entries.Single(candidate => candidate.FullName.EndsWith($"/{abi}/{layer}", StringComparison.Ordinal));
        var directory = Path.Combine(bricks, $"{common}.{architecture}", "Brick~", "Native");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, layer);
        entry.ExtractToFile(target, overwrite: true);
        var bytes = File.ReadAllBytes(target);
        RequireAlignment(abi, bytes);
        hashes[abi + "/" + layer] = Convert.ToHexString(SHA256.HashData(bytes));
    }
}

var license = await client.GetStringAsync($"https://raw.githubusercontent.com/KhronosGroup/Vulkan-ValidationLayers/{tag}/LICENSE.txt");
foreach (var brick in new[] { common, common + ".arm64", common + ".x64" })
{
    File.WriteAllText(Path.Combine(bricks, brick, "LICENSE.md"), license);
}

using (var stream = File.Create(Path.Combine(root, "Brick~", "build.json")))
using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
{
    writer.WriteStartObject();
    writer.WriteString("Version", version);
    writer.WriteString("Source", url);
    writer.WriteString("ArchiveSha256", archiveSha256);
    writer.WriteStartObject("Sha256");
    foreach (var (name, hash) in hashes)
    {
        writer.WriteString(name, hash);
    }
    writer.WriteEndObject();
    writer.WriteEndObject();
}
return 0;

static void RequireAlignment(string abi, byte[] elf)
{
    var table = (int)BinaryPrimitives.ReadUInt64LittleEndian(elf.AsSpan(32));
    var entrySize = BinaryPrimitives.ReadUInt16LittleEndian(elf.AsSpan(54));
    var entries = BinaryPrimitives.ReadUInt16LittleEndian(elf.AsSpan(56));
    for (var i = 0; i < entries; i++)
    {
        var header = elf.AsSpan(table + (i * entrySize), entrySize);
        var alignment = BinaryPrimitives.ReadUInt64LittleEndian(header[48..]);
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) == 1 && alignment < requiredAlignment)
        {
            throw new InvalidDataException($"{abi}: a LOAD segment aligns to {alignment}, below {requiredAlignment} bytes.");
        }
    }
}
