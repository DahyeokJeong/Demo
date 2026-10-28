using System;
using System.Collections.Generic;
using System.Text;

public static class CsvParser
{
    public static List<string[]> Parse(string csvText)
    {
        List<string[]> rows = new();
        List<string> columns = new();
        StringBuilder value = new();

        bool isInsideQuotes = false;

        for (int i = 0; i < csvText.Length; i++)
        {
            char c = csvText[i];

            if (c == '"')
            {
                if (isInsideQuotes &&
                    i + 1 < csvText.Length &&
                    csvText[i + 1] == '"')
                {
                    value.Append('"');
                    i++;
                }
                else
                {
                    isInsideQuotes = !isInsideQuotes;
                }
            }
            else if (c == ',' && !isInsideQuotes)
            {
                columns.Add(value.ToString());
                value.Clear();
            }
            else if ((c == '\n' || c == '\r') && !isInsideQuotes)
            {
                if (c == '\r' &&
                    i + 1 < csvText.Length &&
                    csvText[i + 1] == '\n')
                {
                    i++;
                }

                columns.Add(value.ToString());
                value.Clear();

                rows.Add(columns.ToArray());
                columns.Clear();
            }
            else
            {
                value.Append(c);
            }
        }

        if (isInsideQuotes)
            throw new FormatException("CSV의 큰따옴표가 닫히지 않았습니다.");

        if (value.Length > 0 || columns.Count > 0)
        {
            columns.Add(value.ToString());
            rows.Add(columns.ToArray());
        }

        return rows;
    }
}
