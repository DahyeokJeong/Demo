using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CsvImporter
{
    [MenuItem("Tools/Data/Read CSV")]
    public static void ReadCsv()
    {
        string path = Path.Combine(
            Application.dataPath,
            "02.Data/CSV/EnemyData.csv"
        );

        if (!File.Exists(path))
        {
            Debug.LogError($"CSV 파일을 찾을 수 없습니다: {path}");
            return;
        }

        string csvText = File.ReadAllText(path);

        List<string[]> rows = CsvParser.Parse(csvText);

        if (rows.Count < 2)
        {
            Debug.LogWarning("CSV에 데이터가 없습니다.");
            return;
        }

        string[] headers = rows[0];
        string[] values = rows[1];

        if (headers.Length != values.Length)
        {
            Debug.LogError("CSV 헤더와 데이터의 열 개수가 다릅니다.");
            return;
        }

        EnemyData enemy = new EnemyData();
        Type type = typeof(EnemyData);

        for (int i = 0; i < headers.Length; i++)
        {
            string header = headers[i];
            string csvValue = values[i];

            FieldInfo field = type.GetField(header);

            if (field == null)
            {
                Debug.LogWarning($"일치하는 필드가 없습니다: {header}");
                continue;
            }

            try
            {
                object convertedValue = Convert.ChangeType(
                    csvValue,
                    field.FieldType,
                    CultureInfo.InvariantCulture
                );

                field.SetValue(enemy, convertedValue);
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"필드 변환 실패: {header}, 값: {csvValue}\n{e.Message}"
                );
                return;
            }
        }

        Debug.Log(
            $"Enemy 생성 완료 / " +
            $"Id: {enemy.Id}, " +
            $"Name: {enemy.Name}, " 
           // $"Hp: {enemy.Hp}"
        );
    }

    [MenuItem("Tools/Data/Test Reflection")]
    public static void TestReflection()
    {
        EnemyData enemy = new EnemyData();

        System.Type type = typeof(EnemyData);

        System.Reflection.FieldInfo field = type.GetField("Hp");

        field.SetValue(enemy, 100);

        //Debug.Log($"Enemy HP : {enemy.Hp}");
    }
}
