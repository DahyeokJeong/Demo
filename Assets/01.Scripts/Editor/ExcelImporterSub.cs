/*
using System.IO;                 // File, Path, Stream, StreamReader
using System.IO.Compression;     // ZipArchive, ZipFile
using System.Linq;               // FirstOrDefault 등 LINQ 메서드
using System.Xml.Linq;           // XDocument, XElement, XAttribute, XNamespace
using System.Collections.Generic; // List, Dictionary, HashSet
using UnityEditor;               // MenuItem
using UnityEngine;               // Application, Debug

public static class ExcelImporter
{
    // Unity 상단 메뉴에 Tools/Data/Read Excel 추가
    [MenuItem("Tools/Data/Read Excel")]
    public static void ReadExcel()
    {
        // Application.dataPath는 Unity 프로젝트의 Assets 폴더 경로
        // Path.Combine은 운영체제에 맞게 경로를 결합
        string path = Path.Combine(
            Application.dataPath,
            "02.Data/Excel/ExcelData.xlsx"
        );

        // 파일이 존재하지 않으면 이후 작업을 진행하지 않음
        if (!File.Exists(path))
        {
            Debug.LogError("Excel 파일을 찾을 수 없습니다.");
            return;
        }

        // XLSX 파일은 내부적으로 ZIP 압축 파일
        // using 선언은 메서드 종료 시 리소스를 자동 해제
        using ZipArchive archive = ZipFile.OpenRead(path);


        // ==================================================
        // 1. workbook.xml 읽기
        // ==================================================

        // ZIP 내부에서 지정한 경로의 파일을 가져옴
        // GetEntry는 파일이 없으면 null 반환
        ZipArchiveEntry entry = archive.GetEntry("xl/workbook.xml");

        if (entry == null)
        {
            Debug.LogError("workbook.xml을 찾을 수 없습니다.");
            return;
        }

        // Stream: 파일 데이터를 읽기 위한 데이터 흐름
        // StreamReader: Stream의 데이터를 문자로 읽음
        using Stream stream = entry.Open();
        using StreamReader reader = new StreamReader(stream);

        // XML 데이터를 문서 객체로 변환
        XDocument document = XDocument.Load(reader);

        // XML 요소는 이름뿐 아니라 Namespace까지 일치해야 탐색 가능
        XNamespace ns = document.Root.Name.Namespace;

        // Element()는 조건에 맞는 단일 자식 요소 반환
        // Elements()는 조건에 맞는 여러 자식 요소 반환
        XElement sheets = document.Root.Element(ns + "sheets");

        if (sheets == null)
        {
            Debug.LogError("sheets 요소를 찾을 수 없습니다.");
            return;
        }


        // ==================================================
        // 2. 시트 이름과 관계 ID 확인
        // ==================================================

        // foreach는 컬렉션의 요소를 하나씩 순회
        foreach (XElement sheet in sheets.Elements(ns + "sheet"))
        {
            // Attribute()는 특정 속성 하나를 가져옴
            // ?.는 null일 경우 이후 접근을 중단
            // ??는 왼쪽 값이 null일 경우 오른쪽 값 사용
            string sheetName = sheet.Attribute("name")?.Value ?? "";

            // Attributes()는 모든 속성을 가져옴
            // FirstOrDefault()는 조건에 맞는 첫 번째 항목을 반환
            // 조건에 맞는 항목이 없으면 null 반환
            //
            // r:id는 일반 id가 아니라 relationships Namespace의 속성
            XAttribute relationshipAttribute = sheet.Attributes()
                .FirstOrDefault(attribute =>
                    attribute.Name.LocalName == "id" &&
                    attribute.Name.NamespaceName.Contains("relationships"));

            if (relationshipAttribute == null)
            {
                Debug.LogError($"{sheetName}의 관계 ID를 찾을 수 없습니다.");
                continue;
            }

            // continue는 현재 반복만 건너뛰고 다음 반복으로 이동
            // return은 메서드 전체를 종료
            Debug.Log(
                $"시트 이름: {sheetName} / 관계 ID: {relationshipAttribute.Value}"
            );
        }


        // ==================================================
        // 3. workbook.xml.rels 읽기
        // ==================================================

        // workbook.xml의 관계 ID를 실제 XML 경로와 연결하는 파일
        ZipArchiveEntry relsEntry =
            archive.GetEntry("xl/_rels/workbook.xml.rels");

        if (relsEntry == null)
        {
            Debug.LogError("workbook.xml.rels를 찾을 수 없습니다.");
            return;
        }

        using Stream relsStream = relsEntry.Open();
        using StreamReader relsReader = new StreamReader(relsStream);

        XDocument relsDocument = XDocument.Load(relsReader);
        XNamespace relsNs = relsDocument.Root.Name.Namespace;

        // Dictionary<TKey, TValue>
        // Key를 이용해 Value를 빠르게 검색하는 자료구조
        //
        // Key   : 관계 ID (rId1, rId2 등)
        // Value : 실제 XML 파일 경로
        Dictionary<string, string> relationshipDic =
            new Dictionary<string, string>();

        foreach (XElement relationship in
            relsDocument.Root.Elements(relsNs + "Relationship"))
        {
            string id = relationship.Attribute("Id")?.Value;
            string target = relationship.Attribute("Target")?.Value;

            // IsNullOrEmpty는 null 또는 빈 문자열인지 검사
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(target))
                continue;

            // Dictionary 인덱서
            // Key가 없으면 추가하고, 이미 존재하면 값을 덮어씀
            relationshipDic[id] = target;
        }

        Debug.Log($"저장된 관계 정보 수: {relationshipDic.Count}");


        // ==================================================
        // 4. sharedStrings.xml 읽기
        // ==================================================

        // List<T>는 순서가 있는 동적 컬렉션
        // Excel의 공유 문자열은 인덱스를 통해 참조하므로 List 사용
        List<string> sharedStrings = new List<string>();

        ZipArchiveEntry sharedEntry =
            archive.GetEntry("xl/sharedStrings.xml");

        // 모든 XLSX 파일에 sharedStrings.xml이 있는 것은 아님
        if (sharedEntry != null)
        {
            using Stream sharedStream = sharedEntry.Open();
            using StreamReader sharedReader = new StreamReader(sharedStream);

            XDocument sharedDocument = XDocument.Load(sharedReader);
            XNamespace sharedNs = sharedDocument.Root.Name.Namespace;

            // si는 Shared String Item
            foreach (XElement item in
                sharedDocument.Root.Elements(sharedNs + "si"))
            {
                // XElement.Value는 요소 내부의 텍스트를 반환
                // 하위 요소가 있다면 그 텍스트도 연결해서 반환
                sharedStrings.Add(item.Value);
            }
        }

        Debug.Log($"공유 문자열 개수: {sharedStrings.Count}");


        // ==================================================
        // 5. 시트 XML 찾기
        // ==================================================

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

            // TryGetValue(Key, out Value)
            // Key가 존재하면 true를 반환하고 out 변수에 값을 저장
            // Key가 존재하지 않으면 false 반환
            if (!relationshipDic.TryGetValue(
                relationshipId, out string target))
            {
                Debug.LogError($"{sheetName}의 XML 경로를 찾을 수 없습니다.");
                continue;
            }

            // 현재 단계에서는 EnemyData 시트만 처리
            if (sheetName != "EnemyData")
                continue;

            // ZIP 내부 파일 경로 앞에 붙은 '/' 제거
            // 현재 Excel 파일의 절대 Target 경로를 기준으로 처리
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

            // sheetData는 실제 행(row)과 셀(c)을 포함
            XElement sheetData =
                sheetDocument.Root.Element(sheetNs + "sheetData");

            if (sheetData == null)
            {
                Debug.LogError("sheetData를 찾을 수 없습니다.");
                continue;
            }

            Debug.Log($"시트 XML 읽기 성공: {sheetName}");


            // ==================================================
            // 6. 자료구조 준비
            // ==================================================

            // HashSet은 중복된 값을 저장하지 않는 컬렉션
            // 발견된 셀 타입을 중복 없이 확인하기 위해 사용
            HashSet<string> cellTypes = new HashSet<string>();

            // 열 인덱스와 헤더 이름을 연결
            //
            // 0 -> Id
            // 1 -> Name
            // 2 -> Element
            // 3 -> MaxHp
            Dictionary<int, string> headers =
                new Dictionary<int, string>();

            // STEP 22
            // 각 Excel 행을 Dictionary로 저장
            // List에는 여러 행의 Dictionary가 들어감
            //
            // rows[0] : 첫 번째 적 데이터
            // rows[1] : 두 번째 적 데이터
            List<Dictionary<string, string>> rows =
                new List<Dictionary<string, string>>();


            // ==================================================
            // 7. 행과 셀 순회
            // ==================================================

            // row는 Excel의 한 행
            foreach (XElement row in
                sheetData.Elements(sheetNs + "row"))
            {
                // r 속성은 행 번호를 나타냄
                string rowNumber = row.Attribute("r")?.Value ?? "";

                // 현재 Excel에서는 1행을 헤더로 사용
                bool isHeader = rowNumber == "1";

                // 매 행마다 새로운 Dictionary 생성
                // 하나의 Dictionary에 하나의 적 데이터를 저장
                Dictionary<string, string> rowData =
                    new Dictionary<string, string>();

                // c는 Excel의 개별 셀
                foreach (XElement cell in row.Elements(sheetNs + "c"))
                {
                    // 셀 주소: A1, B2, AA5 등
                    string cellAddress = cell.Attribute("r")?.Value ?? "";

                    // t 속성은 셀의 저장 타입
                    // n         : 숫자
                    // str       : 문자열 결과
                    // s         : 공유 문자열 인덱스
                    // inlineStr : 셀 내부에 직접 저장된 문자열
                    string cellType = cell.Attribute("t")?.Value ?? "n";

                    // 타입에 맞는 실제 셀 값 추출
                    string cellValue =
                        GetCellValue(cell, sheetNs, sharedStrings);

                    // Excel 열 주소를 0부터 시작하는 인덱스로 변환
                    int columnIndex = GetColumnIndex(cellAddress);

                    if (isHeader)
                    {
                        // 첫 번째 행이면 헤더 이름 저장
                        headers[columnIndex] = cellValue;
                    }
                    else
                    {
                        // 데이터 행이면 해당 열의 헤더 이름 조회
                        if (headers.TryGetValue(
                            columnIndex, out string headerName))
                        {
                            // STEP 22
                            // Key   : 헤더 이름
                            // Value : 실제 셀 값
                            //
                            // 예: rowData["Id"] = "1001"
                            //     rowData["Name"] = "도깨비"
                            rowData[headerName] = cellValue;
                        }
                    }

                    // HashSet은 중복된 타입을 자동으로 제외
                    cellTypes.Add(cellType);

                    Debug.Log(
                        $"행: {rowNumber} / 셀: {cellAddress} / " +
                        $"타입: {cellType} / 값: {cellValue}"
                    );
                }

                // 헤더가 아닌 행이며 실제 데이터가 존재할 때 저장
                if (!isHeader && rowData.Count > 0)
                {
                    // List에 완성된 Dictionary 추가
                    rows.Add(rowData);
                }
            }


            // ==================================================
            // 8. 결과 출력
            // ==================================================

            // KeyValuePair는 Dictionary의 Key와 Value 한 쌍
            foreach (KeyValuePair<int, string> header in headers)
            {
                Debug.Log(
                    $"열 인덱스: {header.Key} / 헤더: {header.Value}"
                );
            }

            foreach (string type in cellTypes)
            {
                Debug.Log($"발견된 셀 타입: {type}");
            }

            // Count는 List에 저장된 요소의 개수
            Debug.Log($"저장된 데이터 행 수: {rows.Count}");

            // 바깥 foreach는 행을 순회
            foreach (Dictionary<string, string> rowData in rows)
            {
                // 안쪽 foreach는 해당 행의 Key/Value를 순회
                foreach (KeyValuePair<string, string> data in rowData)
                {
                    Debug.Log($"{data.Key} : {data.Value}");
                }
            }
        }
    }


    // ==================================================
    // 9. 셀 값 추출
    // ==================================================

    // private : 클래스 외부에서 직접 접근 불가
    // static  : 객체 생성 없이 호출 가능
    // string  : 반환형
    //
    // 매개변수
    // cell          : 현재 셀 XML
    // ns            : 해당 시트의 XML Namespace
    // sharedStrings : 공유 문자열 목록
    private static string GetCellValue(
        XElement cell,
        XNamespace ns,
        List<string> sharedStrings)
    {
        string cellType = cell.Attribute("t")?.Value ?? "n";

        // switch는 값에 따라 실행할 분기를 선택
        switch (cellType)
        {
            case "inlineStr":
                // inlineStr은 is 요소에 문자열을 저장
                XElement inlineString = cell.Element(ns + "is");
                return inlineString?.Value ?? "";

            case "n":
            case "str":
                // n과 str은 v 요소에서 값을 가져옴
                XElement valueElement = cell.Element(ns + "v");
                return valueElement?.Value ?? "";

            case "s":
                // s 타입의 v 값은 문자열 자체가 아닌 인덱스
                string indexText = cell.Element(ns + "v")?.Value;

                // TryParse는 문자열을 int로 변환 시도
                // 성공하면 true와 변환된 정수를 반환
                // 실패하면 false 반환
                if (!int.TryParse(indexText, out int index))
                {
                    Debug.LogWarning(
                        "공유 문자열 인덱스를 변환할 수 없습니다."
                    );
                    return "";
                }

                // List 범위를 벗어난 인덱스 접근 방지
                if (index < 0 || index >= sharedStrings.Count)
                {
                    Debug.LogWarning(
                        $"잘못된 공유 문자열 인덱스: {index}"
                    );
                    return "";
                }

                // 공유 문자열 목록에서 실제 값 가져오기
                return sharedStrings[index];

            default:
                Debug.LogWarning(
                    $"아직 지원하지 않는 셀 타입: {cellType}"
                );
                return "";
        }
    }


    // ==================================================
    // 10. Excel 열 주소를 인덱스로 변환
    // ==================================================

    // Excel은 A, B, C ... Z, AA, AB 순서로 열을 표시
    // C# List와 배열은 0부터 인덱스가 시작
    //
    // A1  -> 0
    // B2  -> 1
    // Z3  -> 25
    // AA5 -> 26
    // AB5 -> 27
    private static int GetColumnIndex(string cellAddress)
    {
        int column = 0;

        // string은 char의 연속이므로 foreach로 문자 순회 가능
        foreach (char c in cellAddress)
        {
            // A~Z가 아닌 문자를 만나면 반복 종료
            // 셀 주소에서 숫자 부분을 제외하기 위한 조건
            if (c < 'A' || c > 'Z')
                break;

            // 알파벳을 1~26 숫자로 변환
            // AA의 경우:
            // 첫 번째 A -> 1
            // 두 번째 A -> 1 * 26 + 1 = 27
            column = column * 26 + (c - 'A' + 1);
        }

        // Excel 열 번호는 1부터, C# 인덱스는 0부터 시작
        return column - 1;
    }
}
*/