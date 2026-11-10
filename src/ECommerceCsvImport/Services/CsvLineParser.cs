using System.Text;
namespace ECommerceCsvImport.Services;
public static class CsvLineParser
{
    public static IReadOnlyList<string> Parse(string line)
    {
        var result = new List<string>();
        if (line is null) return result;
        var current = new StringBuilder();
        bool inQuotes = false;
        int i = 0;
        while (i < line.Length)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i += 2;
                        continue;
                    }
                    inQuotes = false;
                    i++;
                    continue;
                }
                current.Append(c);
                i++;
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                    i++;
                    continue;
                }
                if (c == ',')
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                    i++;
                    continue;
                }
                current.Append(c);
                i++;
            }
        }
        result.Add(current.ToString().Trim());
        return result;
    }
}