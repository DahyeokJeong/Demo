
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class DataManager
{
    public enum InitState
    {
        None,
        Loading,
        Completed,
        Failed
    }

    public Dictionary<int, EnemyData> EnemyDic { get; private set; }
        = new Dictionary<int, EnemyData>();

    public InitState State { get; private set; } = InitState.None;

    public bool IsInitialized => State == InitState.Completed;

    public void Init(Action<bool> onComplete = null)
    {
        if (State == InitState.Loading)
        {
            Debug.LogWarning("DataManager 초기화가 이미 진행 중입니다.");
            return;
        }

        if (State == InitState.Completed)
        {
            onComplete?.Invoke(true);
            return;
        }

        State = InitState.Loading;
        EnemyDic.Clear();

        AsyncOperationHandle<TextAsset> handle =
            Addressables.LoadAssetAsync<TextAsset>("EnemyData");

        handle.Completed += operation =>
        {
            if (operation.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("EnemyData JSON 로드 실패");
                Addressables.Release(operation);
                State = InitState.Failed;
                onComplete?.Invoke(false);
                return;
            }

            try
            {
                TextAsset textAsset = operation.Result;

                DataLoader<EnemyData> loader =
                    JsonUtility.FromJson<DataLoader<EnemyData>>(textAsset.text);

                if (loader == null || loader.Items == null)
                {
                    throw new Exception("EnemyData JSON 변환 실패");
                }

                Dictionary<int, EnemyData> loadedDic =
                    new Dictionary<int, EnemyData>();

                foreach (EnemyData enemy in loader.Items)
                {
                    if (enemy == null)
                        continue;

                    if (loadedDic.ContainsKey(enemy.Id))
                    {
                        Debug.LogWarning($"중복된 Enemy Id: {enemy.Id}");
                        continue;
                    }

                    loadedDic.Add(enemy.Id, enemy);
                }

                EnemyDic = loadedDic;
                State = InitState.Completed;

                Debug.Log($"EnemyData 로드 완료: {EnemyDic.Count}개");
            }
            catch (Exception e)
            {
                EnemyDic.Clear();
                State = InitState.Failed;

                Debug.LogError($"DataManager 초기화 실패: {e.Message}");
            }
            finally
            {
                Addressables.Release(operation);
            }

            onComplete?.Invoke(IsInitialized);
        };
    }

    public EnemyData GetEnemy(int id)
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("DataManager 초기화가 완료되지 않았습니다.");
            return null;
        }

        if (EnemyDic.TryGetValue(id, out EnemyData enemy))
        {
            return enemy;
        }

        Debug.LogWarning($"EnemyData를 찾을 수 없습니다: {id}");
        return null;
    }
}
