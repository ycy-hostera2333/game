using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Deeptide.Core.Data
{
    /// <summary>
    /// CSV 解析（策划/09 M2.1）：逗号分隔，第一行是表头；字段可以用双引号包起来，引号里可以有逗号、换行，"" 表示一个引号。
    /// 空行和以 # 开头的行跳过。Core 只接收字符串，读文件交给外层（Unity 用 TextAsset，Tools 直接读磁盘）。
    /// </summary>
    public static class Csv
    {
        public static CsvTable Parse(string text, string fileName)
        {
            var records = new List<(int Line, List<string> Fields)>();
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false, fieldStarted = false;
            int line = 1, recordLine = 1;
            int i = 0;
            if (text.Length > 0 && text[0] == '﻿') i = 1;

            void EndField()
            {
                fields.Add(sb.ToString());
                sb.Clear();
                fieldStarted = false;
            }

            void EndRecord()
            {
                EndField();
                bool blank = fields.Count == 1 && fields[0].Trim().Length == 0;
                bool comment = fields.Count > 0 && fields[0].TrimStart().StartsWith("#", StringComparison.Ordinal);
                if (!blank && !comment) records.Add((recordLine, fields));
                fields = new List<string>();
            }

            for (; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else
                    {
                        if (c == '\n') line++;
                        sb.Append(c);
                    }
                    continue;
                }
                switch (c)
                {
                    case '"' when !fieldStarted:
                        inQuotes = true;
                        fieldStarted = true;
                        break;
                    case ',':
                        EndField();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        EndRecord();
                        line++;
                        recordLine = line;
                        break;
                    default:
                        sb.Append(c);
                        fieldStarted = true;
                        break;
                }
            }
            if (inQuotes) throw new FormatException($"{fileName}：第 {recordLine} 行的引号没有闭合");
            if (sb.Length > 0 || fields.Count > 0) EndRecord();

            if (records.Count == 0) throw new FormatException($"{fileName}：缺少表头");
            var header = records[0].Fields;
            var table = new CsvTable(fileName, header);
            for (int r = 1; r < records.Count; r++)
            {
                var (ln, f) = records[r];
                if (f.Count != header.Count)
                    throw new FormatException($"{fileName}：第 {ln} 行有 {f.Count} 列，表头有 {header.Count} 列");
                table.Rows.Add(new CsvRow(table, ln, f));
            }
            return table;
        }

        /// <summary>解析「Damage=6;Hits=2」格式的数值表。</summary>
        public static Dictionary<string, int> ParseVars(string text)
        {
            var result = new Dictionary<string, int>();
            foreach (var kv in ParseMap(text))
            {
                if (!int.TryParse(kv.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                    throw new FormatException($"数值格式错误：{kv.Key}={kv.Value}");
                result[kv.Key] = v;
            }
            return result;
        }

        /// <summary>解析「Prayer=狂热祷文;Dagger=血祭匕首」格式，保持书写顺序。</summary>
        public static List<KeyValuePair<string, string>> ParseMap(string text)
        {
            var result = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrWhiteSpace(text)) return result;
            foreach (var pair in text.Split(';'))
            {
                if (pair.Trim().Length == 0) continue;
                int eq = pair.IndexOf('=');
                if (eq <= 0) throw new FormatException($"格式错误：{pair}（应为 键=值）");
                result.Add(new KeyValuePair<string, string>(pair.Substring(0, eq).Trim(), pair.Substring(eq + 1).Trim()));
            }
            return result;
        }

        /// <summary>解析「exhaust;ethereal」格式的标签列表。</summary>
        public static List<string> ParseList(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return result;
            foreach (var item in text.Split(';'))
                if (item.Trim().Length > 0) result.Add(item.Trim());
            return result;
        }
    }

    public sealed class CsvTable
    {
        public readonly string FileName;
        public readonly List<string> Header;
        public readonly List<CsvRow> Rows = new List<CsvRow>();
        readonly Dictionary<string, int> _columns = new Dictionary<string, int>();

        public CsvTable(string fileName, List<string> header)
        {
            FileName = fileName;
            Header = header;
            for (int i = 0; i < header.Count; i++) _columns[header[i].Trim()] = i;
        }

        public bool HasColumn(string name) => _columns.ContainsKey(name);

        internal int Column(string name)
        {
            if (!_columns.TryGetValue(name, out int i)) throw new FormatException($"{FileName}：缺少列 {name}");
            return i;
        }
    }

    public sealed class CsvRow
    {
        readonly CsvTable _table;
        readonly List<string> _fields;
        public readonly int Line;

        internal CsvRow(CsvTable table, int line, List<string> fields)
        {
            _table = table;
            Line = line;
            _fields = fields;
        }

        /// <summary>按列名取值（去掉首尾空白）；表里没有这一列时返回空串。</summary>
        public string this[string column] => _table.HasColumn(column) ? _fields[_table.Column(column)].Trim() : "";

        public string Where => $"{_table.FileName} 第 {Line} 行";

        public int Int(string column)
        {
            string s = this[column];
            if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                throw new FormatException($"{Where}：{column} 应为整数，实际是「{s}」");
            return v;
        }

        public int IntOr(string column, int fallback) => this[column].Length == 0 ? fallback : Int(column);

        public T Enum<T>(string column) where T : struct
        {
            string s = this[column];
            if (!System.Enum.TryParse(s, true, out T v) || !System.Enum.IsDefined(typeof(T), v))
                throw new FormatException($"{Where}：{column} 不能是「{s}」（可选 {string.Join(" / ", System.Enum.GetNames(typeof(T)))}）");
            return v;
        }

        public bool Flag(string column)
        {
            string s = this[column];
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "是";
        }
    }
}
