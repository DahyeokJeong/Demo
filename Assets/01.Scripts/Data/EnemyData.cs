
using System;

[Serializable]
[ExcelSheet("EnemyData")]
public class EnemyData
{
    public int Id;
    public string Name;
    public string Element;
    public int MaxHp;
    public int Attack;
    public string PrefabKey;
    public string Description;
}
