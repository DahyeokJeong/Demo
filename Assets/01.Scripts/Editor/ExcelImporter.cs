
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using UnityEditor;
using UnityEngine;

public static class ExcelImporter
{
    private const string ExcelRelativePath = "02.Data/Excel/ExcelData.xlsx";
    private const string JsonRelativePath = "02.Data/JSON";

    [MenuItem("Tools/Data/Read Excel")]
    public static void ReadExcel()
    {
        string excelPath = Path.Combine(
            Application.dataPath,
            ExcelRelativePath
        );

        if (!File.Exists(excelPath))
        {
            Debug.LogError($"Excel 파일을 찾을 수 없습니다: {excelPath}");
            return;
        }

        List<string> sheetNames = ExcelReader.GetSheetNames(excelPath);

        Dictionary<string, Type> typeDic = FindExcelDataTypes();

        foreach (string sheetName in sheetNames)
        {
            if (!typeDic.TryGetValue(sheetName, out Type dataType))
            {
                Debug.LogWarning(
                    $"시트에 연결된 데이터 클래스를 찾을 수 없습니다: {sheetName}"
                );
                continue;
            }

            ImportSheet(excelPath, sheetName, dataType);
        }
    }

    private static Dictionary<string, Type> FindExcelDataTypes()
    {
        Dictionary<string, Type> typeDic =
            new Dictionary<string, Type>();

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types;
            }

            foreach (Type type in types)
            {
                if (type == null || !type.IsClass || type.IsAbstract)
                    continue;

                ExcelSheetAttribute attribute =
                    type.GetCustomAttribute<ExcelSheetAttribute>();

                if (attribute == null)
                    continue;

                if (typeDic.ContainsKey(attribute.SheetName))
                {
                    Debug.LogError(
                        $"중복된 Excel 시트 등록: {attribute.SheetName}"
                    );
                    continue;
                }

                typeDic.Add(attribute.SheetName, type);
            }
        }

        return typeDic;
    }

    private static void ImportSheet(
        string excelPath,
        string sheetName,
        Type dataType)
    {
        List<Dictionary<string, string>> rows =
            ExcelReader.Read(excelPath, sheetName);

        if (rows.Count == 0)
        {
            Debug.LogWarning($"변환할 데이터가 없습니다: {sheetName}");
            return;
        }

        Type loaderType = typeof(DataLoader<>).MakeGenericType(dataType);

        object loader = Activator.CreateInstance(loaderType);

        FieldInfo itemsField = loaderType.GetField("Items");

        IList dataList = (IList)itemsField.GetValue(loader);

        foreach (Dictionary<string, string> rowData in rows)
        {
            object dataObject = Activator.CreateInstance(dataType);

            foreach (KeyValuePair<string, string> data in rowData)
            {
                FieldInfo field = dataType.GetField(data.Key);

                if (field == null)
                {
                    Debug.LogWarning(
                        $"{dataType.Name} 필드를 찾을 수 없습니다: {data.Key}"
                    );
                    continue;
                }

                if (string.IsNullOrWhiteSpace(data.Value))
                    continue;

                try
                {
                    object convertedValue = Convert.ChangeType(
                        data.Value,
                        field.FieldType,
                        CultureInfo.InvariantCulture
                    );

                    field.SetValue(dataObject, convertedValue);
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        $"데이터 변환 실패 / 시트: {sheetName} / " +
                        $"필드: {data.Key} / 값: {data.Value} / 오류: {e.Message}"
                    );
                }
            }

            dataList.Add(dataObject);
        }

        string json = JsonUtility.ToJson(loader, true);

        string outputDirectory = Path.Combine(
            Application.dataPath,
            JsonRelativePath
        );

        Directory.CreateDirectory(outputDirectory);

        string fileName = $"{sheetName}.json";

        string outputPath = Path.Combine(
            outputDirectory,
            fileName
        );

        File.WriteAllText(outputPath, json);

        AssetDatabase.Refresh();

        string assetPath = $"Assets/{JsonRelativePath}/{fileName}";

        bool registered = AddressableRegistrar.Register(
            assetPath,
            sheetName
        );

        if (!registered)
        {
            Debug.LogError(
                $"Addressables 등록 실패: {sheetName}"
            );
            return;
        }

        Debug.Log(
            $"데이터 변환 완료 / 시트: {sheetName} / " +
            $"타입: {dataType.Name} / 개수: {dataList.Count}"
        );
    }
}
