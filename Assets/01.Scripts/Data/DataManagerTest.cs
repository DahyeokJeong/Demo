
using UnityEngine;

public class DataManagerTest : MonoBehaviour
{
    private DataManager dataManager;

    private void Start()
    {
        dataManager = new DataManager();

        Debug.Log($"초기화 전 상태: {dataManager.State}");

        dataManager.Init(OnDataLoaded);

        Debug.Log($"Init 호출 후 상태: {dataManager.State}");
    }

    private void OnDataLoaded(bool success)
    {
        if (!success)
        {
            Debug.LogError("DataManager 초기화 실패");
            return;
        }

        Debug.Log($"초기화 완료 상태: {dataManager.State}");

        foreach (var pair in dataManager.EnemyDic)
        {
            EnemyData enemy = pair.Value;

            Debug.Log(
                $"Id: {enemy.Id}, " +
                $"Name: {enemy.Name}, " +
                $"MaxHp: {enemy.MaxHp}, " +
                $"Attack: {enemy.Attack}"
            );
        }
    }
}
