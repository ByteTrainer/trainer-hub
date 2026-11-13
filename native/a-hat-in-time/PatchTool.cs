using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

internal static class PatchTool {
    private const string Base = "Binaries/Win64";
    private const string Target = "HatinTimeGame.exe";
    private const string Runtime = "HatTrainer.Runtime.dll";
    private const string OriginalHash = "0197F98641C2150C5E90E5E911F4464E38527C241E50817736CA7072C0B99892";

    private static string Hash(byte[] bytes) {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
    }

    private static int Read(byte[] data, int offset) { return BitConverter.ToInt32(data, offset); }
    private static void Write(byte[] data, int offset, int value) {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, data, offset, 4);
    }

    private static int Align(int value, int alignment) { return (value + alignment - 1) & -alignment; }

    private static int Locate(byte[] image, int table, int count, int rva) {
        for (int i = 0; i < count; ++i) {
            int header = table + i * 40;
            int displacement = rva - Read(image, header + 12);
            if (displacement >= 0 && displacement < Read(image, header + 16))
                return Read(image, header + 20) + displacement;
        }
        throw new InvalidDataException("The import directory is outside the image sections.");
    }

    private static byte[] AddRuntimeImport(byte[] original) {
        int pe = Read(original, 0x3c), optional = pe + 24;
        int count = BitConverter.ToUInt16(original, pe + 6);
        int table = optional + BitConverter.ToUInt16(original, pe + 20);
        int header = table + count * 40;
        if (Read(original, pe) != 0x4550 || BitConverter.ToUInt16(original, optional) != 0x20b ||
            header + 40 > Read(original, optional + 60))
            throw new InvalidDataException("Unexpected plugin image layout.");

        int directories = optional + 112, imports = directories + 8;
        int oldImports = Locate(original, table, count, Read(original, imports));
        int oldSize = 0;
        while (oldSize < Read(original, imports + 4) && Read(original, oldImports + oldSize + 12) != 0)
            oldSize += 20;

        int sectionAlignment = Read(original, optional + 32), fileAlignment = Read(original, optional + 36);
        int rva = Align(Read(original, optional + 56), sectionAlignment);
        int raw = Align(original.Length, fileAlignment);
        int lookup = Align(oldSize + 40, 8), address = lookup + 16, name = address + 16;
        byte[] dllName = Encoding.ASCII.GetBytes(Runtime + "\0");
        byte[] symbol = Encoding.ASCII.GetBytes("ByteTrainerRuntime\0");
        int hintName = Align(name + dllName.Length, 2), length = hintName + 2 + symbol.Length;
        int rawLength = Align(length, fileAlignment);
        var patched = new byte[raw + rawLength];
        Buffer.BlockCopy(original, 0, patched, 0, original.Length);
        Buffer.BlockCopy(original, oldImports, patched, raw, oldSize);
        Buffer.BlockCopy(dllName, 0, patched, raw + name, dllName.Length);
        Buffer.BlockCopy(symbol, 0, patched, raw + hintName + 2, symbol.Length);
        Write(patched, raw + oldSize, rva + lookup);
        Write(patched, raw + oldSize + 12, rva + name);
        Write(patched, raw + oldSize + 16, rva + address);
        Write(patched, raw + lookup, rva + hintName);
        Write(patched, raw + address, rva + hintName);

        Buffer.BlockCopy(Encoding.ASCII.GetBytes(".byte"), 0, patched, header, 5);
        Write(patched, header + 8, length);
        Write(patched, header + 12, rva);
        Write(patched, header + 16, rawLength);
        Write(patched, header + 20, raw);
        Write(patched, header + 36, unchecked((int)0xc0000040));
        Buffer.BlockCopy(BitConverter.GetBytes((ushort)(count + 1)), 0, patched, pe + 6, 2);
        Write(patched, optional + 8, Read(original, optional + 8) + rawLength);
        Write(patched, optional + 56, Align(rva + length, sectionAlignment));
        Write(patched, optional + 64, 0);
        Write(patched, imports, rva);
        Write(patched, imports + 4, oldSize + 40);
        return patched;
    }

    private static void Main(string[] args) {
        try {
            if (args.Length != 2 || args[0] != "install")
                throw new ArgumentException("Use: PatchTool.exe install GAME_FOLDER");

            string dir = Path.Combine(args[1], Base);
            string target = Path.Combine(dir, Target);
            string backup = target + ".trainerhub-original", marker = target + ".trainerhub-sha256";
            byte[] current = File.ReadAllBytes(target);
            string hash = Hash(current);
            if (hash != OriginalHash && (!File.Exists(marker) || File.ReadAllText(marker).Trim() != hash))
                throw new InvalidDataException("The original executable does not match the supported Steam build.");
            byte[] original = File.Exists(backup) ? File.ReadAllBytes(backup) : current;
            if (Hash(original) != OriginalHash) throw new InvalidDataException("The original executable backup is invalid.");
            byte[] patched = AddRuntimeImport(original);
            if (!File.Exists(backup)) File.WriteAllBytes(backup, original);
            File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Runtime), Path.Combine(dir, Runtime), true);
            File.WriteAllBytes(target, patched);
            File.WriteAllText(marker, Hash(patched));
        } catch (Exception error) {
            Console.Error.WriteLine(error.Message);
            Environment.ExitCode = 1;
        }
    }
}
