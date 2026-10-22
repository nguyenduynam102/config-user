using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace WinUser;
public static class Policy
{
    public static string UserName(string name)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 20 || name.EndsWith('.') || name.Any(c => char.IsControl(c) || "\"/\\[]:;|=,+*?<>@".Contains(c)))
            throw new ArgumentException("Tên user không hợp lệ (1–20 ký tự).");
        return name;
    }
    public static string Shortcut(string path)
    {
        var name = Path.GetFileName(path);
        if (name != path || name is "." or ".." || name.IndexOfAny(new[]{'/', '\\', ':'}) >= 0 ||
            !(name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".url", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Chỉ chấp nhận tên shortcut .lnk hoặc .url.");
        return name;
    }
    public static string Unc(string path)
    {
        if (!path.StartsWith(@"\\") || path.StartsWith(@"\\?\") || path.StartsWith(@"\\.\") || path.Contains('"') || path.Any(char.IsControl))
            throw new ArgumentException("Đường dẫn phải là UNC \\\\máy-chủ\\share.");
        var parts = path[2..].Split('\\');
        if (parts.Length < 2 || parts.Any(p => p.Length == 0 || p is "." or ".." || p.Contains(':')))
            throw new ArgumentException("UNC không hợp lệ.");
        return path;
    }
    public static string Drive(string value)
    {
        value = value.Trim().ToUpperInvariant();
        if (value.Length == 1) value += ":";
        if (value.Length != 2 || value[0] < 'D' || value[0] > 'Z' || value[1] != ':') throw new ArgumentException("Chọn ổ D: đến Z:.");
        return value;
    }
    public static IReadOnlyList<RegChange> ParseRegistry(string text)
    {
        var lines = text.Replace("\r", "").Split('\n');
        if (lines.Length == 0 || lines[0].Trim('\uFEFF', ' ') != "Windows Registry Editor Version 5.00")
            throw new ArgumentException("Chỉ hỗ trợ file REG Version 5.00.");
        string? key = null;
        var changes = new List<RegChange>();
        foreach (var raw in lines.Skip(1))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';')) continue;
            if (line.StartsWith('['))
            {
                if (!line.EndsWith(']') || !line.StartsWith("[HKEY_CURRENT_USER\\", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Chỉ cho phép HKEY_CURRENT_USER; không xóa khóa hoặc sửa Registry toàn máy.");
                key = line[19..^1];
                if (!key.StartsWith("Software\\", StringComparison.OrdinalIgnoreCase) || key.Contains("..") ||
                    key.Split('\\').Any(s => new[]{"Run", "RunOnce", "Policies", "Winlogon", "Classes"}.Contains(s, StringComparer.OrdinalIgnoreCase)))
                    throw new ArgumentException("Khóa nhạy cảm hoặc ngoài HKCU\\Software bị từ chối ở phiên bản này.");
                continue;
            }
            if (key is null) throw new ArgumentException("Giá trị REG thiếu khóa.");
            var eq = line.IndexOf('=');
            if (eq < 1) throw new ArgumentException("Dòng REG không hợp lệ.");
            var lhs = line[..eq].Trim(); var rhs = line[(eq+1)..].Trim();
            var name = lhs == "@" ? "" : Unquote(lhs);
            if (rhs.StartsWith("dword:", StringComparison.OrdinalIgnoreCase) && rhs.Length == 14 && uint.TryParse(rhs[6..], System.Globalization.NumberStyles.HexNumber, null, out var num))
                changes.Add(new(key, name, null, unchecked((int)num)));
            else if (rhs.StartsWith('"')) changes.Add(new(key, name, Unquote(rhs), null));
            else throw new ArgumentException("Chỉ hỗ trợ REG_SZ và REG_DWORD, không hỗ trợ xóa hoặc hex/multiline.");
        }
        if (changes.Count == 0) throw new ArgumentException("File REG không có thay đổi được hỗ trợ.");
        return changes;
    }
    private static string Unquote(string s)
    {
        if (s.Length < 2 || s[0] != '"' || s[^1] != '"') throw new ArgumentException("Chuỗi REG không hợp lệ.");
        var result = "";
        for (int i = 1; i < s.Length-1; i++)
        {
            if (s[i] == '\\') { if (++i >= s.Length-1 || s[i] is not ('\\' or '"')) throw new ArgumentException("Escape REG không hỗ trợ."); }
            else if (s[i] == '"') throw new ArgumentException("Dấu nháy REG không hợp lệ.");
            result += s[i];
        }
        return result;
    }
}
public record RegChange(string Key, string Name, string? Text, int? Number);
