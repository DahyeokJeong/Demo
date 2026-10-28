using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ExcelImporter
{
    [MenuItem("Tools/Data/Read Excel")]
    public static void ReadExcel()
    {
        string path = Path.Combine(Application.dataPath, "02.Data/Excel/ExcelData.xlsx");

        if (!File.Exists(path))
        {
            Debug.LogError("Excel 파일을 찾을 수 없습니다.");
            return;
        }

        using ZipArchive archive = ZipFile.OpenRead(path);

        ZipArchiveEntry entry = archive.GetEntry("xl/workbook.xml");

        if (entry == null)
        {
            Debug.LogError("workbook.xml을 찾을 수 없습니다.");
            return;
        }

        using Stream stream = entry.Open();
        using StreamReader reader = new StreamReader(stream);

        XDocument document = XDocument.Load(reader);
        XNamespace ns = document.Root.Name.Namespace;
        XElement sheets = document.Root.Element(ns + "sheets");

        if (sheets == null)
        {
            Debug.LogError("sheets 요소를 찾을 수 없습니다.");
            return;
        }

        foreach (XElement sheet in sheets.Elements(ns + "sheet"))
        {
            string sheetName = sheet.Attribute("name")?.Value ?? "";

            XAttribute relationshipAttribute = sheet.Attributes()
                .FirstOrDefault(attribute =>
                    attribute.Name.LocalName == "id" &&
                    attribute.Name.NamespaceName.Contains("relationships"));

            if (relationshipAttribute == null)
            {
                Debug.LogError($"{sheetName}의 관계 ID를 찾을 수 없습니다.");
                continue;
            }

            Debug.Log($"시트 이름: {sheetName} / 관계 ID: {relationshipAttribute.Value}");
        }

        ZipArchiveEntry relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");

        if (relsEntry == null)
        {
            Debug.LogError("workbook.xml.rels를 찾을 수 없습니다.");
            return;
        }

        using Stream relsStream = relsEntry.Open();
        using StreamReader relsReader = new StreamReader(relsStream);

        XDocument relsDocument = XDocument.Load(relsReader);
        XNamespace relsNs = relsDocument.Root.Name.Namespace;

        Dictionary<string, string> relationshipDic =
            new Dictionary<string, string>();

        foreach (XElement relationship in relsDocument.Root.Elements(relsNs + "Relationship"))
        {
            string id = relationship.Attribute("Id")?.Value;
            string target = relationship.Attribute("Target")?.Value;

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(target))
                continue;

            relationshipDic[id] = target;
        }

        Debug.Log($"저장된 관계 정보 수: {relationshipDic.Count}");

        List<string> sharedStrings = new List<string>();

        ZipArchiveEntry sharedEntry = archive.GetEntry("xl/sharedStrings.xml");

        if (sharedEntry != null)
        {
            using Stream sharedStream = sharedEntry.Open();
            using StreamReader sharedReader = new StreamReader(sharedStream);

            XDocument sharedDocument = XDocument.Load(sharedReader);
            XNamespace sharedNs = sharedDocument.Root.Name.Namespace;

            foreach (XElement item in sharedDocument.Root.Elements(sharedNs + "si"))
            {
                sharedStrings.Add(item.Value);
            }
        }

        Debug.Log($"공유 문자열 개수: {sharedStrings.Count}");

        foreach (XElement sheet in sheets.Elements(ns + "sheet"))
        {
            string sheetName = sheet.Attribute("name")?.Value ?? "";

            XAttribute relationshipAttribute = sheet.Attributes()
                .FirstOrDefault(attribute =>
                    attribute.Name.LocalName == "id" &&
                    attribute.Name.NamespaceName.Contains("relationships"));

            if (relationshipAttribute == null)
                continue;

            string relationshipId = relationshipAttribute.Value;

            if (!relationshipDic.TryGetValue(relationshipId, out string target))
            {
                Debug.LogError($"{sheetName}의 XML 경로를 찾을 수 없습니다.");
                continue;
            }

            if (sheetName != "EnemyData")
                continue;

            string sheetPath = target.TrimStart('/');
            ZipArchiveEntry sheetEntry = archive.GetEntry(sheetPath);

            if (sheetEntry == null)
            {
                Debug.LogError($"{sheetName}의 XML 파일을 찾을 수 없습니다.");
                continue;
            }

            using Stream sheetStream = sheetEntry.Open();
            using StreamReader sheetReader = new StreamReader(sheetStream);

            XDocument sheetDocument = XDocument.Load(sheetReader);
            XNamespace sheetNs = sheetDocument.Root.Name.Namespace;
            XElement sheetData = sheetDocument.Root.Element(sheetNs + "sheetData");

            if (sheetData == null)
            {
                Debug.LogError("sheetData를 찾을 수 없습니다.");
                continue;
            }

            Debug.Log($"시트 XML 읽기 성공: {sheetName}");

            HashSet<string> cellTypes = new HashSet<string>();
            Dictionary<int, string> headers = new Dictionary<int, string>();

            List<Dictionary<string, string>> rows =
                new List<Dictionary<string, string>>();

            foreach (XElement row in sheetData.Elements(sheetNs + "row"))
            {
                string rowNumber = row.Attribute("r")?.Value ?? "";
                bool isHeader = rowNumber == "1";

                Dictionary<string, string> rowData =
                    new Dictionary<string, string>();

                foreach (XElement cell in row.Elements(sheetNs + "c"))
                {
                    string cellAddress = cell.Attribute("r")?.Value ?? "";
                    string cellType = cell.Attribute("t")?.Value ?? "n";
                    string cellValue = GetCellValue(cell, sheetNs, sharedStrings);
                    int columnIndex = GetColumnIndex(cellAddress);

                    if (isHeader)
                    {
                        headers[columnIndex] = cellValue;
                    }
                    else
                    {
                        if (headers.TryGetValue(columnIndex, out string headerName))
                        {
                            rowData[headerName] = cellValue;
                        }
                    }

                    cellTypes.Add(cellType);

                    Debug.Log(
                        $"행: {rowNumber} / 셀: {cellAddress} / 타입: {cellType} / 값: {cellValue}"
                    );
                }

                if (!isHeader && rowData.Count > 0)
                {
                    rows.Add(rowData);
                }
            }

            foreach (KeyValuePair<int, string> header in headers)
            {
                Debug.Log($"열 인덱스: {header.Key} / 헤더: {header.Value}");
            }

            foreach (string type in cellTypes)
            {
                Debug.Log($"발견된 셀 타입: {type}");
            }

            Debug.Log($"저장된 데이터 행 수: {rows.Count}");

            foreach (Dictionary<string, string> rowData in rows)
            {
                foreach (KeyValuePair<string, string> data in rowData)
                {
                    Debug.Log($"{data.Key} : {data.Value}");
                }
            }
        }
    }

    private static string GetCellValue(
        XElement cell,
        XNamespace ns,
        List<string> sharedStrings)
    {
        string cellType = cell.Attribute("t")?.Value ?? "n";

        switch (cellType)
        {
            case "inlineStr":
                XElement inlineString = cell.Element(ns + "is");
                return inlineString?.Value ?? "";

            case "n":
            case "str":
                XElement valueElement = cell.Element(ns + "v");
                return valueElement?.Value ?? "";

            case "s":
                string indexText = cell.Element(ns + "v")?.Value;

                if (!int.TryParse(indexText, out int index))
                {
                    Debug.LogWarning("공유 문자열 인덱스를 변환할 수 없습니다.");
                    return "";
                }

                if (index < 0 || index >= sharedStrings.Count)
                {
                    Debug.LogWarning($"잘못된 공유 문자열 인덱스: {index}");
                    return "";
                }

                return sharedStrings[index];

            default:
                Debug.LogWarning($"아직 지원하지 않는 셀 타입: {cellType}");
                return "";
        }
    }

    private static int GetColumnIndex(string cellAddress)
    {
        int column = 0;

        foreach (char c in cellAddress)
        {
            if (c < 'A' || c > 'Z')
                break;

            column = column * 26 + (c - 'A' + 1);
        }

        return column - 1;
    }
}