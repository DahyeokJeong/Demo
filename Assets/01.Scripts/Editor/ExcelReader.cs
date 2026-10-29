
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using UnityEngine;

public static class ExcelReader
{
    public static List<Dictionary<string, string>> Read(
        string path,
        string sheetName)
    {
        List<Dictionary<string, string>> rows =
            new List<Dictionary<string, string>>();

        if (!File.Exists(path))
        {
            Debug.LogError("Excel 파일을 찾을 수 없습니다.");
            return rows;
        }

        using ZipArchive archive = ZipFile.OpenRead(path);

        ZipArchiveEntry workbookEntry =
            archive.GetEntry("xl/workbook.xml");

        if (workbookEntry == null)
        {
            Debug.LogError("workbook.xml을 찾을 수 없습니다.");
            return rows;
        }

        XDocument workbookDocument;

        using (Stream stream = workbookEntry.Open())
        {
            workbookDocument = XDocument.Load(stream);
        }

        XNamespace workbookNs = workbookDocument.Root.Name.Namespace;

        XElement sheets =
            workbookDocument.Root.Element(workbookNs + "sheets");

        if (sheets == null)
        {
            Debug.LogError("sheets 요소를 찾을 수 없습니다.");
            return rows;
        }

        ZipArchiveEntry relsEntry =
            archive.GetEntry("xl/_rels/workbook.xml.rels");

        if (relsEntry == null)
        {
            Debug.LogError("workbook.xml.rels를 찾을 수 없습니다.");
            return rows;
        }

        XDocument relsDocument;

        using (Stream stream = relsEntry.Open())
        {
            relsDocument = XDocument.Load(stream);
        }

        XNamespace relsNs = relsDocument.Root.Name.Namespace;

        Dictionary<string, string> relationshipDic =
            new Dictionary<string, string>();

        foreach (XElement relationship in
            relsDocument.Root.Elements(relsNs + "Relationship"))
        {
            string id = relationship.Attribute("Id")?.Value;
            string target = relationship.Attribute("Target")?.Value;

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(target))
                continue;

            relationshipDic[id] = target;
        }

        List<string> sharedStrings = new List<string>();

        ZipArchiveEntry sharedEntry =
            archive.GetEntry("xl/sharedStrings.xml");

        if (sharedEntry != null)
        {
            using Stream sharedStream = sharedEntry.Open();

            XDocument sharedDocument = XDocument.Load(sharedStream);
            XNamespace sharedNs = sharedDocument.Root.Name.Namespace;

            foreach (XElement item in
                sharedDocument.Root.Elements(sharedNs + "si"))
            {
                sharedStrings.Add(item.Value);
            }
        }

        XElement selectedSheet = sheets.Elements(workbookNs + "sheet")
            .FirstOrDefault(sheet =>
                sheet.Attribute("name")?.Value == sheetName);

        if (selectedSheet == null)
        {
            Debug.LogError($"시트를 찾을 수 없습니다: {sheetName}");
            return rows;
        }

        XAttribute relationshipAttribute = selectedSheet.Attributes()
            .FirstOrDefault(attribute =>
                attribute.Name.LocalName == "id" &&
                attribute.Name.NamespaceName.Contains("relationships"));

        if (relationshipAttribute == null)
        {
            Debug.LogError($"{sheetName}의 관계 ID를 찾을 수 없습니다.");
            return rows;
        }

        string relationshipId = relationshipAttribute.Value;

        if (!relationshipDic.TryGetValue(relationshipId, out string targetPath))
        {
            Debug.LogError($"{sheetName}의 XML 경로를 찾을 수 없습니다.");
            return rows;
        }

        string sheetPath = targetPath.StartsWith("/")
            ? targetPath.TrimStart('/')
            : "xl/" + targetPath;

        ZipArchiveEntry sheetEntry = archive.GetEntry(sheetPath);

        if (sheetEntry == null)
        {
            Debug.LogError($"{sheetName}의 XML 파일을 찾을 수 없습니다.");
            return rows;
        }

        XDocument sheetDocument;

        using (Stream stream = sheetEntry.Open())
        {
            sheetDocument = XDocument.Load(stream);
        }

        XNamespace sheetNs = sheetDocument.Root.Name.Namespace;

        XElement sheetData =
            sheetDocument.Root.Element(sheetNs + "sheetData");

        if (sheetData == null)
        {
            Debug.LogError("sheetData를 찾을 수 없습니다.");
            return rows;
        }

        Dictionary<int, string> headers =
            new Dictionary<int, string>();

        foreach (XElement row in sheetData.Elements(sheetNs + "row"))
        {
            string rowNumber = row.Attribute("r")?.Value ?? "";
            bool isHeader = rowNumber == "1";

            Dictionary<string, string> rowData =
                new Dictionary<string, string>();

            foreach (XElement cell in row.Elements(sheetNs + "c"))
            {
                string cellAddress = cell.Attribute("r")?.Value ?? "";

                int columnIndex = GetColumnIndex(cellAddress);

                if (columnIndex < 0)
                    continue;

                string cellValue =
                    GetCellValue(cell, sheetNs, sharedStrings);

                if (isHeader)
                {
                    headers[columnIndex] = cellValue;
                }
                else if (headers.TryGetValue(
                    columnIndex, out string headerName))
                {
                    if (!string.IsNullOrEmpty(headerName))
                        rowData[headerName] = cellValue;
                }
            }

            if (!isHeader && rowData.Count > 0)
            {
                rows.Add(rowData);
            }
        }

        Debug.Log($"{sheetName} 데이터 행 수: {rows.Count}");

        return rows;
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
                Debug.LogWarning($"지원하지 않는 셀 타입: {cellType}");
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


    public static List<string> GetSheetNames(string excelPath)
    {
        List<string> sheetNames = new List<string>();

        using (ZipArchive archive = ZipFile.OpenRead(excelPath))
        {
            ZipArchiveEntry workbookEntry =
                archive.GetEntry("xl/workbook.xml");

            if (workbookEntry == null)
                throw new Exception("workbook.xml을 찾을 수 없습니다.");

            using (Stream stream = workbookEntry.Open())
            {
                XDocument document = XDocument.Load(stream);

                XNamespace ns =
                    "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

                foreach (XElement sheet in document.Descendants(ns + "sheet"))
                {
                    string sheetName = (string)sheet.Attribute("name");

                    if (!string.IsNullOrEmpty(sheetName))
                        sheetNames.Add(sheetName);
                }
            }
        }

        return sheetNames;
    }

}
