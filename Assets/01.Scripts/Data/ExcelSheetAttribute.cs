
using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ExcelSheetAttribute : Attribute
{
    public string SheetName { get; }

    public ExcelSheetAttribute(string sheetName)
    {
        SheetName = sheetName;
    }
}
